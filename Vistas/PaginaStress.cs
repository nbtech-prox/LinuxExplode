using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

/// <summary>
/// Secção de stress: CPU (quatro cargas), RAM, GPU, disco e tudo ao mesmo tempo, com guarda térmica
/// e leitura ao vivo de temperaturas, potência e frequência. Todos os números vêm das medições.
/// </summary>
public sealed class PaginaStress : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };

    // configuração
    private readonly ComboBox _duracao = new() { Width = 140 };
    private readonly ComboBox _threads = new() { Width = 140 };
    private readonly ComboBox _carga = new() { Width = 190 };
    private readonly ComboBox _memoria = new() { Width = 140 };
    private readonly ComboBox _limite = new() { Width = 140 };
    private readonly ComboBox _tamanho = new() { Width = 140 };

    // ao vivo
    private readonly TextBlock _tempo = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _tempCpu = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _tempGpu = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _potencia = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _fase = new() { Text = Core.Textos.T("pronto para começar"), Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _barra = new() { Minimum = 0, Maximum = 1, Value = 0 };

    // resultado e registo
    private readonly StackPanel _resultado = new() { Spacing = 2 };
    private readonly TextBlock _resumo = new() { Text = Core.Textos.T("sem resultados"), Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _registo = new() { Text = "", Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };
    private readonly List<string> _eventos = new();
    private readonly Button _parar;
    private readonly Button _guardar;
    private readonly List<Button> _botoes = new();

    private SessaoStress? _sessao;
    private ResultadoStress? _ultimo;
    private DateTime _inicio;
    private TimeSpan _duracaoEscolhida = TimeSpan.FromMinutes(5);

    // ciclos agendados
    private readonly ComboBox _ciclosAlvo = new() { Width = 170 };
    private readonly ComboBox _ciclosN = new() { Width = 110 };
    private readonly ComboBox _ciclosDuracao = new() { Width = 130 };
    private readonly ComboBox _ciclosPausa = new() { Width = 130 };
    private readonly TextBlock _ciclosEstado = new() { Text = Core.Textos.T("pronto"), Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _ciclosResumo = new() { Text = Core.Textos.T("sem série registada"), Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _ciclosTabela = new() { Spacing = 4 };
    private readonly List<Button> _botoesCiclos = new();
    private SessaoCiclos? _serie;
    private bool _ciclosTerminado;

    // enchimento de VRAM
    private readonly ComboBox _vramGpu = new();
    private readonly ComboBox _vramLimite = new() { Width = 160 };
    private readonly ComboBox _vramBloco = new() { Width = 130 };
    private readonly TextBlock _vramEstado = new() { Text = Core.Textos.T("pronto"), Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _vramResumo = new() { Text = Core.Textos.T("sem teste de VRAM"), Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _vramLinhas = new() { Spacing = 2 };
    private readonly List<Button> _botoesVram = new();
    private CancellationTokenSource? _vramCancelar;
    private List<InfoVram> _vramGpus = new();

    public PaginaStress()
    {
        _parar = UI.Botao("Parar", false, Parar);
        _parar.IsEnabled = false;
        _guardar = UI.Botao("Guardar relatório", false, GuardarRelatorio);
        _guardar.IsEnabled = false;

        Content = UI.Pagina(
            UI.Cabecalho("Stress", "Aquece CPU, RAM, GPU e disco — com guarda térmica e valores medidos", _parar),
            _corpo);

        // gancho de captura: LINUXEXPLODE_SECAO=testes|ciclos|vram|ao-vivo|resultado|registo deixa
        // só esse cartão no topo da janela (não há scroll fiável para fotografar o fundo da página)
        var secao = (Environment.GetEnvironmentVariable("LINUXEXPLODE_SECAO") ?? "").ToLowerInvariant();
        bool Quer(string nome) => secao.Length == 0 || secao == nome;

        if (Quer("config")) _corpo.Children.Add(CartaoConfiguracao());
        if (Quer("testes")) _corpo.Children.Add(CartaoTestes());
        if (Quer("ciclos")) _corpo.Children.Add(CartaoCiclos());
        if (Quer("vram")) _corpo.Children.Add(CartaoVram());
        if (Quer("ao-vivo") || Quer("aovivo")) _corpo.Children.Add(CartaoAoVivo());
        if (Quer("resultado")) _corpo.Children.Add(CartaoResultado());
        if (Quer("registo")) _corpo.Children.Add(CartaoRegisto());

        Evento("pronto · limites de fábrica: CPU/GPU 95 °C");

        Loaded += async (_, _) =>
        {
            await CarregarGpus();

            // verificação automática dos cartões novos: LINUXEXPLODE_AUTOTEST=vram|ciclos
            // (o diálogo tem de ser provado pelo caminho do botão, não só pela linha de comandos)
            var autoTeste = (Environment.GetEnvironmentVariable("LINUXEXPLODE_AUTOTEST") ?? "").ToLowerInvariant();

            if (autoTeste == "vram")
            {
                ComecarVram();
                return;
            }

            if (autoTeste == "ciclos")
            {
                int n = int.TryParse(Environment.GetEnvironmentVariable("LINUXEXPLODE_CICLOS"), out int cn) ? cn : 2;
                int seg = int.TryParse(Environment.GetEnvironmentVariable("LINUXEXPLODE_STRESS_SEG"), out int cs) ? cs : 3;
                ComecarCiclos(TimeSpan.FromSeconds(Math.Clamp(seg, 1, 600)), Math.Clamp(n, 1, 20));
                return;
            }

            // verificação automática: LINUXEXPLODE_STRESS=cpu|ram|gpu|disco|tudo (+ _SEG=N)
            var auto = Environment.GetEnvironmentVariable("LINUXEXPLODE_STRESS");
            if (string.IsNullOrEmpty(auto)) return;

            int seg2 = int.TryParse(Environment.GetEnvironmentVariable("LINUXEXPLODE_STRESS_SEG"), out int v) ? v : 10;
            var alvo = auto.ToLowerInvariant() switch
            {
                "ram" => AlvoStress.Ram,
                "gpu" => AlvoStress.Gpu,
                "disco" => AlvoStress.Disco,
                "tudo" => AlvoStress.Tudo,
                _ => AlvoStress.Cpu
            };

            Comecar(alvo, TimeSpan.FromSeconds(Math.Clamp(seg2, 5, 3600)));
        };
    }

    private async Task CarregarGpus()
    {
        try
        {
            _vramGpus = await Task.Run(StressVram.Dispositivos);
            _vramGpu.Items.Clear();
            foreach (var g in _vramGpus)
                _vramGpu.Items.Add(Textos.TF("[{0}] {1} · {2} · {3}", g.Indice, g.Nome, Textos.T(g.Tipo), Util.Bytes(g.HeapBytes)));

            if (_vramGpus.Count == 0)
            {
                _vramGpu.Items.Add(Textos.T("nenhuma GPU Vulkan — falta o driver"));
                _vramGpu.SelectedIndex = 0;
                foreach (var b in _botoesVram) b.IsEnabled = false;
            }
            else _vramGpu.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            _vramGpu.Items.Add("falha a listar GPUs: " + ex.Message);
            _vramGpu.SelectedIndex = 0;
        }
    }

    // ------------------------------------------------------------------ configuração

    private Control CartaoConfiguracao()
    {
        _duracao.Items.Add(Textos.T("1 minuto"));
        _duracao.Items.Add(Textos.T("2 minutos"));
        _duracao.Items.Add(Textos.T("5 minutos"));
        _duracao.Items.Add(Textos.T("10 minutos"));
        _duracao.Items.Add(Textos.T("20 minutos"));
        _duracao.Items.Add(Textos.T("30 minutos"));
        _duracao.SelectedIndex = 2;

        int nucleos = Environment.ProcessorCount;
        _threads.Items.Add(Textos.TF("todos ({0})", nucleos));
        _threads.Items.Add("1");
        foreach (int n in new[] { 2, 4, 8 }.Where(n => n < nucleos)) _threads.Items.Add($"{n}");
        _threads.SelectedIndex = 0;

        _carga.Items.Add(Textos.T("FMA vetorial (SIMD)"));
        _carga.Items.Add(Textos.T("inteiro"));
        _carga.Items.Add(Textos.T("vírgula flutuante"));
        _carga.Items.Add(Textos.T("memória"));
        _carga.SelectedIndex = 0;

        _memoria.Items.Add(Textos.T("25 % da RAM"));
        _memoria.Items.Add(Textos.T("50 % da RAM"));
        _memoria.Items.Add(Textos.T("75 % da RAM"));
        _memoria.SelectedIndex = 1;

        _limite.Items.Add("85 °C");
        _limite.Items.Add("90 °C");
        _limite.Items.Add("95 °C");
        _limite.Items.Add("100 °C");
        _limite.SelectedIndex = 2;

        _tamanho.Items.Add("256 MB");
        _tamanho.Items.Add("512 MB");
        _tamanho.Items.Add("1 GB");
        _tamanho.Items.Add("2 GB");
        _tamanho.SelectedIndex = 1;

        var grelha = UI.Grelha(3, 12,
            Campo("Duração", _duracao),
            Campo("Threads de CPU", _threads),
            Campo("Carga de CPU", _carga),
            Campo("Memória a encher", _memoria),
            Campo("Guarda térmica", _limite),
            Campo("Disco por passagem", _tamanho));

        var nota = UI.Texto(
            "A guarda térmica lê os sensores reais (k10temp e amdgpu) quatro vezes por segundo e para tudo " +
            "assim que um limite é passado. O ficheiro de disco é apagado no fim.", "sub");

        var pilha = new StackPanel { Spacing = 12 };
        pilha.Children.Add(grelha);
        pilha.Children.Add(nota);
        return UI.Cartao("Configuração", pilha);
    }

    private static Control Campo(string rotulo, Control controlo)
    {
        var p = new StackPanel { Spacing = 4 };
        p.Children.Add(UI.Texto(Textos.T(rotulo).ToUpperInvariant(), "rotulo"));
        p.Children.Add(controlo);
        return p;
    }

    // ------------------------------------------------------------------ testes

    private Control CartaoTestes()
    {
        var linha = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var (texto, alvo, primario) in new[]
                 {
                     ("Testar CPU", AlvoStress.Cpu, false),
                     ("Testar RAM", AlvoStress.Ram, false),
                     ("Testar GPU", AlvoStress.Gpu, false),
                     ("Testar disco", AlvoStress.Disco, false),
                     ("Tudo (burn-in)", AlvoStress.Tudo, true)
                 })
        {
            var b = UI.Botao(texto, primario, () => Comecar(alvo));
            b.Margin = new Avalonia.Thickness(0, 0, 8, 8);
            _botoes.Add(b);
            linha.Children.Add(b);
        }

        var pilha = new StackPanel { Spacing = 10 };
        pilha.Children.Add(linha);
        pilha.Children.Add(UI.Texto(
            "Cada teste corre durante a duração escolhida. CPU faz passagens contínuas de carga, a RAM " +
            "enche, relê e verifica cada palavra, o disco escreve e relê a verificar, e a GPU dispara o " +
            "path tracer em ciclo. “Tudo” corre os quatro em paralelo — é o teste de estabilidade a sério.", "sub"));
        return UI.Cartao("Testes", pilha);
    }

    // ------------------------------------------------------------------ ciclos agendados

    private Control CartaoCiclos()
    {
        _ciclosAlvo.Items.Add("CPU");
        _ciclosAlvo.Items.Add("RAM");
        _ciclosAlvo.Items.Add("GPU");
        _ciclosAlvo.Items.Add("disco");
        _ciclosAlvo.Items.Add(Textos.T("tudo (burn-in)"));
        _ciclosAlvo.SelectedIndex = 0;

        _ciclosN.Items.Add(Textos.T("3 ciclos"));
        _ciclosN.Items.Add(Textos.T("5 ciclos"));
        _ciclosN.Items.Add(Textos.T("10 ciclos"));
        _ciclosN.Items.Add(Textos.T("20 ciclos"));
        _ciclosN.SelectedIndex = 0;

        _ciclosDuracao.Items.Add("30 s");
        _ciclosDuracao.Items.Add(Textos.T("1 minuto"));
        _ciclosDuracao.Items.Add(Textos.T("2 minutos"));
        _ciclosDuracao.Items.Add(Textos.T("5 minutos"));
        _ciclosDuracao.SelectedIndex = 1;

        _ciclosPausa.Items.Add("10 s");
        _ciclosPausa.Items.Add("30 s");
        _ciclosPausa.Items.Add(Textos.T("1 minuto"));
        _ciclosPausa.Items.Add(Textos.T("2 minutos"));
        _ciclosPausa.SelectedIndex = 1;

        var iniciar = UI.Botao("Iniciar série", true, () => ComecarCiclos());
        var parar = UI.Botao("Parar série", false, PararCiclos);
        parar.IsEnabled = false;
        _botoesCiclos.Add(iniciar);
        _botoesCiclos.Add(parar);

        var acoes = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var b in _botoesCiclos)
        {
            b.Margin = new Avalonia.Thickness(0, 0, 8, 8);
            acoes.Children.Add(b);
        }

        var pilha = new StackPanel { Spacing = 12 };
        pilha.Children.Add(UI.Grelha(4, 12,
            Campo("Alvo", _ciclosAlvo),
            Campo("Duração do ciclo", _ciclosDuracao),
            Campo("Número de ciclos", _ciclosN),
            Campo("Pausa entre ciclos", _ciclosPausa)));
        pilha.Children.Add(acoes);
        pilha.Children.Add(_ciclosEstado);
        pilha.Children.Add(_ciclosResumo);
        pilha.Children.Add(_ciclosTabela);
        pilha.Children.Add(UI.Texto(
            "A série repete o mesmo teste N vezes, com pausa entre ciclos, e dá a mediana de cada métrica " +
            "com o mínimo e o máximo. Um teste longo não mostra se a máquina repete o resultado; N ciclos " +
            "iguais mostram — e se a variação passar os 3 % o cartão avisa. A guarda térmica continua " +
            "activa e um ciclo que a dispare termina a série.", "sub"));
        return UI.Cartao("Ciclos agendados", pilha);
    }

    private void ComecarCiclos(TimeSpan? duracaoFixa = null, int? ciclosFixo = null)
    {
        if (_serie is { EmCurso: true })
        {
            Evento("a série de ciclos já está a correr");
            return;
        }
        if (_sessao is { EmCurso: true })
        {
            Evento("há um teste simples a correr — usa Parar antes de arrancar a série");
            return;
        }
        if (_vramCancelar is not null)
        {
            Evento("o teste de VRAM está a correr — pára-o antes de arrancar a série");
            return;
        }

        var alvo = _ciclosAlvo.SelectedIndex switch
        {
            1 => AlvoStress.Ram,
            2 => AlvoStress.Gpu,
            3 => AlvoStress.Disco,
            4 => AlvoStress.Tudo,
            _ => AlvoStress.Cpu
        };
        int ciclos = ciclosFixo ?? (_ciclosN.SelectedIndex switch { 1 => 5, 2 => 10, 3 => 20, _ => 3 });
        var duracao = duracaoFixa ?? (_ciclosDuracao.SelectedIndex switch
        {
            0 => TimeSpan.FromSeconds(30),
            2 => TimeSpan.FromMinutes(2),
            3 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(1)
        });
        var pausa = _ciclosPausa.SelectedIndex switch
        {
            0 => TimeSpan.FromSeconds(10),
            2 => TimeSpan.FromMinutes(1),
            3 => TimeSpan.FromMinutes(2),
            _ => TimeSpan.FromSeconds(30)
        };

        GuardaTermica.LimiteCpu = _limite.SelectedIndex switch { 0 => 85, 1 => 90, 2 => 95, _ => 95 };
        GuardaTermica.LimiteGpu = GuardaTermica.LimiteCpu;

        int threads = _threads.SelectedIndex switch
        {
            0 => Environment.ProcessorCount,
            1 => 1,
            _ => int.TryParse(_threads.SelectedItem?.ToString(), out int n) ? n : Environment.ProcessorCount
        };

        var carga = _carga.SelectedIndex switch
        {
            1 => CargaCpu.Inteiro,
            2 => CargaCpu.Flutuante,
            3 => CargaCpu.Memoria,
            _ => CargaCpu.Vector
        };

        double fracao = _memoria.SelectedIndex switch { 0 => 0.25, 2 => 0.75, _ => 0.5 };
        long tamanho = _tamanho.SelectedIndex switch
        {
            0 => 256L * 1024 * 1024,
            2 => 1024L * 1024 * 1024,
            3 => 2048L * 1024 * 1024,
            _ => 512L * 1024 * 1024
        };

        _serie?.Dispose();
        _serie = new SessaoCiclos(new OpcoesCiclos(alvo, ciclos, duracao, pausa, threads, carga, fracao,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache", "linux-explode", "stress"),
            tamanho, 0));

        _serie.Progresso += p => Dispatcher.UIThread.Post(() => MostrarProgressoCiclos(p));
        _serie.Terminou += r => Dispatcher.UIThread.Post(() => MostrarCiclos(r));

        _ciclosTabela.Children.Clear();
        _ciclosTerminado = false;
        _ciclosResumo.Text = Textos.TF("a correr: {0} · {1} ciclo(s) de {2} s", NomeAlvo(alvo), ciclos, $"{duracao.TotalSeconds:0}");
        _ciclosEstado.Text = Textos.T("a arrancar…");

        foreach (var b in _botoes) b.IsEnabled = false;
        _botoesCiclos[0].IsEnabled = false;
        _botoesCiclos[1].IsEnabled = true;
        AtualizarBotoesParar();

        Evento(Textos.TF("série · {0} · {1} × {2} s · pausa {3} s", NomeAlvo(alvo), ciclos, $"{duracao.TotalSeconds:0}", $"{pausa.TotalSeconds:0}"));

        try { _serie.Iniciar(); }
        catch (Exception ex)
        {
            Evento(Textos.T("a série não arrancou: ") + ex.Message);
            TerminarCiclosInterface();
        }
    }

    private void PararCiclos()
    {
        if (_serie is not { EmCurso: true }) return;
        _serie.Parar();
        _ciclosEstado.Text = Textos.T("a parar…");
        Evento("pedido de paragem da série de ciclos");
    }

    private void MostrarProgressoCiclos(ProgressoStress p)
    {
        // um progresso atrasado do último ciclo não pode apagar o resultado já apresentado
        if (_ciclosTerminado) return;

        _ciclosEstado.Text = string.IsNullOrEmpty(p.Ritmo)
            ? Textos.TF("{0} s · {1}", $"{p.Segundos:0}", Textos.T(p.Fase))
            : Textos.TF("{0} s · {1} · {2}", $"{p.Segundos:0}", Textos.T(p.Fase), p.Ritmo);
        if (!string.IsNullOrEmpty(p.Detalhe)) _ciclosResumo.Text = Textos.T("a correr · ") + p.Detalhe;
    }

    private void MostrarCiclos(ResultadoCiclos r)
    {
        _ciclosTerminado = true;
        RelatorioCompleto.GuardarUltimosCiclos(r);  // entra no relatório completo
        _ciclosResumo.Text = Textos.TF("{0} · {1} s · energia {2} Wh · ", Textos.T(r.Resumo), $"{r.Segundos:0}", $"{r.EnergiaKwh * 1000:0,0}") +
                             Textos.TF("picos CPU {0} °C / GPU {1} °C", $"{r.PicoCpuC:0}", $"{r.PicoGpuC:0}");

        _ciclosTabela.Children.Clear();
        if (r.Agregado.Count > 0)
        {
            _ciclosTabela.Children.Add(LinhaCiclos(Textos.T("métrica"), Textos.T("mediana"), Textos.T("mínimo"), Textos.T("máximo"), Textos.T("variação"), true));
            foreach (var m in r.Agregado)
                _ciclosTabela.Children.Add(LinhaCiclos(Textos.T(m.Rotulo),
                    m.Contador ? $"{m.Mediana:0}" : $"{m.Mediana:N1}",
                    m.Contador ? Textos.TF("em {0}", m.Amostras) : $"{m.Minimo:N1}",
                    m.Contador ? "—" : $"{m.Maximo:N1}",
                    m.Contador ? "total" : m.Amostras >= 3 ? Textos.TF("±{0} %", $"{m.VariacaoPct:0.0}") : Textos.TF("amostra pequena ({0})", m.Amostras),
                    false));
        }

        for (int i = 0; i < r.Lista.Count; i++)
        {
            var c = r.Lista[i];
            _ciclosTabela.Children.Add(UI.Texto(
                Textos.TF("ciclo {0}: {1} em {2} s · ", i + 1, Textos.T(c.Completo ? "completo" : "parado"), $"{c.Segundos:0}") +
                Textos.TF("pico CPU {0} °C · GPU {1} °C · {2} Wh", $"{c.PicoCpuC:0}", $"{c.PicoGpuC:0}", $"{c.EnergiaKwh * 1000:0.0}"), "mono"));
        }

        _ciclosEstado.Text = r.Parado ? "série parada" : "série terminada";
        Evento(Textos.TF("fim da série · {0} · pico CPU {1} °C / GPU {2} °C", Textos.T(r.Resumo), $"{r.PicoCpuC:0}", $"{r.PicoGpuC:0}"));
        TerminarCiclosInterface();
    }

    private static Control LinhaCiclos(string a, string b, string c, string d, string e, bool cabecalho)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*,*,*,Auto") };
        var textos = new[] { a, b, c, d, e };
        for (int i = 0; i < textos.Length; i++)
        {
            var t = UI.Texto(textos[i], cabecalho ? "rotulo" : "mono");
            t.TextWrapping = TextWrapping.NoWrap;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(t, i);
            g.Children.Add(t);
        }
        return g;
    }

    private void TerminarCiclosInterface()
    {
        if (_vramCancelar is null)
            foreach (var b in _botoes) b.IsEnabled = true;
        _botoesCiclos[0].IsEnabled = true;
        _botoesCiclos[1].IsEnabled = false;
        AtualizarBotoesParar();
    }

    // ------------------------------------------------------------------ VRAM (Vulkan)

    private Control CartaoVram()
    {
        _vramLimite.Items.Add("512 MB");
        _vramLimite.Items.Add("1 GB");
        _vramLimite.Items.Add("2 GB");
        _vramLimite.Items.Add("4 GB");
        _vramLimite.Items.Add("8 GB");
        _vramLimite.Items.Add("até o driver recusar");
        _vramLimite.SelectedIndex = 2;

        _vramBloco.Items.Add("64 MB");
        _vramBloco.Items.Add("128 MB");
        _vramBloco.Items.Add("256 MB");
        _vramBloco.Items.Add("1 GB");
        _vramBloco.SelectedIndex = 1;

        _vramGpu.Items.Add("a listar GPUs…");
        _vramGpu.SelectedIndex = 0;

        var encher = UI.Botao("Encher VRAM", true, ComecarVram);
        var parar = UI.Botao("Parar", false, PararVram);
        parar.IsEnabled = false;
        _botoesVram.Add(encher);
        _botoesVram.Add(parar);

        var acoes = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var b in _botoesVram)
        {
            b.Margin = new Avalonia.Thickness(0, 0, 8, 8);
            acoes.Children.Add(b);
        }

        var pilha = new StackPanel { Spacing = 12 };

        // grelha com a coluna da GPU a crescer: um UniformGrid dava-lhe a mesma largura das outras
        // e o nome do dispositivo (longo) passava por cima do limite
        var grelha = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*,*") };
        var celulas = new Control[]
        {
            Campo("GPU", _vramGpu),
            Campo("Limite", _vramLimite),
            Campo("Bloco", _vramBloco)
        };
        for (int i = 0; i < celulas.Length; i++)
        {
            celulas[i].Margin = new Avalonia.Thickness(6);
            Grid.SetColumn(celulas[i], i);
            grelha.Children.Add(celulas[i]);
        }

        pilha.Children.Add(grelha);
        pilha.Children.Add(acoes);
        pilha.Children.Add(_vramEstado);
        pilha.Children.Add(_vramResumo);
        pilha.Children.Add(_vramLinhas);
        pilha.Children.Add(UI.Texto(
            "Aloca blocos de VRAM em Vulkan, escreve um padrão em cada um e relê duas amostras. O total é o " +
            "que o driver aceitou; o pico e o uso vêm de /sys/class/drm (fonte independente da aplicação). " +
            "Encher até o driver recusar pode deixar o ambiente gráfico sem memória — nestas placas o limite " +
            "seguro é 2 GB com o desktop a correr.", "sub"));
        return UI.Cartao("Memória da GPU (Vulkan)", pilha);
    }

    private void ComecarVram()
    {
        if (_vramCancelar is not null)
        {
            Evento("o teste de VRAM já está a correr");
            return;
        }
        if (_vramGpus.Count == 0)
        {
            Evento("sem GPU Vulkan — não há VRAM para encher");
            return;
        }
        if (_sessao is { EmCurso: true } || _serie is { EmCurso: true })
        {
            Evento("há outro teste a correr — usa Parar antes de encher a VRAM");
            return;
        }

        int escolha = Math.Clamp(_vramGpu.SelectedIndex, 0, _vramGpus.Count - 1);
        int indice = _vramGpus[escolha].Indice;
        long limiteMb = _vramLimite.SelectedIndex switch
        {
            0 => 512, 1 => 1024, 3 => 4096, 4 => 8192, 5 => 0, _ => 2048
        };
        long blocoMb = _vramBloco.SelectedIndex switch { 0 => 64, 2 => 256, 3 => 1024, _ => 128 };

        _vramCancelar = new CancellationTokenSource();
        var ct = _vramCancelar.Token;

        _vramLinhas.Children.Clear();
        _vramResumo.Text = limiteMb == 0
            ? Textos.TF("a encher até o driver recusar, blocos de {0} MB", blocoMb)
            : Textos.TF("a encher até {0} MB, blocos de {1} MB", limiteMb, blocoMb);
        _vramEstado.Text = Textos.T("a alocar blocos…");

        _botoesVram[0].IsEnabled = false;
        _botoesVram[1].IsEnabled = true;
        foreach (var b in _botoes) b.IsEnabled = false;
        AtualizarBotoesParar();

        Evento(Textos.TF("VRAM · GPU {0} · limite {1} · bloco {2} MB", indice, limiteMb == 0 ? Textos.T("até falhar") : Textos.TF("{0} MB", limiteMb), blocoMb));

        _ = Task.Run(() =>
        {
            ResultadoVram? resultado = null;
            string? erro = null;
            try
            {
                resultado = StressVram.Correr(indice, limiteMb * 1024 * 1024, blocoMb * 1024 * 1024,
                    p => Dispatcher.UIThread.Post(() => _vramEstado.Text = p), ct);
            }
            catch (Exception ex) { erro = ex.Message; }

            Dispatcher.UIThread.Post(() => TerminarVram(resultado, erro));
        });
    }

    private void PararVram()
    {
        if (_vramCancelar is null) return;
        _vramCancelar.Cancel();
        _vramEstado.Text = Textos.T("a parar…");
        Evento("pedido de paragem do teste de VRAM");
    }

    private void TerminarVram(ResultadoVram? r, string? erro)
    {
        _vramCancelar?.Dispose();
        _vramCancelar = null;

        if (_sessao is not { EmCurso: true } && _serie is not { EmCurso: true })
            foreach (var b in _botoes) b.IsEnabled = true;
        _botoesVram[0].IsEnabled = _vramGpus.Count > 0;
        _botoesVram[1].IsEnabled = false;
        AtualizarBotoesParar();

        _vramLinhas.Children.Clear();

        if (r is null)
        {
            _vramResumo.Text = Textos.T("o teste de VRAM falhou: ") + (erro ?? "sem resultado");
            _vramEstado.Text = Textos.T("terminado com erro");
            Evento("VRAM falhou: " + erro);
            return;
        }

        _vramResumo.Text = r.TotalAlocado == 0 && r.Erro is not null
            ? Textos.TF("não alocou nada — {0}", r.Erro)
            : Textos.TF("{0} bloco(s) · {1} em {2} s", r.Blocos, Util.Bytes(r.TotalAlocado), $"{r.Segundos:0.0}") +
              (r.SysfsPico is long pico ? Textos.TF(" · pico em /sys {0}", Util.Bytes(pico)) : "");
        _vramEstado.Text = r.Parado ? "parado pelo utilizador" : "terminado";
        RelatorioCompleto.GuardarUltimoVram(r);     // entra no relatório completo

        foreach (var l in r.Linhas) _vramLinhas.Children.Add(UI.Texto("· " + l, "mono"));

        Evento(Textos.TF("VRAM · {0} · {1} · {2} erro(s) de verificação", r.Gpu, Util.Bytes(r.TotalAlocado), r.ErrosVerificacao));
    }

    private void AtualizarBotoesParar()
        => _parar.IsEnabled = _sessao is { EmCurso: true } || _serie is { EmCurso: true } || _vramCancelar is not null;

    // ------------------------------------------------------------------ ao vivo

    private Control CartaoAoVivo()
    {
        // blocos com os TextBlock que são actualizados ao vivo
        var grelha = UI.Grelha(4, 12,
            BlocoMetrica(_tempo, "tempo"),
            BlocoMetrica(_tempCpu, "CPU · k10temp"),
            BlocoMetrica(_tempGpu, "GPU · amdgpu"),
            BlocoMetrica(_potencia, "potência · RAPL + GPU"));

        var pilha = new StackPanel { Spacing = 12 };
        pilha.Children.Add(grelha);
        pilha.Children.Add(_barra);
        pilha.Children.Add(_fase);
        return UI.Cartao("Ao vivo", pilha);
    }

    private static Control BlocoMetrica(TextBlock valor, string legenda)
    {
        var p = new StackPanel { Spacing = 2 };
        p.Children.Add(valor);
        p.Children.Add(UI.Texto(legenda, "sub"));
        return p;
    }

    // ------------------------------------------------------------------ resultado

    private Control CartaoResultado()
    {
        var pilha = new StackPanel { Spacing = 8 };
        pilha.Children.Add(_resumo);
        pilha.Children.Add(_resultado);

        var acoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        acoes.Children.Add(_guardar);
        pilha.Children.Add(acoes);

        return UI.Cartao("Resultado", pilha);
    }

    private Control CartaoRegisto()
    {
        var pilha = new StackPanel { Spacing = 8 };
        pilha.Children.Add(_registo);
        return UI.Cartao("Registo", pilha);
    }

    // ------------------------------------------------------------------ execução

    private void Comecar(AlvoStress alvo, TimeSpan? duracao = null)
    {
        if (_sessao is { EmCurso: true })
        {
            Evento("já há um teste a correr — usa Parar primeiro");
            return;
        }

        if (_serie is { EmCurso: true })
        {
            Evento("a série de ciclos está a correr — pára-a antes de arrancar um teste simples");
            return;
        }

        if (_vramCancelar is not null)
        {
            Evento("o teste de VRAM está a correr — pára-o antes de arrancar outro teste");
            return;
        }

        if (duracao is TimeSpan fixa) _duracaoEscolhida = fixa;
        else _duracaoEscolhida = _duracao.SelectedIndex switch
        {
            0 => TimeSpan.FromMinutes(1),
            1 => TimeSpan.FromMinutes(2),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(10),
            4 => TimeSpan.FromMinutes(20),
            _ => TimeSpan.FromMinutes(30)
        };

        GuardaTermica.LimiteCpu = _limite.SelectedIndex switch { 0 => 85, 1 => 90, 2 => 95, _ => 100 };
        GuardaTermica.LimiteGpu = GuardaTermica.LimiteCpu;

        int threads = _threads.SelectedIndex switch
        {
            0 => Environment.ProcessorCount,
            1 => 1,
            _ => int.TryParse(_threads.SelectedItem?.ToString(), out int n) ? n : Environment.ProcessorCount
        };

        var carga = _carga.SelectedIndex switch
        {
            1 => CargaCpu.Inteiro,
            2 => CargaCpu.Flutuante,
            3 => CargaCpu.Memoria,
            _ => CargaCpu.Vector
        };

        double fracao = _memoria.SelectedIndex switch { 0 => 0.25, 2 => 0.75, _ => 0.5 };
        long tamanho = _tamanho.SelectedIndex switch
        {
            0 => 256L * 1024 * 1024,
            2 => 1024L * 1024 * 1024,
            3 => 2048L * 1024 * 1024,
            _ => 512L * 1024 * 1024
        };

        int indiceGpu = 0;

        _sessao?.Dispose();
        _sessao = new SessaoStress(alvo, _duracaoEscolhida, threads, carga, fracao,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache", "linux-explode", "stress"),
            tamanho, indiceGpu);

        _sessao.Progresso += p => Dispatcher.UIThread.Post(() => MostrarProgresso(p));
        _sessao.Terminou += r => Dispatcher.UIThread.Post(() => MostrarResultado(r));

        _inicio = DateTime.Now;
        _barra.Value = 0;
        _resultado.Children.Clear();
        _resumo.Text = Textos.TF("a correr: {0}", NomeAlvo(alvo));
        _fase.Text = Textos.T("a arrancar…");

        foreach (var b in _botoes) b.IsEnabled = false;
        _parar.IsEnabled = true;

        string duracaoTexto = _duracaoEscolhida.TotalSeconds < 60
            ? $"{_duracaoEscolhida.TotalSeconds:0} s"
            : $"{_duracaoEscolhida.TotalMinutes:0} min";

        string detalhe = alvo is AlvoStress.Cpu or AlvoStress.Tudo
            ? Textos.TF(" · {0} thread(s) · carga {1}", threads, Textos.T(NomeCarga(carga)))
            : "";

        Evento(Textos.TF("início · {0} · {1}{2} · limite {3} °C", NomeAlvo(alvo), duracaoTexto, detalhe, $"{GuardaTermica.LimiteCpu:0}"));

        try { _sessao.Iniciar(); }
        catch (Exception ex)
        {
            Evento(Textos.T("não arrancou: ") + ex.Message);
            TerminarInterface();
        }
    }

    private void Parar()
    {
        bool algo = false;

        if (_sessao is { EmCurso: true })
        {
            _sessao.Parar();
            algo = true;
        }

        if (_serie is { EmCurso: true })
        {
            _serie.Parar();
            Evento("pedido de paragem da série de ciclos");
            algo = true;
        }

        if (_vramCancelar is not null)
        {
            _vramCancelar.Cancel();
            Evento("pedido de paragem do teste de VRAM");
            algo = true;
        }

        if (algo) _fase.Text = Textos.T("a parar…");
    }

    private void MostrarProgresso(ProgressoStress p)
    {
        if (_sessao is null) return;

        _tempo.Text = $"{p.Segundos:0} s";
        _fase.Text = string.IsNullOrEmpty(p.Ritmo) ? Textos.T(p.Fase) : Textos.TF("{0} · {1}", Textos.T(p.Fase), p.Ritmo);

        var partes = p.Detalhe.Split(" · ");
        foreach (var parte in partes)
        {
            if (parte.EndsWith("°C") && parte.StartsWith("CPU")) _tempCpu.Text = parte.Replace("CPU ", "");
            else if (parte.EndsWith("°C") && parte.StartsWith("GPU")) _tempGpu.Text = parte.Replace("GPU ", "");
            else if (parte.EndsWith(" W")) _potencia.Text = parte;
        }

        _barra.Value = Math.Clamp(p.Segundos / Math.Max(1, _duracaoEscolhida.TotalSeconds), 0, 1);
    }

    private void MostrarResultado(ResultadoStress r)
    {
        _ultimo = r;
        RelatorioCompleto.GuardarUltimoStress(r);   // entra no relatório completo
        _resultado.Children.Clear();

        _resumo.Text = r.Resumo.StartsWith("concluído")
            ? r.Resumo
            : Textos.TF("{0} — {1}", Textos.T(r.Completo ? "concluído" : "parado"), Textos.T(r.Resumo));

        foreach (var linha in r.Linhas)
            _resultado.Children.Add(UI.Texto("· " + linha, "mono"));

        _resultado.Children.Add(UI.Texto(
            Textos.TF("· picos: CPU {0} °C · GPU {1} °C · energia {2} Wh ", $"{r.PicoCpuC:0}", $"{r.PicoGpuC:0}", $"{r.EnergiaKwh * 1000:0.0}") +
            Textos.TF("em {0} s", $"{r.Segundos:0}"), "mono"));

        _barra.Value = 1;
        _guardar.IsEnabled = true;

        Evento(Textos.TF("fim · {0} · {1} · pico CPU {2} °C / GPU {3} °C", NomeAlvo(r.Alvo), Textos.T(r.Resumo), $"{r.PicoCpuC:0}", $"{r.PicoGpuC:0}"));
        _fase.Text = Textos.T("terminado — podes repetir");
        TerminarInterface();
    }

    private void TerminarInterface()
    {
        foreach (var b in _botoes) b.IsEnabled = true;
        AtualizarBotoesParar();
        _fase.Text = _ultimo is null ? "pronto para começar" : "terminado — podes repetir";
    }

    // ------------------------------------------------------------------ registo e relatório

    private void Evento(string texto)
    {
        _eventos.Insert(0, $"{DateTime.Now:HH:mm:ss}  {texto}");
        if (_eventos.Count > 12) _eventos.RemoveAt(_eventos.Count - 1);
        _registo.Text = string.Join("\n", _eventos);
    }

    private void GuardarRelatorio()
    {
        if (_ultimo is null) return;

        try
        {
            string pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "linux-explode-relatorios");
            Directory.CreateDirectory(pasta);
            string ficheiro = Path.Combine(pasta, $"stress-{DateTime.Now:yyyyMMdd-HHmmss}.json");

            var documento = new
            {
                aplicacao = "LINUX EXPLODE",
                versao = Relatorio.Versao,
                quando = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                alvo = _ultimo.Alvo.ToString(),
                completo = _ultimo.Completo,
                resumo = _ultimo.Resumo,
                duracaoSegundos = Math.Round(_ultimo.Segundos, 1),
                picoCpuC = Math.Round(_ultimo.PicoCpuC, 1),
                picoGpuC = Math.Round(_ultimo.PicoGpuC, 1),
                energiaWh = Math.Round(_ultimo.EnergiaKwh * 1000, 1),
                limiteC = GuardaTermica.LimiteCpu,
                linhas = _ultimo.Linhas
            };

            File.WriteAllText(ficheiro,
                JsonSerializer.Serialize(documento, new JsonSerializerOptions { WriteIndented = true }));

            Evento(Textos.T("relatório gravado em ") + ficheiro);
        }
        catch (Exception ex)
        {
            Evento(Textos.T("não consegui gravar o relatório: ") + ex.Message);
        }
    }

    private static string NomeAlvo(AlvoStress alvo) => alvo switch
    {
        AlvoStress.Cpu => "CPU",
        AlvoStress.Ram => "RAM",
        AlvoStress.Gpu => "GPU",
        AlvoStress.Disco => "disco",
        _ => "tudo (burn-in)"
    };

    private static string NomeCarga(CargaCpu carga) => carga switch
    {
        CargaCpu.Inteiro => "inteiro",
        CargaCpu.Flutuante => "vírgula flutuante",
        CargaCpu.Memoria => "memória",
        _ => "FMA vetorial"
    };

    /// <summary>Sair da página pára o stress: não se deixa a máquina a arder noutra aba.</summary>
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_sessao is { EmCurso: true })
        {
            _sessao.Parar();
            Evento("página fechada — teste parado");
        }

        if (_serie is { EmCurso: true })
        {
            _serie.Parar();
            Evento("página fechada — série de ciclos parada");
        }

        if (_vramCancelar is not null)
        {
            _vramCancelar.Cancel();
            Evento("página fechada — teste de VRAM parado");
        }
    }
}

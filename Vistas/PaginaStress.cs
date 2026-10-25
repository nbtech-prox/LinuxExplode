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
    private readonly TextBlock _fase = new() { Text = "pronto para começar", Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _barra = new() { Minimum = 0, Maximum = 1, Value = 0 };

    // resultado e registo
    private readonly StackPanel _resultado = new() { Spacing = 2 };
    private readonly TextBlock _resumo = new() { Text = "sem resultados", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _registo = new() { Text = "", Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };
    private readonly List<string> _eventos = new();
    private readonly Button _parar;
    private readonly Button _guardar;
    private readonly List<Button> _botoes = new();

    private SessaoStress? _sessao;
    private ResultadoStress? _ultimo;
    private DateTime _inicio;
    private TimeSpan _duracaoEscolhida = TimeSpan.FromMinutes(5);

    public PaginaStress()
    {
        _parar = UI.Botao("Parar", false, Parar);
        _parar.IsEnabled = false;
        _guardar = UI.Botao("Guardar relatório", false, GuardarRelatorio);
        _guardar.IsEnabled = false;

        Content = UI.Pagina(
            UI.Cabecalho("STRESS", "Aquece CPU, RAM, GPU e disco — com guarda térmica e valores medidos", _parar),
            _corpo);

        _corpo.Children.Add(CartaoConfiguracao());
        _corpo.Children.Add(CartaoTestes());
        _corpo.Children.Add(CartaoAoVivo());
        _corpo.Children.Add(CartaoResultado());
        _corpo.Children.Add(CartaoRegisto());

        Evento("pronto · limites de fábrica: CPU/GPU 95 °C");

        Loaded += (_, _) =>
        {
            // verificação automática: LINUXEXPLODE_STRESS=cpu|ram|gpu|disco|tudo (+ _SEG=N)
            var auto = Environment.GetEnvironmentVariable("LINUXEXPLODE_STRESS");
            if (string.IsNullOrEmpty(auto)) return;

            int seg = int.TryParse(Environment.GetEnvironmentVariable("LINUXEXPLODE_STRESS_SEG"), out int v) ? v : 10;
            var alvo = auto.ToLowerInvariant() switch
            {
                "ram" => AlvoStress.Ram,
                "gpu" => AlvoStress.Gpu,
                "disco" => AlvoStress.Disco,
                "tudo" => AlvoStress.Tudo,
                _ => AlvoStress.Cpu
            };

            Comecar(alvo, TimeSpan.FromSeconds(Math.Clamp(seg, 5, 3600)));
        };
    }

    // ------------------------------------------------------------------ configuração

    private Control CartaoConfiguracao()
    {
        _duracao.Items.Add("1 minuto");
        _duracao.Items.Add("2 minutos");
        _duracao.Items.Add("5 minutos");
        _duracao.Items.Add("10 minutos");
        _duracao.Items.Add("20 minutos");
        _duracao.Items.Add("30 minutos");
        _duracao.SelectedIndex = 2;

        int nucleos = Environment.ProcessorCount;
        _threads.Items.Add($"todos ({nucleos})");
        _threads.Items.Add("1");
        foreach (int n in new[] { 2, 4, 8 }.Where(n => n < nucleos)) _threads.Items.Add($"{n}");
        _threads.SelectedIndex = 0;

        _carga.Items.Add("FMA vetorial (SIMD)");
        _carga.Items.Add("inteiro");
        _carga.Items.Add("vírgula flutuante");
        _carga.Items.Add("memória");
        _carga.SelectedIndex = 0;

        _memoria.Items.Add("25 % da RAM");
        _memoria.Items.Add("50 % da RAM");
        _memoria.Items.Add("75 % da RAM");
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
        p.Children.Add(UI.Texto(rotulo.ToUpperInvariant(), "rotulo"));
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
        _resumo.Text = $"a correr: {NomeAlvo(alvo)}";
        _fase.Text = "a arrancar…";

        foreach (var b in _botoes) b.IsEnabled = false;
        _parar.IsEnabled = true;

        string duracaoTexto = _duracaoEscolhida.TotalSeconds < 60
            ? $"{_duracaoEscolhida.TotalSeconds:0} s"
            : $"{_duracaoEscolhida.TotalMinutes:0} min";

        string detalhe = alvo is AlvoStress.Cpu or AlvoStress.Tudo
            ? $" · {threads} thread(s) · carga {NomeCarga(carga)}"
            : "";

        Evento($"início · {NomeAlvo(alvo)} · {duracaoTexto}{detalhe} · limite {GuardaTermica.LimiteCpu:0} °C");

        try { _sessao.Iniciar(); }
        catch (Exception ex)
        {
            Evento("não arrancou: " + ex.Message);
            TerminarInterface();
        }
    }

    private void Parar()
    {
        if (_sessao is null || !_sessao.EmCurso) return;
        Evento("pedido de paragem do utilizador");
        _sessao.Parar();
        _fase.Text = "a parar…";
    }

    private void MostrarProgresso(ProgressoStress p)
    {
        if (_sessao is null) return;

        _tempo.Text = $"{p.Segundos:0} s";
        _fase.Text = string.IsNullOrEmpty(p.Ritmo) ? p.Fase : $"{p.Fase} · {p.Ritmo}";

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
        _resultado.Children.Clear();

        _resumo.Text = r.Resumo.StartsWith("concluído")
            ? r.Resumo
            : $"{(r.Completo ? "concluído" : "parado")} — {r.Resumo}";

        foreach (var linha in r.Linhas)
            _resultado.Children.Add(UI.Texto("· " + linha, "mono"));

        _resultado.Children.Add(UI.Texto(
            $"· picos: CPU {r.PicoCpuC:0} °C · GPU {r.PicoGpuC:0} °C · energia {r.EnergiaKwh * 1000:0.0} Wh " +
            $"em {r.Segundos:0} s", "mono"));

        _barra.Value = 1;
        _guardar.IsEnabled = true;

        Evento($"fim · {NomeAlvo(r.Alvo)} · {r.Resumo} · pico CPU {r.PicoCpuC:0} °C / GPU {r.PicoGpuC:0} °C");
        _fase.Text = "terminado — podes repetir";
        TerminarInterface();
    }

    private void TerminarInterface()
    {
        foreach (var b in _botoes) b.IsEnabled = true;
        _parar.IsEnabled = false;
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

            Evento("relatório gravado em " + ficheiro);
        }
        catch (Exception ex)
        {
            Evento("não consegui gravar o relatório: " + ex.Message);
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
    }
}

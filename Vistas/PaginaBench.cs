using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaBench : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };

    // CPU bench
    private readonly TextBlock _estado = new() { Text = Textos.T("pronto"), Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _pontuacao = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _historico = new() { Text = Textos.T("sem resultados"), Classes = { "mono" } };
    // Um resultado por tipo de teste — não uma lista: o cartão mostra o último resultado de cada
    // modalidade e substitui-o quando aparece um diferente.
    private readonly Dictionary<string, (string Chave, string Linha)> _resultados = new();

    // RAM bench
    private readonly TextBlock _ramEstado = new() { Text = Textos.T("pronto"), Classes = { "sub" } };
    private readonly StackPanel _ramResultado = new() { Spacing = 0 };

    // Disco bench
    private readonly TextBlock _discoEstado = new() { Text = Textos.T("pronto"), Classes = { "sub" } };
    private readonly StackPanel _discoResultado = new() { Spacing = 0 };

    // GPU bench
    private readonly ComboBox _gpuLista = new() { Width = 560, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _gpuEstado = new() { Text = Textos.T("pronto"), Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _gpuPontuacao = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _gpuComparacao = new() { Text = "", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _comparacao = new() { Text = "", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _historicoEstado = new() { Text = "", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly List<InfoGpu> _gpus = new();

    // Histórico em SQLite (0.20.13): pesquisa, período e gráfico da série escolhida
    private readonly TextBox _histProcura = new() { Watermark = "procurar tipo, alvo ou detalhe…", Width = 320 };
    private readonly ComboBox _histPeriodo = new() { Width = 150 };
    private readonly StackPanel _histLista = new() { Spacing = 2 };
    private readonly TextBlock _histResumo = new() { Text = "", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _histGrafico = new() { Spacing = 4 };

    // Latência de memória (0.20.7)
    private readonly CurvaLatencia _curva = new();
    private readonly TextBlock _latEstado = new() { Text = Textos.T("pronto"), Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _latResumo = new() { Text = Textos.T("sem medições"), Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };

    // Tarefa do mundo real (0.20.7)
    private readonly TextBlock _realEstado = new() { Text = Textos.T("pronto"), Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _realResultado = new() { Spacing = 2 };

    // Ranking local (0.20.7)
    private readonly StackPanel _rankingLista = new() { Spacing = 4 };
    private readonly TextBlock _rankingEstado = new() { Text = "", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };

    public PaginaBench()
    {
        Content = UI.Pagina(
            UI.Cabecalho("Benchmark", "Path tracing de CPU e GPU · banda de memória · disco"),
            _corpo);

        // gancho de captura: LINUXEXPLODE_SECAO=latencia|real|ranking|cpu|ram|gpu|disco deixa só esse
        // cartão na página, para o poder fotografar no topo da janela (não há scroll fiável).
        var secao = (Environment.GetEnvironmentVariable("LINUXEXPLODE_SECAO") ?? "").ToLowerInvariant();
        bool Quer(string nome) => secao.Length == 0 || secao == nome;

        if (Quer("gpu")) _corpo.Children.Add(CartaoGpu());
        if (Quer("fps")) _corpo.Children.Add(new CartaoFps());
        if (Quer("cpu")) _corpo.Children.Add(CartaoCpu());
        if (Quer("ram")) _corpo.Children.Add(CartaoRam());
        if (Quer("latencia") || Quer("latência")) _corpo.Children.Add(CartaoLatencia());
        if (Quer("real")) _corpo.Children.Add(CartaoReal());
        if (Quer("disco")) _corpo.Children.Add(CartaoDisco());
        if (Quer("ranking")) _corpo.Children.Add(CartaoRanking());
        if (Quer("historico") || Quer("histórico")) _corpo.Children.Add(CartaoHistorico());

        Loaded += (_, _) =>
        {
            CarregarGpus();
            ActualizarRanking();

            // modo de teste automático: LINUXEXPLODE_AUTOTEST=1 (compute) ou "rt" (ray tracing)
            // abre o pop do bench, que é o caminho real do botão Testar GPU
            var auto = Environment.GetEnvironmentVariable("LINUXEXPLODE_AUTOTEST");
            bool tratadoNoCartao = auto is "latmem" or "real";   // cada cartão tem o seu próprio gancho
            if (auto == "cpu")
                AbrirPrevia(ModalidadePrevia.Cpu, int.TryParse(
                    Environment.GetEnvironmentVariable("LINUXEXPLODE_THREADS"), out int t) && t > 0 ? t : 0);
            else if (!tratadoNoCartao && !string.IsNullOrEmpty(auto) && _gpus.Count > 0)
                AbrirPrevia(auto == "rt" ? ModalidadePrevia.Rt : ModalidadePrevia.Gpu);
        };
    }

    // ------------------------------------------------------------------ GPU

    private void CarregarGpus()
    {
        if (_gpus.Count > 0) return;
        var lista = BenchGpu.Dispositivos();
        _gpus.AddRange(lista);

        if (_gpus.Count == 0)
        {
            _gpuEstado.Text = Textos.T("nenhuma GPU Vulkan com fila de computação — verifica o driver (vulkan-radeon / mesa)");
            return;
        }

        foreach (var g in _gpus)
            _gpuLista.Items.Add($"{g.Nome}  ({Textos.T(g.Tipo)})");
        _gpuLista.SelectedIndex = 0;
        _gpuEstado.Text = Textos.TF("{0} GPU(s) Vulkan detetada(s)", _gpus.Count);
    }

    private Control CartaoGpu()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            Textos.TF("O mesmo path tracer da CPU, agora em compute shader Vulkan: {0} amostras por pixel, " +
                      "repetido até {1} s para a nota ser estável — a resolução escolhe-se no pop-up, de 320×180 até 4K. " +
                      "O modo ray tracing usa VK_KHR_ray_query com 64 esferas em BVH — a nota não é comparável com a de compute.",
                      BenchGpu.AmostrasPadrao, $"{BenchGpu.AlvoSegundos:0.0}"), "sub"));
        conteudo.Children.Add(_gpuLista);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Testar GPU", true, () => AbrirPrevia(ModalidadePrevia.Gpu)));
        botoes.Children.Add(UI.Botao("Testar ray tracing", false, () => AbrirPrevia(ModalidadePrevia.Rt)));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_gpuEstado);

        var linhaNota = new StackPanel { Spacing = 2 };
        linhaNota.Children.Add(UI.Texto("NOTA GPU", "rotulo"));
        linhaNota.Children.Add(_gpuPontuacao);
        linhaNota.Children.Add(_gpuComparacao);
        conteudo.Children.Add(linhaNota);

        return UI.Cartao("GPU · Benchmark (Vulkan)", conteudo);
    }

    /// <summary>
    /// Abre o pop do bench: a cena a animar + a medição, com escolha de resolução nos testes de GPU.
    /// A nota e o detalhe voltam para o cartão à medida que são medidos.
    /// </summary>
    private void AbrirPrevia(ModalidadePrevia modalidade, int threads = 0)
    {
        // 0 threads = "todos os cores" (a prévia faz o mesmo); normalizar aqui garante que a linha
        // do histórico diz o número real de threads usadas.
        if (modalidade == ModalidadePrevia.Cpu && threads <= 0) threads = Environment.ProcessorCount;

        int indice = 0;
        if (modalidade != ModalidadePrevia.Cpu && _gpuLista.SelectedIndex >= 0 && _gpuLista.SelectedIndex < _gpus.Count)
            indice = _gpus[_gpuLista.SelectedIndex].Indice;

        string ultimaNota = "—";

        var janela = new JanelaPrevia(modalidade, indice, threads, r =>
        {
            ultimaNota = r.Nota;
            if (modalidade == ModalidadePrevia.Cpu)
            {
                var tipo = threads == 1 ? "single" : "multi";

                // O resultado fica fixo: só substitui o anterior quando é diferente.
                var chave = ChaveDoResultado(r.Nota);
                if (_resultados.TryGetValue(tipo, out var guardado) && guardado.Chave == chave) return;

                _resultados[tipo] = (chave, $"{r.Nota} pts · {threads}T · {DateTime.Now:HH:mm}");

                string alvoCpu = $"{threads} threads";
                double notaCpu = Numero(r.Nota);
                if (r.CorridaConcluida)
                    Historico.Registar("cpu", alvoCpu, notaCpu, r.Detalhe);

                _comparacao.Text = Historico.Comparacao("cpu", alvoCpu, notaCpu) ?? "primeira corrida guardada";
                _pontuacao.Text = r.Nota;
                _estado.Text = r.Detalhe;
                MostrarResultados();
                ActualizarRanking();
            }
            else
            {
                _gpuPontuacao.Text = r.Nota;
                _gpuEstado.Text = (modalidade == ModalidadePrevia.Rt ? "ray tracing · " : "compute · ") + r.Detalhe;

                string tipo = modalidade == ModalidadePrevia.Rt ? "rt" : "gpu";
                string alvo = _gpus.Count > 0 && _gpuLista.SelectedIndex >= 0 ? _gpus[_gpuLista.SelectedIndex].Nome : "GPU";
                double nota = Numero(r.Nota);

                _gpuComparacao.Text = Historico.Comparacao(tipo, alvo, nota) ?? "primeira corrida deste alvo";
                Historico.Registar(tipo, alvo, nota, r.Detalhe);
                ActualizarRanking();
            }
        });

        if (TopLevel.GetTopLevel(this) is Window dono) janela.Show(dono);
        else janela.Show();
    }

    /// <summary>
    /// Um resultado por modalidade (multi-core / single-core), lado a lado no cartão. Não acumula:
    /// cada linha é o último resultado daquele tipo, substituído quando aparece um diferente.
    /// </summary>
    private void MostrarResultados()
    {
        var linhas = new List<string>();

        if (_resultados.TryGetValue("multi", out var multi))
            linhas.Add($"{Textos.T("Multi-core"),-12} {multi.Linha}");
        if (_resultados.TryGetValue("single", out var single))
            linhas.Add($"{Textos.T("Single-core"),-12} {single.Linha}");

        _historico.Text = linhas.Count == 0 ? "sem resultados" : string.Join("\n", linhas);
    }

    /// <summary>
    /// Chave de comparação de um resultado: dois algarismos significativos. Duas corridas do mesmo
    /// teste dão 5 603 e 5 598 — para o utilizador é o mesmo resultado, e o histórico não pode
    /// encher-se de linhas repetidas.
    /// </summary>
    private static string ChaveDoResultado(string nota)
    {
        var digitosTexto = new string(nota.Where(char.IsDigit).ToArray());
        if (!long.TryParse(digitosTexto, out long valor) || valor <= 0) return nota;

        int casas = (int)Math.Floor(Math.Log10(valor)) + 1;
        long passo = (long)Math.Pow(10, Math.Max(0, casas - 2));
        return ((valor + passo / 2) / passo * passo).ToString();
    }

    // ------------------------------------------------------------------ CPU

    private Control CartaoCpu()
    {
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Multi-core", true, () => AbrirPrevia(ModalidadePrevia.Cpu, Environment.ProcessorCount)));
        botoes.Children.Add(UI.Botao("Single-core", false, () => AbrirPrevia(ModalidadePrevia.Cpu, 1)));

        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "O mesmo renderizador do benchmark da versão Windows: path tracer determinístico, 960×540, 1024 amostras por pixel. " +
            "A nota é amostras por segundo ÷ 10.000 — comparável entre máquinas.", "sub"));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_estado);

        var linhaNota = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var pilha = new StackPanel { Spacing = 2 };
        pilha.Children.Add(UI.Texto("NOTA", "rotulo"));
        pilha.Children.Add(_pontuacao);
        pilha.Children.Add(_comparacao);
        linhaNota.Children.Add(pilha);
        var hist = new StackPanel { Spacing = 4, Margin = new Avalonia.Thickness(30, 0, 0, 0) };
        hist.Children.Add(UI.Texto("HISTÓRICO", "rotulo"));
        hist.Children.Add(_historico);
        Grid.SetColumn(hist, 1);
        linhaNota.Children.Add(hist);
        conteudo.Children.Add(linhaNota);

        return UI.Cartao("CPU · Benchmark", conteudo);
    }

    // ------------------------------------------------------------------ RAM

    private Control CartaoRam()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto("Banda de escrita, leitura, cópia e latência real de acesso à RAM.", "sub"));
        conteudo.Children.Add(UI.Botao("Testar memória", true, async () =>
        {
            _ramEstado.Text = Textos.T("a testar…");
            _ramResultado.Children.Clear();
            try
            {
                var r = await Task.Run(() => BenchRam.Correr(t => Dispatcher.UIThread.Post(() => _ramEstado.Text = t)));
                _ramResultado.Children.Add(UI.Linha("Escrita sequencial", $"{r.EscritaMBs:N0} MB/s"));
                _ramResultado.Children.Add(UI.Linha("Leitura sequencial", $"{r.LeituraMBs:N0} MB/s"));
                _ramResultado.Children.Add(UI.Linha("Cópia", $"{r.CopiaMBs:N0} MB/s"));
                _ramResultado.Children.Add(UI.Linha("Latência", $"{r.LatenciaNs:N1} ns", false));
                _ramEstado.Text = Textos.TF("bloco de {0}", Util.Bytes(r.Tamanho));

                string detalheRam = Textos.TF("escrita {0} MB/s · leitura {1} MB/s · ", $"{r.EscritaMBs:N0}", $"{r.LeituraMBs:N0}") +
                                    Textos.TF("cópia {0} MB/s · latência {1} ns", $"{r.CopiaMBs:N0}", $"{r.LatenciaNs:N1}");
                Historico.Registar("ram", Util.Bytes(r.Tamanho), r.EscritaMBs, detalheRam);

                _ramResultado.Children.Add(UI.Texto(
                    "· " + (Historico.Comparacao("ram", Util.Bytes(r.Tamanho), r.EscritaMBs) ?? "primeira corrida"), "sub"));
            }
            catch (Exception ex) { _ramEstado.Text = Textos.T("falhou: ") + ex.Message; }
        }));
        conteudo.Children.Add(_ramEstado);
        conteudo.Children.Add(_ramResultado);
        return UI.Cartao("Memória RAM", conteudo);
    }

    // ------------------------------------------------------------------ DISCO

    /// <summary>
    /// Latência por tamanho de bloco (item 11): a curva mostra os patamares das caches e o salto para a
    /// RAM. O percurso é aleatório de propósito — um acesso sequencial media a banda do pré-buscador,
    /// não a latência.
    /// </summary>
    private Control CartaoLatencia()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "Pointer chasing sobre blocos de 64 B (uma linha de cache), de 4 KB a 256 MB: nanossegundos por " +
            "acesso. Cada patamar é um nível — L1, L2, L3 — e o degrau final é a RAM. " +
            "O percurso é uma permutação aleatória com semente fixa, por isso duas corridas visitam os mesmos endereços.", "sub"));

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Medir latência", true, () => _ = MedirLatencia()));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_latEstado);
        conteudo.Children.Add(_curva);
        conteudo.Children.Add(_latResumo);

        // hook de verificação sem clicar: LINUXEXPLODE_AUTOTEST=latmem
        if (Environment.GetEnvironmentVariable("LINUXEXPLODE_AUTOTEST") == "latmem")
            Loaded += (_, _) => _ = MedirLatencia();

        return UI.Cartao("Memória · latência por tamanho de bloco", conteudo);
    }

    private async Task MedirLatencia()
    {
        _latEstado.Text = Textos.T("a medir 4 KB → 256 MB (alguns segundos)…");
        _curva.Definir(Array.Empty<LatenciaBloco>());

        try
        {
            var pontos = await Task.Run(() => BenchMemoria.Curva(
                t => Dispatcher.UIThread.Post(() => _latEstado.Text = t)));

            _curva.Definir(pontos);
            _latResumo.Text = $"{BenchMemoria.Resumo(pontos)}\n" +
                              string.Join(" · ", pontos.Select(p => $"{p.Tamanho} {p.Ns:0.0} ns"));
            _latEstado.Text = Textos.TF("{0} pontos medidos", pontos.Count);
        }
        catch (Exception ex)
        {
            _latEstado.Text = Textos.TF("falhou: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Carga "do mundo real" (item 12): descomprimir 48 MB verificando o SHA-256 e compilar 1 500 funções
    /// C. Sem rede e sem ficheiros temporários; se não houver compilador, isso é dito em vez de escondido.
    /// </summary>
    private Control CartaoReal()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            Textos.TF("Duas tarefas fixas e repetíveis: descomprimir {0} MB " +
                      "(o resultado é conferido por SHA-256) e compilar um ficheiro C com " +
                      "{1} funções, com o compilador do sistema. " +
                      "Não usa a rede nem o disco além do próprio trabalho — é o que um utilizador faz a compilar ou a instalar.",
                      TarefaReal.BytesPayload / (1024 * 1024), TarefaReal.FuncoesC), "sub"));

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Correr tarefa real", true, () => _ = CorrerTarefaReal()));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_realEstado);
        conteudo.Children.Add(_realResultado);

        if (Environment.GetEnvironmentVariable("LINUXEXPLODE_AUTOTEST") == "real")
            Loaded += (_, _) => _ = CorrerTarefaReal();

        return UI.Cartao("Tarefa do mundo real · descomprimir + compilar", conteudo);
    }

    private async Task CorrerTarefaReal()
    {
        _realEstado.Text = Textos.T("a correr (descomprimir + compilar)…");
        _realResultado.Children.Clear();

        try
        {
            var r = await Task.Run(() => TarefaReal.Correr(
                t => Dispatcher.UIThread.Post(() => _realEstado.Text = t)));

            _realResultado.Children.Add(UI.Texto(Textos.TF("descompressão   {0} MB/s", $"{r.DescompressaoMBs:N0}"), "mono"));
            _realResultado.Children.Add(UI.Texto(
                r.CompilacaoSegundos is double s
                    ? Textos.TF("compilação      {0} kB/s   ({1} s, {2})", $"{r.CompilacaoKBs:N0}", $"{s:0.00}", r.Compilador)
                    : Textos.TF("compilação      —   {0}", r.CompilacaoMotivo), "mono"));
            _realResultado.Children.Add(UI.Texto(Textos.TF("total           {0} s", $"{r.TotalSegundos:0.00}"), "mono"));

            _realEstado.Text = r.CompilacaoSegundos is null
                ? Textos.TF("descompressão verificada (SHA-256 {0}…); a compilação não correu", r.Sha256[..12])
                : Textos.TF("concluído · SHA-256 {0}… igual ao original", r.Sha256[..12]);

            Historico.Registar("real", $"gzip {TarefaReal.BytesPayload / (1024 * 1024)} MB", r.DescompressaoMBs, "MB/s");
            if (r.CompilacaoKBs is double kbs)
                Historico.Registar("real", $"compilar {TarefaReal.FuncoesC} funções", kbs, "kB/s de fonte");

            ActualizarRanking();
        }
        catch (Exception ex)
        {
            _realEstado.Text = Textos.TF("falhou: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Ranking local (item 9): por alvo, a mediana das últimas 3 corridas e a variação entre elas. Quando
    /// a variação passa dos 3 %, o cartão avisa que a nota não é comparável — máquina instável, não teste mau.
    /// </summary>
    private Control CartaoRanking()
    {
        var conteudo = new StackPanel { Spacing = 10 };
        conteudo.Children.Add(UI.Texto(
            Textos.TF("Mediana das últimas {0} corridas de cada alvo, com a variação entre elas. " +
                      "Acima de {1} % de variação a máquina não estava estável e o número vale pouco — " +
                      "o aviso aparece na linha. Para a mediana ser de 3, corre `--cli bench multi --repeticoes 3`.",
                      Historico.CorridasRanking, $"{RankingLocal.ToleranciaPct:0}"), "sub"));

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Actualizar", false, ActualizarRanking));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_rankingEstado);
        conteudo.Children.Add(_rankingLista);

        return UI.Cartao("Ranking local · mediana de 3 corridas", conteudo);
    }

    /// <summary>Relê o histórico e redesenha o ranking (chamado ao entrar na página e depois de cada teste).</summary>
    private void ActualizarRanking()
    {
        _rankingLista.Children.Clear();

        var linhas = Historico.Rankings();
        if (linhas.Count == 0)
        {
            _rankingEstado.Text = Textos.T("sem corridas guardadas — corre um teste para começar o ranking");
            return;
        }

        _rankingEstado.Text = Textos.TF("{0} alvo(s) no histórico", linhas.Count);

        foreach (var l in linhas)
        {
            var bloco = new StackPanel { Spacing = 1 };
            bloco.Children.Add(UI.Texto($"{l.Tipo.ToUpperInvariant()} · {l.Alvo}", "rotulo"));
            bloco.Children.Add(UI.Texto(l.Linha, "mono"));

            if (l.Aviso is not null)
                bloco.Children.Add(new TextBlock
                {
                    Text = Textos.T("! ") + l.Aviso,
                    Classes = { "sub" },
                    Foreground = UI.Cor("Mau"),
                    TextWrapping = TextWrapping.Wrap
                });

            _rankingLista.Children.Add(bloco);
        }
    }

    private Control CartaoDisco()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "Escrita e leitura sequencial + 4K aleatório num ficheiro temporário de 256 MB, " +
            "com O_DIRECT (sem passar pelo cache do sistema). O ficheiro é apagado no fim.", "sub"));
        conteudo.Children.Add(UI.Botao("Testar disco", true, async () =>
        {
            _discoEstado.Text = Textos.T("a testar…");
            _discoResultado.Children.Clear();
            try
            {
                var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "linuxexplode");
                var r = await Task.Run(() => BenchDisco.Correr(pasta, 256, t => Dispatcher.UIThread.Post(() => _discoEstado.Text = t)));
                _discoResultado.Children.Add(UI.Linha("Sequencial · escrita", $"{r.SeqEscritaMBs:N0} MB/s"));
                _discoResultado.Children.Add(UI.Linha("Sequencial · leitura", $"{r.SeqLeituraMBs:N0} MB/s"));
                _discoResultado.Children.Add(UI.Linha("4K aleatório · leitura", $"{r.RandLeituraMBs:N1} MB/s"));
                _discoResultado.Children.Add(UI.Linha("4K aleatório · escrita", $"{r.RandEscritaMBs:N1} MB/s", false));
                _discoEstado.Text = Textos.TF("ficheiro de {0} em {1}", Util.Bytes(r.Ficheiro), pasta);

                string detalheDisco = Textos.TF("seq escrita {0} · seq leitura {1} · ", $"{r.SeqEscritaMBs:N0}", $"{r.SeqLeituraMBs:N0}") +
                                      Textos.TF("4K leitura {0} · 4K escrita {1} MB/s", $"{r.RandLeituraMBs:N1}", $"{r.RandEscritaMBs:N1}");
                Historico.Registar("disco", "sequencial (escrita)", r.SeqEscritaMBs, detalheDisco);

                _discoResultado.Children.Add(UI.Texto(
                    "· " + (Historico.Comparacao("disco", "sequencial (escrita)", r.SeqEscritaMBs) ?? "primeira corrida"), "sub"));
            }
            catch (Exception ex) { _discoEstado.Text = Textos.T("falhou: ") + ex.Message; }
        }));
        conteudo.Children.Add(_discoEstado);
        conteudo.Children.Add(_discoResultado);
        return UI.Cartao("Disco", conteudo);
    }

    // ------------------------------------------------------------------ histórico

    private Control CartaoHistorico()
    {
        var conteudo = new StackPanel { Spacing = 10 };

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Exportar CSV", false, () => Exportar("csv")));
        botoes.Children.Add(UI.Botao("Exportar JSON", false, () => Exportar("json")));
        botoes.Children.Add(UI.Botao("Importar…", false, Importar));

        conteudo.Children.Add(UI.Texto(
            "As notas ficam guardadas em SQLite (~/.local/share/linux-explode/historico.db), com a data e a " +
            "versão. Cada cartão mostra o melhor resultado, a mediana e a variação da última corrida. " +
            "Pesquisa por tipo, alvo ou detalhe e filtra por período; exporta para CSV/JSON para comparar " +
            "máquinas, ou importa o ficheiro de outra máquina. O resultados.json antigo é migrado sozinho " +
            "no primeiro arranque e guardado como resultados.json.migrado.", "sub"));
        conteudo.Children.Add(botoes);

        _histPeriodo.Items.Add(Textos.T("tudo"));
        _histPeriodo.Items.Add(Textos.T("7 dias"));
        _histPeriodo.Items.Add(Textos.T("30 dias"));
        _histPeriodo.Items.Add(Textos.T("90 dias"));
        _histPeriodo.SelectedIndex = 0;
        _histPeriodo.SelectionChanged += (_, _) => ActualizarHistorico();

        _histProcura.TextChanged += (_, _) => ActualizarHistorico();

        var filtros = new WrapPanel { Orientation = Orientation.Horizontal };
        var campoProcura = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 12, 8) };
        campoProcura.Children.Add(UI.Texto("PROCURAR", "rotulo"));
        campoProcura.Children.Add(_histProcura);
        var campoPeriodo = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 12, 8) };
        campoPeriodo.Children.Add(UI.Texto("PERÍODO", "rotulo"));
        campoPeriodo.Children.Add(_histPeriodo);
        filtros.Children.Add(campoProcura);
        filtros.Children.Add(campoPeriodo);
        conteudo.Children.Add(filtros);

        conteudo.Children.Add(UI.Texto(Historico.Caminho, "mono"));
        conteudo.Children.Add(_histResumo);
        conteudo.Children.Add(_histGrafico);
        conteudo.Children.Add(_histLista);
        conteudo.Children.Add(_historicoEstado);

        ActualizarHistorico();

        return UI.Cartao("Histórico de resultados", conteudo);
    }

    /// <summary>
    /// Relê o histórico com o filtro actual. A pesquisa é feita no SQLite (tipo, alvo ou detalhe) e o
    /// gráfico mostra a série da combinação tipo/alvo mais recente que passou o filtro — com os valores
    /// reais, sem normalizar notas de tipos diferentes.
    /// </summary>
    private void ActualizarHistorico()
    {
        DateTime? desde = _histPeriodo.SelectedIndex switch
        {
            1 => DateTime.Now.AddDays(-7),
            2 => DateTime.Now.AddDays(-30),
            3 => DateTime.Now.AddDays(-90),
            _ => null
        };

        var lista = Historico.Pesquisar(_histProcura.Text, desde, limite: 80);
        int total = Historico.Todos().Count;

        _histResumo.Text = Textos.TF("{0} de {1} resultado(s) no histórico", lista.Count, total) +
                           (desde is not null ? Textos.TF(" · desde {0}", $"{desde:dd/MM/yyyy}") : "");

        _histGrafico.Children.Clear();
        _histLista.Children.Clear();

        if (lista.Count == 0)
        {
            _histLista.Children.Add(UI.Texto(total == 0
                ? "ainda sem resultados guardados — corre um teste"
                : "nada encontrado com esse filtro", "sub"));
            return;
        }

        // gráfico: a série mais recente com pelo menos 2 corridas (uma corrida não é uma linha)
        var serie = lista
            .GroupBy(r => (r.Tipo, r.Alvo))
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Max(x => x.Quando))
            .FirstOrDefault()?
            .OrderBy(x => x.Quando)
            .ToList() ?? new List<RegistoBench>();

        if (serie.Count >= 2)
        {
            var spark = new Sparkline(Math.Clamp(serie.Count, 2, 600)) { Height = 64 };
            foreach (var r in serie) spark.Adicionar(r.Nota);

            var caixa = new StackPanel { Spacing = 2 };
            caixa.Children.Add(UI.Texto(
                Textos.TF("últimas {0} corridas · {1} · {2} · ", serie.Count, serie[0].Tipo, serie[0].Alvo) +
                Textos.TF("mín {0} · máx {1}", $"{serie.Min(r => r.Nota):N0}", $"{serie.Max(r => r.Nota):N0}"), "mono"));
            caixa.Children.Add(spark);
            _histGrafico.Children.Add(caixa);
        }

        _histLista.Children.Add(LinhaHistorico("quando", "tipo", "alvo", "nota", "detalhe", true));
        foreach (var r in lista)
            _histLista.Children.Add(LinhaHistorico(r.Quando.ToString("dd/MM/yyyy HH:mm"), r.Tipo, r.Alvo,
                r.Nota.ToString("N0"), r.Detalhe, false));
    }

    private static Control LinhaHistorico(string quando, string tipo, string alvo, string nota, string detalhe, bool cabecalho)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("130,60,3*,85,2*") };
        var textos = new[] { quando, tipo, alvo, nota, detalhe };
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

    private void Exportar(string formato)
    {
        try
        {
            var pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "linux-explode-relatorios");
            string ficheiro = Path.Combine(pasta, $"bench-{DateTime.Now:yyyyMMdd-HHmmss}.{formato}");

            if (formato == "csv") Historico.ExportarCsv(ficheiro);
            else Historico.ExportarJson(ficheiro);

            _historicoEstado.Text = Textos.TF("gravado em {0}", ficheiro);
        }
        catch (Exception ex)
        {
            _historicoEstado.Text = Textos.T("não consegui exportar: ") + ex.Message;
        }
    }

    private async void Importar()
    {
        try
        {
            var janela = TopLevel.GetTopLevel(this);
            if (janela is null) return;

            var ficheiros = await janela.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Importar resultados (JSON)",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Resultados JSON") { Patterns = new[] { "*.json" } }
                    }
                });

            if (ficheiros.Count == 0) return;

            int novos = Historico.Importar(ficheiros[0].Path.LocalPath);
            _historicoEstado.Text = novos > 0
                ? Textos.TF("{0} resultado(s) importado(s) de {1}", novos, ficheiros[0].Name)
                : "o ficheiro não tinha resultados novos";
        }
        catch (Exception ex)
        {
            _historicoEstado.Text = Textos.T("não consegui importar: ") + ex.Message;
        }
    }

    /// <summary>Converte a nota formatada ("13 355 364") em número.</summary>
    private static double Numero(string nota)
    {
        var digitos = new string(nota.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        return double.TryParse(digitos, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0;
    }
}

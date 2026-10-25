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
    private readonly TextBlock _estado = new() { Text = "pronto", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _pontuacao = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _historico = new() { Text = "", Classes = { "mono" } };
    private readonly List<string> _runs = new();

    // RAM bench
    private readonly TextBlock _ramEstado = new() { Text = "pronto", Classes = { "sub" } };
    private readonly StackPanel _ramResultado = new() { Spacing = 0 };

    // Disco bench
    private readonly TextBlock _discoEstado = new() { Text = "pronto", Classes = { "sub" } };
    private readonly StackPanel _discoResultado = new() { Spacing = 0 };

    // GPU bench
    private readonly ComboBox _gpuLista = new() { Width = 560, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _gpuEstado = new() { Text = "pronto", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _gpuPontuacao = new() { Text = "—", Classes = { "numero" } };
    private readonly List<InfoGpu> _gpus = new();

    public PaginaBench()
    {
        Content = UI.Pagina(
            UI.Cabecalho("EXPLODE BENCH", "Path tracing de CPU e GPU · banda de memória · disco"),
            _corpo);

        _corpo.Children.Add(CartaoGpu());
        _corpo.Children.Add(new CartaoFps());
        _corpo.Children.Add(CartaoCpu());
        _corpo.Children.Add(CartaoRam());
        _corpo.Children.Add(CartaoDisco());

        Loaded += (_, _) =>
        {
            CarregarGpus();
            // modo de teste automático: LINUXEXPLODE_AUTOTEST=1 (compute) ou "rt" (ray tracing)
            // abre o pop do bench, que é o caminho real do botão Testar GPU
            var auto = Environment.GetEnvironmentVariable("LINUXEXPLODE_AUTOTEST");
            if (!string.IsNullOrEmpty(auto) && _gpus.Count > 0)
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
            _gpuEstado.Text = "nenhuma GPU Vulkan com fila de computação — verifica o driver (vulkan-radeon / mesa)";
            return;
        }

        foreach (var g in _gpus)
            _gpuLista.Items.Add($"{g.Nome}  ({g.Tipo})");
        _gpuLista.SelectedIndex = 0;
        _gpuEstado.Text = $"{_gpus.Count} GPU(s) Vulkan detetada(s)";
    }

    private Control CartaoGpu()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "O mesmo path tracer da CPU, agora em compute shader Vulkan: " +
            $"{BenchGpu.AmostrasPadrao} amostras por pixel, repetido até {BenchGpu.AlvoSegundos:0.0} s para a nota " +
            "ser estável — a resolução escolhe-se no pop-up, de 320×180 até 4K. " +
            "O modo ray tracing usa VK_KHR_ray_query com 64 esferas em BVH — a nota não é comparável com a de compute.", "sub"));
        conteudo.Children.Add(_gpuLista);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Testar GPU", true, () => AbrirPrevia(ModalidadePrevia.Gpu)));
        botoes.Children.Add(UI.Botao("Testar ray tracing", false, () => AbrirPrevia(ModalidadePrevia.Rt)));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_gpuEstado);

        var linhaNota = new StackPanel { Spacing = 2 };
        linhaNota.Children.Add(UI.Texto("NOTA GPU", "rotulo"));
        linhaNota.Children.Add(_gpuPontuacao);
        conteudo.Children.Add(linhaNota);

        return UI.Cartao("GPU · EXPLODE BENCH (Vulkan)", conteudo);
    }

    /// <summary>
    /// Abre o pop do bench: a cena a animar + a medição, com escolha de resolução nos testes de GPU.
    /// A nota e o detalhe voltam para o cartão à medida que são medidos.
    /// </summary>
    private void AbrirPrevia(ModalidadePrevia modalidade, int threads = 0)
    {
        int indice = 0;
        if (modalidade != ModalidadePrevia.Cpu && _gpuLista.SelectedIndex >= 0 && _gpuLista.SelectedIndex < _gpus.Count)
            indice = _gpus[_gpuLista.SelectedIndex].Indice;

        string ultimaNota = "—";

        var janela = new JanelaPrevia(modalidade, indice, threads, r =>
        {
            ultimaNota = r.Nota;
            if (modalidade == ModalidadePrevia.Cpu)
            {
                _pontuacao.Text = r.Nota;
                _estado.Text = r.Detalhe;
                // cada corrida concluída entra no histórico (o pop recomeça sozinho)
                if (r.CorridaConcluida)
                {
                    _runs.Insert(0, $"{DateTime.Now:HH:mm}  {r.Nota} pts  ({threads}T)");
                    if (_runs.Count > 6) _runs.RemoveAt(_runs.Count - 1);
                    _historico.Text = string.Join("\n", _runs);
                }
            }
            else
            {
                _gpuPontuacao.Text = r.Nota;
                _gpuEstado.Text = (modalidade == ModalidadePrevia.Rt ? "ray tracing · " : "compute · ") + r.Detalhe;
            }
        });

        if (TopLevel.GetTopLevel(this) is Window dono) janela.Show(dono);
        else janela.Show();
    }

    // ------------------------------------------------------------------ CPU

    private Control CartaoCpu()
    {
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Multi-core", true, () => AbrirPrevia(ModalidadePrevia.Cpu, Environment.ProcessorCount)));
        botoes.Children.Add(UI.Botao("Single-core", false, () => AbrirPrevia(ModalidadePrevia.Cpu, 1)));

        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "O mesmo renderizador do EXPLODE BENCH original: path tracer determinístico, 960×540, 1024 amostras por pixel. " +
            "A nota é amostras por segundo ÷ 10.000 — comparável entre máquinas.", "sub"));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_estado);

        var linhaNota = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var pilha = new StackPanel { Spacing = 2 };
        pilha.Children.Add(UI.Texto("NOTA", "rotulo"));
        pilha.Children.Add(_pontuacao);
        linhaNota.Children.Add(pilha);
        var hist = new StackPanel { Spacing = 4, Margin = new Avalonia.Thickness(30, 0, 0, 0) };
        hist.Children.Add(UI.Texto("HISTÓRICO", "rotulo"));
        hist.Children.Add(_historico);
        Grid.SetColumn(hist, 1);
        linhaNota.Children.Add(hist);
        conteudo.Children.Add(linhaNota);

        return UI.Cartao("CPU · EXPLODE BENCH", conteudo);
    }

    // ------------------------------------------------------------------ RAM

    private Control CartaoRam()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto("Banda de escrita, leitura, cópia e latência real de acesso à RAM.", "sub"));
        conteudo.Children.Add(UI.Botao("Testar memória", true, async () =>
        {
            _ramEstado.Text = "a testar…";
            _ramResultado.Children.Clear();
            try
            {
                var r = await Task.Run(() => BenchRam.Correr(t => Dispatcher.UIThread.Post(() => _ramEstado.Text = t)));
                _ramResultado.Children.Add(UI.Linha("Escrita sequencial", $"{r.EscritaMBs:N0} MB/s"));
                _ramResultado.Children.Add(UI.Linha("Leitura sequencial", $"{r.LeituraMBs:N0} MB/s"));
                _ramResultado.Children.Add(UI.Linha("Cópia", $"{r.CopiaMBs:N0} MB/s"));
                _ramResultado.Children.Add(UI.Linha("Latência", $"{r.LatenciaNs:N1} ns", false));
                _ramEstado.Text = $"bloco de {Util.Bytes(r.Tamanho)}";
            }
            catch (Exception ex) { _ramEstado.Text = "falhou: " + ex.Message; }
        }));
        conteudo.Children.Add(_ramEstado);
        conteudo.Children.Add(_ramResultado);
        return UI.Cartao("Memória RAM", conteudo);
    }

    // ------------------------------------------------------------------ DISCO

    private Control CartaoDisco()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "Escrita e leitura sequencial + 4K aleatório num ficheiro temporário de 256 MB, " +
            "com O_DIRECT (sem passar pelo cache do sistema). O ficheiro é apagado no fim.", "sub"));
        conteudo.Children.Add(UI.Botao("Testar disco", true, async () =>
        {
            _discoEstado.Text = "a testar…";
            _discoResultado.Children.Clear();
            try
            {
                var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "linuxexplode");
                var r = await Task.Run(() => BenchDisco.Correr(pasta, 256, t => Dispatcher.UIThread.Post(() => _discoEstado.Text = t)));
                _discoResultado.Children.Add(UI.Linha("Sequencial · escrita", $"{r.SeqEscritaMBs:N0} MB/s"));
                _discoResultado.Children.Add(UI.Linha("Sequencial · leitura", $"{r.SeqLeituraMBs:N0} MB/s"));
                _discoResultado.Children.Add(UI.Linha("4K aleatório · leitura", $"{r.RandLeituraMBs:N1} MB/s"));
                _discoResultado.Children.Add(UI.Linha("4K aleatório · escrita", $"{r.RandEscritaMBs:N1} MB/s", false));
                _discoEstado.Text = $"ficheiro de {Util.Bytes(r.Ficheiro)} em {pasta}";
            }
            catch (Exception ex) { _discoEstado.Text = "falhou: " + ex.Message; }
        }));
        conteudo.Children.Add(_discoEstado);
        conteudo.Children.Add(_discoResultado);
        return UI.Cartao("Disco", conteudo);
    }
}

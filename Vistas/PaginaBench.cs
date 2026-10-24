using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaBench : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };

    // CPU bench
    private readonly Image _imagem = new() { Stretch = Stretch.Uniform, MaxHeight = 300 };
    private readonly ProgressBar _progresso = new() { Minimum = 0, Maximum = 1, Value = 0 };
    private readonly TextBlock _estado = new() { Text = "pronto", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _pontuacao = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _historico = new() { Text = "", Classes = { "mono" } };
    private readonly List<string> _runs = new();
    private DispatcherTimer? _pintor;
    private CancellationTokenSource? _cts;

    // RAM bench
    private readonly TextBlock _ramEstado = new() { Text = "pronto", Classes = { "sub" } };
    private readonly StackPanel _ramResultado = new() { Spacing = 0 };

    // Disco bench
    private readonly TextBlock _discoEstado = new() { Text = "pronto", Classes = { "sub" } };
    private readonly StackPanel _discoResultado = new() { Spacing = 0 };

    // GPU bench
    private readonly ComboBox _gpuLista = new() { Width = 560, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Image _gpuImagem = new() { Stretch = Stretch.Uniform, MaxHeight = 320 };
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
            var auto = Environment.GetEnvironmentVariable("LINUXEXPLODE_AUTOTEST");
            if (!string.IsNullOrEmpty(auto) && _gpus.Count > 0)
                _ = CorrerGpu(auto == "rt");
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
            $"{BenchGpu.LarguraPadrao}×{BenchGpu.AlturaPadrao}, {BenchGpu.AmostrasPadrao} amostras por pixel, " +
            $"repetido até {BenchGpu.AlvoSegundos:0.0} s para a nota ser estável. " +
            "O modo ray tracing usa VK_KHR_ray_query com 64 esferas em BVH — a nota não é comparável com a de compute.", "sub"));
        conteudo.Children.Add(_gpuLista);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Testar GPU", true, () => _ = CorrerGpu(false)));
        botoes.Children.Add(UI.Botao("Testar ray tracing", false, () => _ = CorrerGpu(true)));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_gpuEstado);

        conteudo.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0D0D0D")),
            BorderBrush = UI.Cor("Linha"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            MinHeight = 150,
            Padding = new Thickness(6),
            Child = _gpuImagem
        });

        var linhaNota = new StackPanel { Spacing = 2 };
        linhaNota.Children.Add(UI.Texto("NOTA GPU", "rotulo"));
        linhaNota.Children.Add(_gpuPontuacao);
        conteudo.Children.Add(linhaNota);

        return UI.Cartao("GPU · EXPLODE BENCH (Vulkan)", conteudo);
    }

    private async Task CorrerGpu(bool rayTracing = false)
    {
        if (_gpus.Count == 0) { CarregarGpus(); return; }

        int indice = _gpuLista.SelectedIndex >= 0 && _gpuLista.SelectedIndex < _gpus.Count
            ? _gpus[_gpuLista.SelectedIndex].Indice
            : _gpus[0].Indice;

        _gpuEstado.Text = rayTracing
            ? "a construir BLAS/TLAS e a aquecer a GPU…"
            : "a compilar o pipeline e a aquecer a GPU…";
        _gpuPontuacao.Text = "—";

        try
        {
            var (pontos, segundos, passagens, largura, altura, amostras, nome, rgba, extra) =
                await Task.Run(() =>
                {
                    if (rayTracing)
                    {
                        var rr = BenchRt.Correr(indice);
                        return rr is null
                            ? (0L, 0.0, 0, 0, 0, 0, "", Array.Empty<byte>(), 0)
                            : (rr.Pontos, rr.Segundos, rr.Passagens, rr.Largura, rr.Altura, rr.Amostras,
                               rr.Nome, rr.Rgba, rr.Esferas);
                    }
                    var rc = BenchGpu.Correr(indice);
                    return rc is null
                        ? (0L, 0.0, 0, 0, 0, 0, "", Array.Empty<byte>(), 0)
                        : (rc.Pontos, rc.Segundos, rc.Passagens, rc.Largura, rc.Altura, rc.Amostras,
                           rc.Nome, rc.Rgba, 0);
                });

            if (rgba.Length == 0)
            {
                _gpuEstado.Text = rayTracing
                    ? "indisponível: esta GPU não expõe VK_KHR_ray_query"
                    : "falhou: não foi possível criar o dispositivo Vulkan";
                return;
            }

            var bmp = new WriteableBitmap(new PixelSize(largura, altura),
                new Avalonia.Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (var fb = bmp.Lock())
                Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
            _gpuImagem.Source = bmp;

            _gpuPontuacao.Text = pontos.ToString("N0");
            _gpuEstado.Text = $"{nome} · {(rayTracing ? "ray tracing" : "compute")} · {segundos:0.00} s · " +
                              $"{passagens} passagens · {largura}×{altura} @ {amostras} spp" +
                              (extra > 0 ? $" · {extra} esferas em BVH" : "");
        }
        catch (Exception ex)
        {
            _gpuEstado.Text = "falhou: " + ex.Message;
        }
    }

    // ------------------------------------------------------------------ CPU

    private Control CartaoCpu()
    {
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Multi-core", true, () => _ = CorrerCpu(Environment.ProcessorCount)));
        botoes.Children.Add(UI.Botao("Single-core", false, () => _ = CorrerCpu(1)));
        botoes.Children.Add(UI.Botao("Cancelar", false, () =>
        {
            _cts?.Cancel();
            _estado.Text = "cancelado";
        }));

        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "O mesmo renderizador do EXPLODE BENCH original: path tracer determinístico, 960×540, 1024 amostras por pixel. " +
            "A nota é amostras por segundo ÷ 10.000 — comparável entre máquinas.", "sub"));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_progresso);
        conteudo.Children.Add(_estado);
        conteudo.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0D0D0D")),
            BorderBrush = UI.Cor("Linha"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            MinHeight = 150,
            Padding = new Thickness(6),
            Child = _imagem
        });

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

    private async Task CorrerCpu(int threads)
    {
        if (_cts is not null) return;

        _cts = new CancellationTokenSource();
        _progresso.Value = 0;
        _estado.Text = threads == 1 ? "a renderizar (single-core)…" : $"a renderizar com {threads} threads…";
        _pontuacao.Text = "—";

        var bench = new BenchRenderer();
        _pintor = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _pintor.Tick += (_, _) =>
        {
            _progresso.Value = bench.BucketsTotal > 0 ? (double)bench.BucketsDone / bench.BucketsTotal : 0;
            AtualizarImagem(bench);
        };
        _pintor.Start();

        try
        {
            var resultado = await Task.Run(() => bench.Run(threads, TimeSpan.Zero, _cts.Token));
            AtualizarImagem(bench);
            _progresso.Value = 1;

            double nota = resultado.Score;
            _pontuacao.Text = nota.ToString("N0");
            _estado.Text = $"{resultado.Threads} thread(s) · {resultado.Seconds:0.0} s · " +
                           $"{resultado.Samples:N0} amostras · {resultado.Passes} passada(s)";
            _runs.Insert(0, $"{DateTime.Now:HH:mm}  {nota:N0} pts  ({resultado.Threads}T, {resultado.Seconds:0.0}s)");
            if (_runs.Count > 6) _runs.RemoveAt(_runs.Count - 1);
            _historico.Text = string.Join("\n", _runs);
        }
        catch (OperationCanceledException)
        {
            _estado.Text = "cancelado";
        }
        catch (Exception ex)
        {
            _estado.Text = "falhou: " + ex.Message;
        }
        finally
        {
            _pintor?.Stop();
            _pintor = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void AtualizarImagem(BenchRenderer bench)
    {
        var pixels = bench.Pixels;
        var bmp = new WriteableBitmap(new PixelSize(BenchRenderer.Width, BenchRenderer.Height),
            new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = bmp.Lock())
            Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
        _imagem.Source = bmp;
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

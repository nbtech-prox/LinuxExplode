using System.Diagnostics;
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

public enum ModalidadePrevia { Cpu, Gpu, Rt }

/// <summary>Resumo que a janela devolve à página do bench (nota e detalhe).</summary>
public sealed record ResultadoPrevia(string Modalidade, string Nota, string Detalhe, bool CorridaConcluida = false);

/// <summary>
/// O "pop" do EXPLODE BENCH: abrir a partir de Testar GPU / Testar ray tracing / Multi-core /
/// Single-core mostra a cena a renderizar ao vivo, com escolha de resolução (até 4K) nos testes de
/// GPU, ao mesmo tempo que mede e mostra a nota. No CPU há uma barra por core.
/// </summary>
public sealed class JanelaPrevia : Window
{
    private static readonly (string Nome, int Largura, int Altura)[] Resolucoes =
    {
        ("320 × 180", 320, 180),
        ("480 × 270", 480, 270),
        ("640 × 360", 640, 360),
        ("800 × 450", 800, 450),
        ("1024 × 576", 1024, 576),
        ("1280 × 720 (padrão)", 1280, 720),
        ("1600 × 900", 1600, 900),
        ("1920 × 1080", 1920, 1080),
        ("2560 × 1440", 2560, 1440),
        ("3200 × 1800", 3200, 1800),
        ("3840 × 2160 (4K)", 3840, 2160)
    };

    private readonly ModalidadePrevia _modalidade;
    private readonly int _indiceGpu;
    private readonly int _threads;
    private readonly Action<ResultadoPrevia>? _aoAtualizar;

    private readonly Image _vista = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _titulo = new() { Classes = { "h2" } };
    private readonly TextBlock _nota = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _estado = new() { Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox? _resolucao;
    private readonly StackPanel? _barras;

    private WriteableBitmap? _bitmap;
    private CancellationTokenSource _cancela = new();
    private DispatcherTimer? _vigia;
    private readonly Stopwatch _relogio = Stopwatch.StartNew();
    private readonly long[] _anterior = new long[128];
    private int _desenhados, _pendente, _falhas, _geracao;
    private int _indiceResolucao = 5;      // 1280×720
    private bool _pronto;                  // a janela já está montada (o ComboBox assenta em Opened)
    private bool _suprimir;
    private int _largura, _altura, _amostras;
    private double _ultimoFps;
    private volatile bool _fechada;
    private long _inicioCorrida;
    private int _corridas;

    public JanelaPrevia(ModalidadePrevia modalidade, int indiceGpu = 0, int threads = 0,
                        Action<ResultadoPrevia>? aoAtualizar = null)
    {
        _modalidade = modalidade;
        _indiceGpu = indiceGpu;
        _threads = threads > 0 ? threads : Environment.ProcessorCount;
        _aoAtualizar = aoAtualizar;

        Title = modalidade switch
        {
            ModalidadePrevia.Cpu => "EXPLODE BENCH — CPU a renderizar",
            ModalidadePrevia.Gpu => "EXPLODE BENCH — GPU a renderizar",
            _ => "EXPLODE BENCH — ray tracing a renderizar"
        };
        Width = 1040;
        Height = 760;
        MinWidth = 480;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Application.Current?.FindResource("Fundo") as IBrush ?? Brushes.Black;

        _titulo.Text = modalidade switch
        {
            ModalidadePrevia.Cpu => _threads == 1
                ? "CPU · path tracer single-core a animar"
                : $"CPU · path tracer a animar · {_threads} threads",
            ModalidadePrevia.Gpu => "GPU · compute Vulkan a animar",
            _ => "GPU · ray tracing (VK_KHR_ray_query) a animar"
        };

        var topo = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        topo.Children.Add(_titulo);

        if (modalidade != ModalidadePrevia.Cpu)
        {
            // gancho de teste: LINUXEXPLODE_RESOLUCAO=3840x2160 escolhe a resolução inicial
            if (Environment.GetEnvironmentVariable("LINUXEXPLODE_RESOLUCAO") is { Length: > 2 } pedida)
            {
                var partes = pedida.ToLowerInvariant().Split('x');
                if (partes.Length == 2 && int.TryParse(partes[0], out int pl) && int.TryParse(partes[1], out int pa))
                    for (int i = 0; i < Resolucoes.Length; i++)
                        if (Resolucoes[i].Largura == pl && Resolucoes[i].Altura == pa) { _indiceResolucao = i; break; }
            }

            _resolucao = new ComboBox { Width = 200, Margin = new Thickness(12, 0, 12, 0) };
            foreach (var (nome, _, _) in Resolucoes) _resolucao.Items.Add(nome);
            _resolucao.SelectedIndex = _indiceResolucao;
            _resolucao.SelectionChanged += (_, _) =>
            {
                if (_suprimir || !_pronto || _resolucao.SelectedIndex < 0) return;
                _indiceResolucao = _resolucao.SelectedIndex;
                _ = Arrancar();
            };
            Grid.SetColumn(_resolucao, 1);
            topo.Children.Add(_resolucao);
        }

        var fechar = UI.Botao("Fechar", true, Close);
        Grid.SetColumn(fechar, 2);
        topo.Children.Add(fechar);

        // Grelha: a imagem ocupa o que sobra, o resto (barras, nota, estado) fica sempre visível
        var raiz = new Grid { Margin = new Thickness(16), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto") };
        raiz.Children.Add(topo);

        var moldura = new Border
        {
            Background = UI.Cor("Cartao2"),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Margin = new Thickness(0, 10, 0, 0),
            Child = _vista
        };
        Grid.SetRow(moldura, 1);
        raiz.Children.Add(moldura);

        if (modalidade == ModalidadePrevia.Cpu)
        {
            _barras = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Height = 44 };
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                var cheio = new Border
                {
                    Background = UI.Cor("Fg"),
                    CornerRadius = new CornerRadius(2),
                    Height = 2,
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                var pista = new Border
                {
                    Background = UI.Cor("Cartao2"),
                    CornerRadius = new CornerRadius(2),
                    Width = 18,
                    Height = 44,
                    Child = cheio
                };
                _barras.Children.Add(pista);
            }
            var faixa = new StackPanel { Spacing = 4, Margin = new Thickness(0, 10, 0, 0) };
            faixa.Children.Add(UI.Texto("CORES EM USO", "rotulo"));
            faixa.Children.Add(_barras);
            Grid.SetRow(faixa, 2);
            raiz.Children.Add(faixa);
        }

        var linhaNota = new StackPanel { Spacing = 2, Margin = new Thickness(0, 10, 0, 0) };
        linhaNota.Children.Add(UI.Texto(
            modalidade == ModalidadePrevia.Cpu ? "NOTA · AMOSTRAS/S ÷ 10.000" : "NOTA", "rotulo"));
        linhaNota.Children.Add(_nota);
        Grid.SetRow(linhaNota, 3);
        raiz.Children.Add(linhaNota);

        Grid.SetRow(_estado, 4);
        raiz.Children.Add(_estado);

        Content = raiz;

        Opened += (_, _) =>
        {
            // o ComboBox volta ao primeiro item quando é montado: reaplicar a escolha antes de medir
            if (_resolucao is not null)
            {
                _suprimir = true;
                _resolucao.SelectedIndex = _indiceResolucao;
                _suprimir = false;
            }
            _pronto = true;
            _ = Arrancar();
        };
        Closed += (_, _) =>
        {
            _fechada = true;
            _cancela.Cancel();
            _vigia?.Stop();
            // o bitmap só é libertado quando não há fotograma em trânsito: apagar antes disso
            // fazia rebentar a app (Lock() sobre um bitmap já libertado)
            if (Volatile.Read(ref _pendente) == 0) _bitmap?.Dispose();
        };
    }

    // ------------------------------------------------------------------ arranque

    private async Task Arrancar()
    {
        // cancela o que estava a correr e começa do zero (mudar a resolução cai aqui)
        _cancela.Cancel();
        _vigia?.Stop();
        _cancela = new CancellationTokenSource();
        var ct = _cancela.Token;
        _pendente = 0;
        _geracao++;

        if (_modalidade == ModalidadePrevia.Cpu)
        {
            ArrancarCpu(ct);
            return;
        }

        int sel = _indiceResolucao;
        var (largura, altura) = sel >= 0 && sel < Resolucoes.Length
            ? (Resolucoes[sel].Largura, Resolucoes[sel].Altura)
            : (1280, 720);

        CriarBitmap(largura, altura, PixelFormat.Rgba8888);
        _estado.Text = "a preparar o dispositivo Vulkan e a medir…";
        _nota.Text = "—";

        try
        {
            var medida = await Task.Run(() =>
            {
                if (_modalidade == ModalidadePrevia.Gpu)
                {
                    var rr = BenchGpu.Correr(_indiceGpu, largura, altura);
                    return rr is null
                        ? ((long Pontos, double Segundos, int Passagens, int Amostras, string Nome, byte[] Rgba))
                          (0L, 0.0, 0, 0, "", Array.Empty<byte>())
                        : (rr.Pontos, rr.Segundos, rr.Passagens, rr.Amostras, rr.Nome, rr.Rgba);
                }

                var rt = BenchRt.Correr(_indiceGpu, largura, altura);
                return rt is null
                    ? ((long Pontos, double Segundos, int Passagens, int Amostras, string Nome, byte[] Rgba))
                      (0L, 0.0, 0, 0, "", Array.Empty<byte>())
                    : (rt.Pontos, rt.Segundos, rt.Passagens, rt.Amostras, rt.Nome, rt.Rgba);
            }, ct);

            if (ct.IsCancellationRequested || _fechada) return;

            if (medida.Rgba.Length == 0)
            {
                _estado.Text = _modalidade == ModalidadePrevia.Gpu
                    ? "falhou: não foi possível criar o dispositivo Vulkan (verifica o driver vulkan-radeon)"
                    : "indisponível: esta GPU não expõe VK_KHR_ray_query";
                return;
            }

            _nota.Text = medida.Pontos.ToString("N0");
            string detalhe = $"{medida.Nome} · {largura}×{altura} @ {medida.Amostras} spp · " +
                             $"{medida.Segundos:0.00} s · {medida.Passagens} passagens";
            _estado.Text = detalhe + " — a animar…";
            _aoAtualizar?.Invoke(new ResultadoPrevia(_modalidade.ToString(),
                medida.Pontos.ToString("N0"), detalhe));

            // primeiro fotograma: o resultado da própria medição
            Entregar(medida.Rgba);

            int spp = SppAnimacao(largura, altura);
            _amostras = spp;
            _desenhados = 0;
            _ultimoFps = 0;
            _relogio.Restart();

            await Task.Run(() =>
            {
                if (_modalidade == ModalidadePrevia.Gpu)
                    BenchGpu.Correr(_indiceGpu, largura, altura, spp, (rgba, _) => Entregar(rgba), ct);
                else
                    BenchRt.Correr(_indiceGpu, largura, altura, spp, (rgba, _) => Entregar(rgba), ct);
            }, ct);
        }
        catch (OperationCanceledException)
        {
            // mudança de resolução ou janela fechada
        }
        catch (Exception e)
        {
            if (!_fechada) _estado.Text = "falhou: " + e.Message;
        }
    }

    /// <summary>
    /// Amostras por pixel na animação. O que interessa é a qualidade na janela, e a janela mostra
    /// o quadro em ~1000 px de largura: por isso o orçamento é de AMOSTRAS POR FOTOGRAMA (constante),
    /// não um número fixo de amostras por pixel. Assim 320×180 leva 256 spp (upescalado continua
    /// limpo) e 4K leva 32 spp (o downscale para a janela média 16 px e também limpa o ruído);
    /// um spp fixo fazia o contrário — grão grosso no pequeno e ruído forte no 4K, com o 1280×720
    /// a ser o único ponto onde calhava bem.
    /// </summary>
    private int SppAnimacao(int largura, int altura)
    {
        // G samples por fotograma: o compute faz ~13 G/s, o ray tracing ~4 G/s nesta máquina
        double alvo = _modalidade == ModalidadePrevia.Rt ? 130_000_000.0 : 265_000_000.0;
        int spp = (int)Math.Round(alvo / Math.Max(1, largura * altura));
        return Math.Clamp(spp, 16, 256);
    }

    // ------------------------------------------------------------------ CPU

    private void ArrancarCpu(CancellationToken ct)
    {
        CriarBitmap(BenchRenderer.Width, BenchRenderer.Height, PixelFormat.Bgra8888);
        _estado.Text = _threads == 1
            ? "a renderizar numa só thread…"
            : $"a renderizar com {_threads} threads…";
        _nota.Text = "—";

        var bench = new BenchRenderer();
        Interlocked.Exchange(ref _inicioCorrida, Stopwatch.GetTimestamp());

        _vigia = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _vigia.Tick += (_, _) =>
        {
            if (_fechada) return;
            CopiarDoBench(bench);
            AtualizarBarras();

            double segundos = Desde(Interlocked.Read(ref _inicioCorrida));
            double nota = segundos > 0 ? bench.SamplesDone / segundos / 10_000.0 : 0;
            int corrida = Volatile.Read(ref _corridas) + 1;
            _nota.Text = nota.ToString("N0");
            string detalhe = $"corrida {corrida} · {_threads} thread(s) · {bench.SamplesDone:N0} amostras em {segundos:0.0} s";
            _estado.Text = $"{detalhe} · bloco {bench.BucketsDone}/{bench.BucketsTotal} — a animar…";
            _aoAtualizar?.Invoke(new ResultadoPrevia("Cpu", nota.ToString("N0"), detalhe));
        };
        _vigia.Start();

        // Uma passagem completa (1024 amostras/pixel) é uma corrida; quando acaba, recomeça.
        _ = Task.Run(() =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Interlocked.Exchange(ref _inicioCorrida, Stopwatch.GetTimestamp());
                    var resultado = bench.Run(_threads, TimeSpan.Zero, ct);
                    if (ct.IsCancellationRequested) break;

                    int n = Interlocked.Increment(ref _corridas);
                    string nota = resultado.Score.ToString("N0");
                    string detalhe = $"corrida {n} · {_threads} thread(s) · {resultado.Seconds:0.0} s · {resultado.Samples:N0} amostras";
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_fechada) return;
                        _nota.Text = nota;
                        _estado.Text = $"{detalhe} — a recomeçar…";
                        _aoAtualizar?.Invoke(new ResultadoPrevia("Cpu", nota, detalhe, true));
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Console.WriteLine("[previa] cpu falhou: " + e.Message); }
        });
    }

    private static double Desde(long inicio) =>
        (Stopwatch.GetTimestamp() - inicio) / (double)Stopwatch.Frequency;

    private void CopiarDoBench(BenchRenderer bench)
    {
        var bitmap = _bitmap;
        if (bitmap is null || _fechada) return;
        int geracao = _geracao;
        var pixels = bench.Pixels;
        try
        {
            using (var buffer = bitmap.Lock())
            {
                // Marshal.Copy(int[]…) conta ELEMENTOS: um int por pixel
                for (int y = 0; y < _altura; y++)
                    Marshal.Copy(pixels, y * _largura, buffer.Address + y * buffer.RowBytes, _largura);
            }
            if (geracao == _geracao) _vista.InvalidateVisual();
        }
        catch (Exception e)
        {
            if (Interlocked.Increment(ref _falhas) == 1)
                Console.WriteLine("[previa] cópia falhou: " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>Uma barra por core com a ocupação desde a última leitura de /proc/stat.</summary>
    private void AtualizarBarras()
    {
        if (_barras is null) return;

        int cores = Math.Min(_barras.Children.Count, _anterior.Length / 2);
        var agora = new long[cores * 2];
        try
        {
            foreach (var linha in File.ReadLines("/proc/stat"))
            {
                if (!linha.StartsWith("cpu", StringComparison.Ordinal) || linha.StartsWith("cpu ", StringComparison.Ordinal))
                    continue;
                var partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length < 5) continue;
                if (!int.TryParse(partes[0].AsSpan(3), out int n) || n < 0 || n >= cores) continue;
                long total = 0;
                for (int i = 1; i < partes.Length && i < 9; i++) total += long.Parse(partes[i]);
                agora[n * 2] = total;
                agora[n * 2 + 1] = long.Parse(partes[4]);
            }
        }
        catch { return; }

        for (int i = 0; i < cores; i++)
        {
            long dTotal = agora[i * 2] - _anterior[i * 2];
            long dOcioso = agora[i * 2 + 1] - _anterior[i * 2 + 1];
            _anterior[i * 2] = agora[i * 2];
            _anterior[i * 2 + 1] = agora[i * 2 + 1];

            double uso = dTotal > 0 ? 1.0 - (double)dOcioso / dTotal : 0;
            uso = Math.Clamp(uso, 0, 1);
            if (_barras.Children[i] is Border { Child: Border cheio })
                cheio.Height = Math.Max(2, uso * 42);
        }
    }

    // ------------------------------------------------------------------ fotogramas

    private void CriarBitmap(int largura, int altura, PixelFormat formato)
    {
        _largura = largura;
        _altura = altura;
        // o bitmap antigo fica para o GC: libertá-lo com um fotograma em trânsito rebentava a app
        _bitmap = new WriteableBitmap(new PixelSize(largura, altura), new Avalonia.Vector(96, 96), formato, AlphaFormat.Opaque);
        _vista.Source = _bitmap;
    }

    private void Entregar(byte[] rgba)
    {
        if (_fechada || rgba.Length < _largura * _altura * 4) return;
        if (Interlocked.Exchange(ref _pendente, 1) == 1) return;   // ainda não desenhou o anterior
        int geracao = _geracao;
        int largura = _largura, altura = _altura;
        Dispatcher.UIThread.Post(() =>
        {
            var bitmap = _bitmap;
            if (!_fechada && bitmap is not null && geracao == _geracao)
            {
                try
                {
                    using (var buffer = bitmap.Lock())
                    {
                        for (int y = 0; y < altura; y++)
                            Marshal.Copy(rgba, y * largura * 4, buffer.Address + y * buffer.RowBytes, largura * 4);
                    }
                    _vista.InvalidateVisual();
                    Contar();
                }
                catch (Exception e)
                {
                    if (Interlocked.Increment(ref _falhas) == 1)
                        Console.WriteLine("[previa] fotograma falhou: " + e.GetType().Name + ": " + e.Message);
                }
            }
            Interlocked.Exchange(ref _pendente, 0);
        });
    }

    private void Contar()
    {
        _desenhados++;
        double segundos = _relogio.Elapsed.TotalSeconds;
        if (segundos >= 0.5)
        {
            _ultimoFps = _desenhados / segundos;
            _desenhados = 0;
            _relogio.Restart();
        }

        string ritmo = _ultimoFps >= 0.5 ? $"{_ultimoFps:0} fotogramas/s" : "a contar fotogramas…";
        _estado.Text = $"{_largura}×{_altura} · {_amostras} amostras/pixel · {ritmo} · câmara em órbita";
    }
}

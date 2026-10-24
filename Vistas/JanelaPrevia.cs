using System.Diagnostics;
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

/// <summary>
/// Pop-up do EXPLODE BENCH: mostra a cena a ser renderizada ao vivo, com a câmara a orbitar.
/// Usa a mesma cena do benchmark (CPU: <see cref="BenchRenderer"/>, GPU: path tracer em Vulkan,
/// RT: <c>VK_KHR_ray_query</c>) mas em baixa resolução e poucas amostras, para haver movimento.
/// </summary>
public sealed class JanelaPrevia : Window
{
    private readonly ModalidadePrevia _modalidade;
    private readonly WriteableBitmap _bitmap;
    private readonly Image _vista;
    private readonly TextBlock _estado = new() { Classes = { "sub" } };
    private readonly TextBlock _titulo = new() { Classes = { "h2" } };
    private readonly CancellationTokenSource _cancela = new();
    private readonly Stopwatch _relogio = Stopwatch.StartNew();
    private int _desenhados;
    private int _pendente;      // 1 = há um fotograma à espera do thread da interface
    private volatile bool _fechada;
    private int _falhas;
    private double _ultimoFps;
    private int _largura, _altura;
    private readonly int _amostras;

    private readonly int _indiceGpu;

    public JanelaPrevia(ModalidadePrevia modalidade, int indiceGpu = 0)
    {
        _modalidade = modalidade;
        _indiceGpu = indiceGpu;
        // Resolução e amostras medidas nesta máquina (RX 6700 XT / 9700X) para ficar entre
        // 35 e 60 fotogramas/s sem perder nitidez: a CPU paga caro por pixel, a GPU não.
        (_largura, _altura, _amostras) = modalidade switch
        {
            ModalidadePrevia.Cpu => (560, 315, 6),    // ~42 fps
            ModalidadePrevia.Gpu => (960, 540, 32),   // ~60 fps
            _ => (800, 450, 16)                       // ~55 fps
        };

        Title = modalidade switch
        {
            ModalidadePrevia.Cpu => "EXPLODE BENCH — CPU a renderizar",
            ModalidadePrevia.Gpu => "EXPLODE BENCH — GPU a renderizar",
            _ => "EXPLODE BENCH — ray tracing a renderizar"
        };
        Width = 900;
        Height = 620;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Application.Current?.FindResource("Fundo") as IBrush ?? Brushes.Black;

        var formato = modalidade == ModalidadePrevia.Cpu ? PixelFormat.Bgra8888 : PixelFormat.Rgba8888;
        _bitmap = new WriteableBitmap(new PixelSize(_largura, _altura), new Avalonia.Vector(96, 96), formato, AlphaFormat.Opaque);

        _vista = new Image
        {
            Source = _bitmap,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _titulo.Text = modalidade switch
        {
            ModalidadePrevia.Cpu => "CPU · path tracer a animar",
            ModalidadePrevia.Gpu => "GPU · compute Vulkan a animar",
            _ => "GPU · ray tracing (VK_KHR_ray_query) a animar"
        };

        var fechar = UI.Botao("Fechar", true, Close);
        var topo = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        topo.Children.Add(_titulo);
        Grid.SetColumn(fechar, 1);
        topo.Children.Add(fechar);

        var raiz = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        raiz.Children.Add(topo);
        raiz.Children.Add(new Border
        {
            Background = UI.Cor("Cartao2"),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = _vista
        });
        raiz.Children.Add(_estado);

        Content = raiz;

        Opened += (_, _) =>
        {
            Console.WriteLine($"[previa] aberta: {_modalidade} {_largura}x{_altura}");
            _ = Task.Run(Desenhar);
        };
        Closed += (_, _) =>
        {
            Console.WriteLine($"[previa] fechada: {_modalidade} (fotogramas={_ultimoFps:0}/s)");
            _fechada = true;
            _cancela.Cancel();
            // o bitmap só é libertado quando não há fotograma em trânsito: apagar antes disso
            // fazia rebentar a app (Lock() sobre um bitmap já libertado)
            if (Volatile.Read(ref _pendente) == 0) _bitmap.Dispose();
        };
    }

    /// <summary>Arranca o ciclo de renderização (corre num thread de fundo).</summary>
    private void Desenhar()
    {
        try
        {
            switch (_modalidade)
            {
                case ModalidadePrevia.Cpu:
                    BenchRenderer.Previa(_largura, _altura, _amostras, _cancela.Token,
                        (pixels, _) => EntregarCpu(pixels));
                    break;
                case ModalidadePrevia.Gpu:
                    BenchGpu.Correr(_indiceGpu, _largura, _altura, _amostras,
                        (rgba, _) => EntregarRgba(rgba), _cancela.Token);
                    break;
                default:
                    BenchRt.Correr(_indiceGpu, _largura, _altura, _amostras,
                        (rgba, _) => EntregarRgba(rgba), _cancela.Token);
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("[previa] falhou: " + e);
            Dispatcher.UIThread.Post(() => _estado.Text = "Não foi possível animar: " + e.Message);
        }
    }

    private void EntregarCpu(int[] pixels)
    {
        if (_fechada) return;
        if (Interlocked.Exchange(ref _pendente, 1) == 1) return;   // ainda não desenhou o anterior
        Dispatcher.UIThread.Post(() =>
        {
            Copiar(buffer =>
            {
                // Marshal.Copy(int[]…) conta ELEMENTOS: um int por pixel
                for (int y = 0; y < _altura; y++)
                    System.Runtime.InteropServices.Marshal.Copy(pixels, y * _largura,
                        buffer.Address + y * buffer.RowBytes, _largura);
            });
            Interlocked.Exchange(ref _pendente, 0);
            if (!_fechada) Contar();
        });
    }

    private void EntregarRgba(byte[] rgba)
    {
        if (_fechada) return;
        if (Interlocked.Exchange(ref _pendente, 1) == 1) return;
        Dispatcher.UIThread.Post(() =>
        {
            Copiar(buffer =>
            {
                for (int y = 0; y < _altura; y++)
                    System.Runtime.InteropServices.Marshal.Copy(rgba, y * _largura * 4,
                        buffer.Address + y * buffer.RowBytes, _largura * 4);
            });
            Interlocked.Exchange(ref _pendente, 0);
            if (!_fechada) Contar();
        });
    }

    private void Copiar(Action<ILockedFramebuffer> copia)
    {
        if (_fechada) return;
        try
        {
            using var buffer = _bitmap.Lock();
            copia(buffer);
            _vista.InvalidateVisual();
        }
        catch (Exception e)
        {
            // janela a fechar ou bitmap libertado: não há nada a desenhar
            if (Interlocked.Increment(ref _falhas) == 1)
                Console.WriteLine("[previa] copia falhou: " + e.GetType().Name + ": " + e.Message);
        }
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

        _estado.Text = $"{_largura}×{_altura} · {_amostras} amostras/pixel · {_ultimoFps:0} fotogramas/s · " +
                       "câmara em órbita · mesma cena e mesma iluminação do teste";
    }
}

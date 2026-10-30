using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

/// <summary>
/// Ícone na bandeja do sistema (StatusNotifierItem no Linux) com as temperaturas da CPU e da GPU no
/// tooltip e um menu para abrir a janela, parar todo o stress e sair. Os valores vêm dos mesmos
/// sensores do resto da aplicação; quando não há leitura, o tooltip di-lo em vez de mostrar zero.
/// </summary>
public sealed class Bandeja : IDisposable
{
    private readonly MainWindow _janela;
    private readonly TrayIcon _icone;
    private readonly NativeMenuItem _estado = new() { IsEnabled = false };
    private readonly NativeMenuItem _parar = new();
    private readonly NativeMenuItem _fecharParaBandeja = new() { ToggleType = NativeMenuItemToggleType.CheckBox };
    private readonly NativeMenuItem _abrir = new();
    private readonly NativeMenuItem _sensores = new();
    private readonly NativeMenuItem _dispositivos = new();
    private readonly NativeMenuItem _sair = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _aActualizar;

    private static Bandeja? _instancia;

    /// <summary>Falso quando o ambiente não tem bandeja (sem StatusNotifierItem/AppIndicator).</summary>
    public static bool Disponivel { get; private set; }

    private Bandeja(MainWindow janela, TrayIcon icone)
    {
        _janela = janela;
        _icone = icone;

        _fecharParaBandeja.IsChecked = Config.Atual.BandejaFechar;
        _fecharParaBandeja.Click += (_, _) =>
        {
            Config.Atual.BandejaFechar = !Config.Atual.BandejaFechar;
            _fecharParaBandeja.IsChecked = Config.Atual.BandejaFechar;
            Config.Atual.Guardar();
        };

        _abrir.Click += (_, _) => janela.MostrarEm();
        _sensores.Click += (_, _) => janela.MostrarEm("sensores");
        _dispositivos.Click += (_, _) => janela.MostrarEm("dispositivos");
        _parar.Click += (_, _) =>
        {
            var n = SessaoStress.PararTodas();
            _estado.Header = n > 0 ? Textos.TF("stress parado ({0} sessão/ões)", n) : Textos.T("não havia stress a correr");
        };
        _sair.Click += (_, _) => janela.Sair();

        var menu = new NativeMenu();
        menu.Items.Add(_estado);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_abrir);
        menu.Items.Add(_sensores);
        menu.Items.Add(_dispositivos);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_parar);
        menu.Items.Add(_fecharParaBandeja);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_sair);

        icone.Menu = menu;
        icone.Clicked += (_, _) => janela.MostrarEm();

        AplicarTextosInterno();
        _timer.Tick += (_, _) => Actualizar();
        Actualizar();
        _timer.Start();
    }

    /// <summary>Cria o ícone. Devolve null quando o ambiente não o suporta (não rebenta a aplicação).</summary>
    public static Bandeja? Criar(MainWindow janela)
    {
        try
        {
            var icone = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://LinuxExplode/Assets/logo.png"))),
                ToolTipText = "LINUX EXPLODE",
                IsVisible = true
            };

            var bandeja = new Bandeja(janela, icone);
            TrayIcon.SetIcons(Application.Current!, new TrayIcons { icone });
            _instancia = bandeja;
            Disponivel = true;
            return bandeja;
        }
        catch
        {
            // sem barra de sistema (sessão mínima, sem StatusNotifierWatcher): a aplicação segue normal
            Disponivel = false;
            return null;
        }
    }

    /// <summary>Reaplica os textos do menu quando o idioma muda (a bandeja vive fora das páginas).</summary>
    public static void AplicarTextos() => _instancia?.AplicarTextosInterno();

    private void AplicarTextosInterno()
    {
        _abrir.Header = Textos.T("Abrir janela");
        _sensores.Header = Textos.T("Sensores");
        _dispositivos.Header = Textos.T("Dispositivos");
        _parar.Header = Textos.T("Parar todo o stress");
        _fecharParaBandeja.Header = Textos.T("Fechar para a bandeja");
        _sair.Header = Textos.T("Sair");
        Actualizar();
    }

    private void Actualizar()
    {
        if (_aActualizar) return;
        _aActualizar = true;
        try
        {
            var (cpu, gpu, watts) = LerTemperaturas();
            var partes = new List<string>();
            if (cpu is double c) partes.Add($"CPU {c:0} °C");
            if (gpu is double g) partes.Add($"GPU {g:0} °C");
            if (watts is double w) partes.Add($"{w:0} W");

            _icone.ToolTipText = partes.Count > 0
                ? "LINUX EXPLODE · " + string.Join(" · ", partes)
                : Textos.T("LINUX EXPLODE · sem leitura de sensores");

            var emCurso = SessaoStress.EmCursoAgora;
            _parar.IsEnabled = emCurso > 0;
            _parar.Header = emCurso > 0 ? Textos.TF("Parar todo o stress ({0})", emCurso) : Textos.T("Parar todo o stress");

            _estado.Header = partes.Count > 0
                ? string.Join(" · ", partes) + (emCurso > 0 ? Textos.TF(" · stress {0}", emCurso) : "")
                : Textos.T("sem leitura de sensores (hwmon vazio?)");
        }
        catch
        {
            _icone.ToolTipText = Textos.T("LINUX EXPLODE");
        }
        finally { _aActualizar = false; }
    }

    private static (double? Cpu, double? Gpu, double? Watts) LerTemperaturas()
    {
        double? cpu = null, gpu = null, watts = null;
        try { cpu = Sensores.CpuTemperatura(); } catch { }
        try { gpu = Sensores.GpuTemperatura(); } catch { }

        // o RAPL precisa de duas leituras: a bandeja usa a potência já medida pela página de sensores
        try { watts = Potencia.Ler().TotalW; } catch { }
        return (cpu, gpu, watts);
    }

    public void Dispose()
    {
        _timer.Stop();
        try { TrayIcon.SetIcons(Application.Current!, new TrayIcons()); } catch { }
        _icone.Dispose();
        _instancia = null;
        Disponivel = false;
    }
}

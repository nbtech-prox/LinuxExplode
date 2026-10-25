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
    private readonly NativeMenuItem _estado = new() { Header = "a ler sensores…", IsEnabled = false };
    private readonly NativeMenuItem _parar = new() { Header = "Parar todo o stress" };
    private readonly NativeMenuItem _fecharParaBandeja;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _aActualizar;

    /// <summary>Falso quando o ambiente não tem bandeja (sem StatusNotifierItem/AppIndicator).</summary>
    public static bool Disponivel { get; private set; }

    private Bandeja(MainWindow janela, TrayIcon icone)
    {
        _janela = janela;
        _icone = icone;

        _fecharParaBandeja = new NativeMenuItem
        {
            Header = "Fechar para a bandeja",
            IsChecked = Config.Atual.BandejaFechar,
            ToggleType = NativeMenuItemToggleType.CheckBox
        };
        _fecharParaBandeja.Click += (_, _) =>
        {
            Config.Atual.BandejaFechar = !Config.Atual.BandejaFechar;
            _fecharParaBandeja.IsChecked = Config.Atual.BandejaFechar;
            Config.Atual.Guardar();
        };

        var abrir = new NativeMenuItem { Header = "Abrir janela" };
        abrir.Click += (_, _) => janela.MostrarEm();

        var sensores = new NativeMenuItem { Header = "Sensores" };
        sensores.Click += (_, _) => janela.MostrarEm("sensores");

        var dispositivos = new NativeMenuItem { Header = "Dispositivos" };
        dispositivos.Click += (_, _) => janela.MostrarEm("dispositivos");

        _parar.Click += (_, _) =>
        {
            var n = SessaoStress.PararTodas();
            _estado.Header = n > 0 ? $"stress parado ({n} sessão/ões)" : "não havia stress a correr";
        };

        var sair = new NativeMenuItem { Header = "Sair" };
        sair.Click += (_, _) => janela.Sair();

        var menu = new NativeMenu();
        menu.Items.Add(_estado);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(abrir);
        menu.Items.Add(sensores);
        menu.Items.Add(dispositivos);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_parar);
        menu.Items.Add(_fecharParaBandeja);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(sair);

        icone.Menu = menu;
        icone.Clicked += (_, _) => janela.MostrarEm();

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
                : "LINUX EXPLODE · sem leitura de sensores";

            var emCurso = SessaoStress.EmCursoAgora;
            _parar.IsEnabled = emCurso > 0;
            _parar.Header = emCurso > 0 ? $"Parar todo o stress ({emCurso})" : "Parar todo o stress";

            _estado.Header = partes.Count > 0
                ? string.Join(" · ", partes) + (emCurso > 0 ? $" · stress {emCurso}" : "")
                : "sem leitura de sensores (hwmon vazio?)";
        }
        catch
        {
            _icone.ToolTipText = "LINUX EXPLODE";
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
        Disponivel = false;
    }
}

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LinuxExplode.Core;
using LinuxExplode.Vistas;

namespace LinuxExplode;

public partial class App : Application
{
    private Bandeja? _bandeja;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var janela = new MainWindow();
            desktop.MainWindow = janela;

            // Gancho de teste: LINUXEXPLODE_SEM_BANDEJA=1 arranca sem ícone (para capturas limpas).
            var semBandeja = Environment.GetEnvironmentVariable("LINUXEXPLODE_SEM_BANDEJA") is { Length: > 0 };
            if (Config.Atual.Bandeja && !semBandeja)
                _bandeja = Bandeja.Criar(janela);

            // Gancho de verificação: prova que fechar esconde (e não mata) e que MostrarEm recupera.
            if (Environment.GetEnvironmentVariable("LINUXEXPLODE_TESTE_BANDEJA") is { Length: > 0 })
                _ = TestarBandeja(janela);

            desktop.ShutdownRequested += (_, _) => { _bandeja?.Dispose(); _bandeja = null; };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task TestarBandeja(MainWindow janela)
    {
        await Task.Delay(4000);
        var pid = Environment.ProcessId;
        janela.Close();
        await Task.Delay(1200);
        Console.WriteLine($"bandeja-teste: depois de fechar → janela visível={janela.IsVisible}, processo {pid} vivo");

        janela.MostrarEm();
        await Task.Delay(1200);
        Console.WriteLine($"bandeja-teste: depois de MostrarEm → janela visível={janela.IsVisible}");

        janela.Sair();
        await Task.Delay(500);
        Console.WriteLine("bandeja-teste: Sair fechou a aplicação");
    }
}

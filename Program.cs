using System.Globalization;
using Avalonia;

namespace LinuxExplode;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // A aplicação é pt-PT: sem isto os números saem com o formato do sistema (12,969,144 em vez de
        // 12 969 144). Todas as leituras de /proc e /sys usam InvariantCulture, por isso a mudança não
        // afecta a interpretação de valores — só a apresentação.
        var portugues = new CultureInfo("pt-PT");
        CultureInfo.DefaultThreadCurrentCulture = portugues;
        CultureInfo.DefaultThreadCurrentUICulture = portugues;
        CultureInfo.CurrentCulture = portugues;
        CultureInfo.CurrentUICulture = portugues;

        if (args.Length > 0 && args[0] == "--cli")
        {
            Environment.Exit(Core.Cli.Executar(args));
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

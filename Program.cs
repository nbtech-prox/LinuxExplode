using System.Globalization;
using Avalonia;
using LinuxExplode.Core;

namespace LinuxExplode;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // A língua vem da configuração; na primeira execução segue a do sistema (pt/es/en) e o resto
        // fica em pt-PT. A cultura acompanha o idioma para os números e as datas mudarem com ele.
        // LINUXEXPLODE_IDIOMA=pt|es|en força a escolha (capturas e testes).
        var forcar = Environment.GetEnvironmentVariable("LINUXEXPLODE_IDIOMA");
        var idioma = !string.IsNullOrWhiteSpace(forcar)
            ? Core.Textos.DeCodigo(forcar)
            : string.IsNullOrWhiteSpace(Core.Config.Atual.Idioma)
                ? Core.Textos.DoSistema()
                : Core.Textos.DeCodigo(Core.Config.Atual.Idioma);
        Core.Textos.Aplicar(idioma);

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

using System.Globalization;
using Avalonia;
using LinuxExplode.Core;

namespace LinuxExplode;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // O português é o idioma principal: por omissão a interface abre sempre em pt-PT. Quem quiser
        // espanhol ou inglês escolhe no selector da barra lateral e a escolha fica gravada.
        // LINUXEXPLODE_IDIOMA=pt|es|en força a escolha (capturas e testes).
        var forcar = Environment.GetEnvironmentVariable("LINUXEXPLODE_IDIOMA");
        var idioma = !string.IsNullOrWhiteSpace(forcar)
            ? Core.Textos.DeCodigo(forcar)
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

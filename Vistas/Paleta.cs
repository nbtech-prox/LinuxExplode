using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace LinuxExplode.Vistas;

/// <summary>
/// Paleta da aplicação em dois temas. As cores vivem em recursos (Tema.axaml) e são trocadas em
/// execução — por isso os estilos usam <c>DynamicResource</c>.
/// </summary>
public static class Paleta
{
    private static readonly (string Chave, string Escuro, string Claro)[] Cores =
    {
        ("Fundo",        "#0A0A0A", "#F4F4F2"),
        ("BarraLateral", "#0C0C0C", "#ECECE9"),
        ("Cartao",       "#111111", "#FFFFFF"),
        ("Cartao2",      "#151515", "#FAFAF8"),
        ("Linha",        "#202020", "#DCDCD7"),
        ("Fg",           "#F2F2F2", "#15181B"),
        ("Muted",        "#8C8C8C", "#5C6367"),
        ("Faint",        "#4A4A4A", "#9AA1A5"),
        ("Bom",          "#9BE8A8", "#157F45"),
        ("Atencao",      "#E8D08C", "#8A6410"),
        ("Mau",          "#E89B9B", "#B23131"),
        ("Botao",        "#1A1A1A", "#EFEFEC"),
        ("BotaoHover",   "#242424", "#E4E4E0"),
        ("BotaoFg",      "#0A0A0A", "#FFFFFF"),
        ("PrimarioHover","#D8D8D8", "#3A3D40"),
        ("NavHover",     "#161616", "#EAEAE6"),
        ("NavAtivo",     "#1C1C1C", "#E2E2DD"),
        ("Trilho",       "#1E1E1E", "#E6E6E2")
    };

    public static bool Claro { get; private set; }

    public static void Aplicar(bool claro)
    {
        var app = Application.Current;
        if (app is null) return;

        Claro = claro;

        foreach (var (chave, escuro, claroHex) in Cores)
        {
            var pincel = new SolidColorBrush(Color.Parse(claro ? claroHex : escuro));

            // a aplicação manda sobre os recursos dos estilos, mas actualizamos os dois para o caso
            // de a chave ter sido definida dentro de um StyleInclude
            app.Resources[chave] = pincel;

            foreach (var estilo in app.Styles)
                if (estilo is Styles grupo && grupo.Resources.ContainsKey(chave))
                    grupo.Resources[chave] = pincel;
        }

        // O Fluent pinta sliders e selecções com a cor de destaque do sistema (azul). Numa interface
        // monocromática isso destoa: a destaque passa a ser um cinza da paleta.
        var destaque = Color.Parse(claro ? "#5C6367" : "#9A9A9A");
        foreach (var chave in new[]
                 {
                     "SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorLight2",
                     "SystemAccentColorLight3", "SystemAccentColorDark1", "SystemAccentColorDark2",
                     "SystemAccentColorDark3"
                 })
            app.Resources[chave] = destaque;

        app.RequestedThemeVariant = claro ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    public static void Alternar() => Aplicar(!Claro);
}

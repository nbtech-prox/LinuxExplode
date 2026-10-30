using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>Idiomas da interface. O pt-PT é o original; espanhol e inglês são traduções.</summary>
public enum Idioma { Pt, Es, En }

/// <summary>
/// Textos da interface em pt-PT (origem), espanhol e inglês.
///
/// A chave é sempre o texto em pt-PT. Duas razões: o código continua legível e uma tradução em falta
/// aparece como português em vez de rebentar ou mostrar uma chave. A cultura acompanha o idioma
/// (pt-PT, es-ES, en-GB) para os números e as datas mudarem com ele.
/// </summary>
public static partial class Textos
{
    public static Idioma Atual { get; private set; } = Idioma.Pt;

    public static string Codigo => Atual switch { Idioma.Es => "es", Idioma.En => "en", _ => "pt" };

    public static string Nome => Atual switch { Idioma.Es => "Español", Idioma.En => "English", _ => "Português" };

    public static Idioma DeCodigo(string? codigo) => codigo?.Trim().ToLowerInvariant() switch
    {
        "es" or "es-es" or "español" or "espanol" => Idioma.Es,
        "en" or "en-gb" or "en-us" or "english" => Idioma.En,
        _ => Idioma.Pt
    };

    /// <summary>Idioma do sistema (LANG/LC_ALL); pt, es e en são reconhecidos, o resto fica em pt-PT.</summary>
    public static Idioma DoSistema()
    {
        var lang = Environment.GetEnvironmentVariable("LC_ALL");
        if (string.IsNullOrWhiteSpace(lang)) lang = Environment.GetEnvironmentVariable("LC_MESSAGES");
        if (string.IsNullOrWhiteSpace(lang)) lang = Environment.GetEnvironmentVariable("LANG");
        var curto = (lang ?? "").Split('.', '_', '-', ':')[0].ToLowerInvariant();
        return curto switch { "es" => Idioma.Es, "en" => Idioma.En, _ => Idioma.Pt };
    }

    /// <summary>Aplica o idioma à cultura do processo e, se pedido, grava a escolha.</summary>
    public static void Aplicar(Idioma idioma, bool guardar = false)
    {
        Atual = idioma;
        var cultura = new CultureInfo(idioma switch
        {
            Idioma.Es => "es-ES",
            Idioma.En => "en-GB",
            _ => "pt-PT"
        });
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;

        if (guardar)
        {
            Config.Atual.Idioma = Codigo;
            Config.Atual.Guardar();
        }
    }

    /// <summary>Traduz um texto fixo. Sem tradução devolve o português.</summary>
    public static string T(string pt)
    {
        if (Atual == Idioma.Pt || string.IsNullOrEmpty(pt)) return pt;
        var mapa = Atual == Idioma.Es ? Es : En;
        return mapa.TryGetValue(pt, out var traduzido) ? traduzido : pt;
    }

    /// <summary>Traduz um molde com marcadores ({0}, {1}) e formata-o com a cultura do idioma.</summary>
    public static string TF(string molde, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, T(molde), args);

    /// <summary>Quantas entradas cada idioma tem (usado pelos testes de cobertura).</summary>
    public static (int Es, int En) Cobertura => (Es.Count, En.Count);

    /// <summary>Chaves traduzidas num idioma (para os testes compararem os dois mapas).</summary>
    public static IReadOnlyCollection<string> Chaves(Idioma idioma)
        => (idioma == Idioma.Es ? Es : En).Keys.ToList();

    /// <summary>Tradução de uma chave sem cair para o português (null quando não existe).</summary>
    public static string? Bruto(Idioma idioma, string chave)
        => (idioma == Idioma.Es ? Es : En).TryGetValue(chave, out var v) ? v : null;
}

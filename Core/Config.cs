using System.Text.Json;

namespace LinuxExplode.Core;

/// <summary>
/// Preferências que o utilizador muda na interface (tarifa de energia, tema, escala). Ficam num JSON
/// pequeno em <c>~/.config/linux-explode/config.json</c>.
/// </summary>
public sealed class Config
{
    public double TarifaEurKwh { get; set; } = 0.20;
    public string Tema { get; set; } = "escuro";
    public double Escala { get; set; } = 1.0;

    // idioma da interface: "pt", "es" ou "en". Vazio = seguir o sistema na primeira execução.
    public string Idioma { get; set; } = "";

    // alertas
    public double LimiteCpuAviso { get; set; } = 90;
    public double LimiteGpuAviso { get; set; } = 90;
    public double EspacoMinimoGb { get; set; } = 10;
    public bool Notificacoes { get; set; }

    // bandeja do sistema
    public bool Bandeja { get; set; } = true;
    public bool BandejaFechar { get; set; } = true;

    private static Config? _atual;

    public static Config Atual => _atual ??= Carregar();

    /// <summary>Relê o ficheiro (usado pelos testes e quando o utilizador muda de pasta).</summary>
    public static void Recarregar() => _atual = Carregar();

    public static string Pasta =>
        Environment.GetEnvironmentVariable("LINUXEXPLODE_CONFIG") is { Length: > 0 } forcar
            ? forcar
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "linux-explode");

    public static string Caminho => Path.Combine(Pasta, "config.json");

    private static Config Carregar()
    {
        try
        {
            if (File.Exists(Caminho))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(Caminho)) ?? new Config();
        }
        catch { }
        return new Config();
    }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Pasta);
            File.WriteAllText(Caminho, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

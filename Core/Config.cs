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

    private static Config? _atual;

    public static Config Atual => _atual ??= Carregar();

    public static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "linux-explode");

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

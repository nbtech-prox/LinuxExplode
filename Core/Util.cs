using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace LinuxExplode.Core;

/// <summary>Leitura de ficheiros de sistema (/proc, /sys) e formatação comum.</summary>
public static class Util
{
    public static string Ler(string caminho, string omisso = "")
    {
        try { return File.Exists(caminho) ? File.ReadAllText(caminho).Trim() : omisso; }
        catch { return omisso; }
    }

    public static string? LerNulo(string caminho)
    {
        try { return File.Exists(caminho) ? File.ReadAllText(caminho).Trim() : null; }
        catch { return null; }
    }

    public static long LerLong(string caminho, long omisso = 0)
        => long.TryParse(Ler(caminho), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : omisso;

    public static double LerDouble(string caminho, double omisso = 0)
        => double.TryParse(Ler(caminho), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : omisso;

    /// <summary>Corre um comando do sistema e devolve stdout (vazio se não existir).</summary>
    public static string Comando(string ficheiro, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(ficheiro)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return "";
            string saida = p.StandardOutput.ReadToEnd();
            p.WaitForExit(8000);
            return saida.Trim();
        }
        catch { return ""; }
    }

    /// <summary>Caminho absoluto de um executável no PATH (null se não existir).</summary>
    public static string? CaminhoDe(string nome)
    {
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
        {
            if (string.IsNullOrWhiteSpace(d)) continue;
            try
            {
                var p = Path.Combine(d, nome);
                if (File.Exists(p)) return p;
            }
            catch { }
        }
        return null;
    }

    public static bool ExisteComando(string nome)
    {
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
        {
            if (string.IsNullOrWhiteSpace(d)) continue;
            try { if (File.Exists(Path.Combine(d, nome))) return true; } catch { }
        }
        return false;
    }

    public static string Bytes(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }

    public static string Gb(long bytes) => $"{bytes / 1073741824.0:0.0} GB";

    /// <summary>Verdadeiro quando a app corre dentro do sandbox Flatpak.</summary>
    public static readonly bool EmFlatpak = File.Exists("/.flatpak-info");

    /// <summary>Lê um ficheiro do anfitrião (dentro do Flatpak delega em flatpak-spawn).</summary>
    public static string LerHost(string caminho)
        => EmFlatpak && ExisteComando("flatpak-spawn") ? Comando("flatpak-spawn", "--host", "cat", caminho) : Ler(caminho);

    /// <summary>Parse de /proc/mounts → ponto de montagem de um dispositivo.</summary>
    public static Dictionary<string, (string Montagem, string Fs, string Opcoes)> Montagens()
    {
        var mapa = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal);
        foreach (var linha in LerHost("/proc/mounts").Split('\n'))
        {
            var p = linha.Split(' ');
            if (p.Length < 4) continue;
            var dev = Desescapar(p[0]);
            if (!mapa.ContainsKey(dev)) mapa[dev] = (Desescapar(p[1]), p[2], p[3]);
        }
        return mapa;
    }

    /// <summary>Ocupação real por ponto de montagem (df). No Flatpak pergunta ao anfitrião.</summary>
    public static Dictionary<string, (long Total, long Livre)> Espaco()
    {
        var mapa = new Dictionary<string, (long, long)>(StringComparer.Ordinal);
        var saida = EmFlatpak
            ? Comando("flatpak-spawn", "--host", "df", "-B1", "-P")
            : Comando("df", "-B1", "-P");
        foreach (var l in saida.Split('\n').Skip(1))
        {
            var p = l.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 6) continue;
            if (!long.TryParse(p[1], out var total) || !long.TryParse(p[3], out var livre)) continue;
            mapa[p[5]] = (total, livre);
        }
        return mapa;
    }

    private static string Desescapar(string s)
        => s.Replace("\\040", " ").Replace("\\011", "\t").Replace("\\134", "\\");

    /// <summary>Nome legível do SO a partir de /etc/os-release.</summary>
    public static (string Nome, string Versao, string Id) OsRelease()
    {
        string nome = "Linux", versao = "", id = "";
        // Dentro do Flatpak /etc/os-release é o do runtime: o do anfitrião está em /run/host/os-release.
        var ficheiro = "/etc/os-release";
        foreach (var candidato in new[] { "/run/host/os-release", "/run/host/etc/os-release" })
            if (File.Exists(candidato)) { ficheiro = candidato; break; }
        foreach (var l in Ler(ficheiro).Split('\n'))
        {
            var i = l.IndexOf('=');
            if (i <= 0) continue;
            var k = l[..i]; var v = l[(i + 1)..].Trim('"');
            switch (k)
            {
                case "PRETTY_NAME": nome = v; break;
                case "VERSION_ID": versao = v; break;
                case "ID": id = v; break;
            }
        }
        return (nome, versao, id);
    }

    public static string Uptime()
    {
        var bruto = Ler("/proc/uptime");
        var primeiro = bruto.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "0";
        double.TryParse(primeiro, NumberStyles.Float, CultureInfo.InvariantCulture, out var s);
        var t = TimeSpan.FromSeconds(s);
        return t.Days > 0 ? $"{t.Days}d {t.Hours}h {t.Minutes}m" : $"{t.Hours}h {t.Minutes}m";
    }

    public static string? PciNome(string vendorId, string deviceId)
    {
        // Tenta a base de dados do sistema (fora do sandbox), senão cai numa tabela pequena.
        foreach (var p in new[] { "/usr/share/hwdata/pci.ids", "/usr/share/misc/pci.ids", "/var/lib/pciutils/pci.ids" })
        {
            try
            {
                if (!File.Exists(p)) continue;
                string? vendor = null;
                foreach (var linha in File.ReadLines(p))
                {
                    if (linha.Length == 0 || linha[0] == '#') continue;
                    if (!char.IsWhiteSpace(linha[0]))
                    {
                        if (linha.StartsWith(vendorId, StringComparison.OrdinalIgnoreCase))
                            vendor = linha.Length > 6 ? linha[6..].Trim() : vendorId;
                        else vendor = null;
                    }
                    else if (vendor is not null && linha.StartsWith("\t") && !linha.StartsWith("\t\t")
                             && linha.TrimStart().StartsWith(deviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        var nome = linha.Trim()[6..].Trim();
                        return $"{vendor} {nome}";
                    }
                }
            }
            catch { }
        }
        return null;
    }

    public static string Temperatura(double celsius) => $"{celsius:0} °C";
}

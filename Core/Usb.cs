using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>Um dispositivo ligado ao barramento USB, lido de <c>/sys/bus/usb/devices</c>.</summary>
public sealed record DispositivoUsb(
    string Id,
    int Barramento,
    int Dispositivo,
    string IdVendor,
    string IdProduto,
    string? Fabricante,
    string? Produto,
    string? Serie,
    double? VelocidadeMbps,
    string? Versao,
    string? Potencia,
    bool Hub,
    List<string> Classes,
    List<string> Drivers)
{
    public string Nome
    {
        get
        {
            if (Produto is { Length: > 0 } p) return p;
            if (Fabricante is { Length: > 0 } f) return f + " (sem nome de produto)";

            // sem descritores de texto: deduz-se pelo driver/classe, que é informação do kernel
            if (Drivers.Contains("btusb")) return "Adaptador Bluetooth (sem descritor)";
            if (Drivers.Contains("usbhid")) return "Dispositivo HID (sem descritor)";
            if (Drivers.Contains("uvcvideo")) return "Câmara USB (sem descritor)";
            if (Drivers.Contains("snd-usb-audio")) return "Interface de áudio USB (sem descritor)";
            if (Drivers.Contains("usb-storage") || Drivers.Contains("uas")) return "Armazenamento USB (sem descritor)";
            return $"Dispositivo {IdVendor}:{IdProduto}";
        }
    }

    /// <summary>"1,5 Mbps (low speed)" … "20 Gbps (SuperSpeed+ 2x2)".</summary>
    public string TextoVelocidade => Usb.VelocidadeTexto(VelocidadeMbps);

    /// <summary>Tag curta do que o dispositivo faz ("áudio", "vídeo", "HID"…).</summary>
    public string TextoClasses => Classes.Count == 0 ? "" : string.Join(", ", Classes);

    public bool TemAudio => Classes.Contains("áudio");
}

/// <summary>
/// Dispositivos USB: o que está ligado, a que velocidade negociou e que driver tem cada interface.
/// A velocidade é a que o kernel reporta em <c>speed</c> — não é um teste de débito.
/// </summary>
public static class Usb
{
    public static List<DispositivoUsb> Listar(string raiz = "/sys/bus/usb/devices")
    {
        var lista = new List<DispositivoUsb>();
        if (!Directory.Exists(raiz)) return lista;

        foreach (var dir in Directory.GetDirectories(raiz).OrderBy(x => Ordem(Path.GetFileName(x))))
        {
            var vid = Util.Ler(dir + "/idVendor");
            if (vid.Length == 0) continue;                       // interface (10-6:1.0), não o dispositivo

            var id = Path.GetFileName(dir);
            var interfaces = Directory.GetDirectories(dir).Where(d => Path.GetFileName(d).Contains(':')).ToList();

            var classes = new List<string>();
            var drivers = new List<string>();
            foreach (var i in interfaces)
            {
                var c = Classe(Util.Ler(i + "/bInterfaceClass"));
                if (c.Length > 0 && !classes.Contains(c)) classes.Add(c);

                var d = Driver(i);
                if (d.Length > 0 && !drivers.Contains(d)) drivers.Add(d);
            }

            var speedLido = Util.LerDouble(dir + "/speed", -1);
            double? velocidade = speedLido > 0 ? speedLido : null;

            lista.Add(new DispositivoUsb(
                id,
                (int)Util.LerLong(dir + "/busnum"),
                (int)Util.LerLong(dir + "/devnum"),
                vid,
                Util.Ler(dir + "/idProduct"),
                Vazio(Util.LerNulo(dir + "/manufacturer")),
                Vazio(Util.LerNulo(dir + "/product")),
                Vazio(Util.LerNulo(dir + "/serial")),
                velocidade,
                Vazio(Util.LerNulo(dir + "/version")),
                Vazio(Util.LerNulo(dir + "/bMaxPower")),
                classes.Contains("hub"),
                classes,
                drivers));
        }

        return lista;
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Nome do driver da interface (link simbólico em /sys/bus/usb/devices/X:1.N/driver).</summary>
    private static string Driver(string dirInterface)
    {
        try
        {
            var alvo = new DirectoryInfo(dirInterface + "/driver").LinkTarget;
            return string.IsNullOrEmpty(alvo) ? "" : Path.GetFileName(alvo.TrimEnd('/'));
        }
        catch { return ""; }
    }

    /// <summary>Código de classe da interface USB → nome em pt-PT.</summary>
    public static string Classe(string hex) => hex.ToLowerInvariant() switch
    {
        "01" => "áudio",
        "02" => "comunicações",
        "03" => "HID",
        "05" => "físico",
        "06" => "imagem",
        "07" => "impressora",
        "08" => "armazenamento",
        "09" => "hub",
        "0a" => "CDC",
        "0b" => "smart card",
        "0d" => "conteúdo",
        "0e" => "vídeo",
        "0f" => "saúde",
        "dc" => "diagnóstico",
        "e0" => "sem fios",
        "ef" => "misc",
        "fe" => "específico da aplicação",
        "ff" => "específico do fabricante",
        _ => hex.Length > 0 && hex != "00" ? "classe " + hex : ""
    };

    public static string VelocidadeTexto(double? mbps)
    {
        if (mbps is not double v) return "velocidade não exposta";
        return v switch
        {
            <= 1.5 => "1,5 Mbps (low speed)",
            <= 12 => "12 Mbps (full speed)",
            <= 480 => "480 Mbps (high speed)",
            <= 5000 => "5 Gbps (USB 3.0)",
            <= 10000 => "10 Gbps (USB 3.1)",
            <= 20000 => "20 Gbps (USB 3.2 2x2)",
            _ => $"{v.ToString("0", CultureInfo.GetCultureInfo("pt-PT"))} Mbps"
        };
    }

    /// <summary>Ordem estável: por barramento e depois pelo caminho na árvore (1-1, 1-1.4, …).</summary>
    private static (int, int, string) Ordem(string id)
    {
        var primeiro = id.Split('-', ':')[0];
        int.TryParse(new string(primeiro.Where(char.IsDigit).ToArray()), out var barramento);
        var prof = id.Count(c => c == '.');
        return (barramento, prof, id);
    }

    /// <summary>Dispositivos com interface de áudio — usados no cartão de áudio.</summary>
    public static List<DispositivoUsb> ComAudio(List<DispositivoUsb>? todos = null)
        => (todos ?? Listar()).Where(d => d.TemAudio).ToList();

    /// <summary>Resumo de uma linha por dispositivo, para `--cli usb`.</summary>
    public static string Linha(DispositivoUsb d)
    {
        var extra = new List<string>();
        if (d.TextoClasses.Length > 0) extra.Add(d.TextoClasses);
        if (d.Drivers.Count > 0) extra.Add(string.Join("/", d.Drivers));
        if (d.Potencia is { Length: > 0 } p) extra.Add(p);
        return $"{d.Id,-10} {d.Nome}" +
               (d.Fabricante is { Length: > 0 } f && !d.Nome.Contains(f) ? $" · {f}" : "") +
               $"  [{d.IdVendor}:{d.IdProduto}]" +
               $"  {d.TextoVelocidade}" +
               (d.Versao is { Length: > 0 } v ? $" · USB {v}" : "") +
               (extra.Count > 0 ? "  " + string.Join(" · ", extra) : "");
    }
}

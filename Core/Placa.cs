using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>
/// Link PCI Express de um dispositivo: velocidade e largura que estão a ser usadas agora, contra o
/// máximo que o próprio dispositivo anuncia. Tudo lido de <c>/sys/bus/pci/devices/*/</c>.
/// </summary>
public sealed record LinkPcie(
    string Endereco,
    string Nome,
    string Classe,
    string CodigoClasse,
    double? GtsAtual,
    double? GtsMax,
    int? LarguraAtual,
    int? LarguraMax)
{
    /// <summary>Ponte PCIe, ponte do sistema ou host bridge — não é um dispositivo final.</summary>
    public bool Ponte => CodigoClasse.StartsWith("0x06", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// O link está a correr abaixo do que o dispositivo suporta. Só se avalia em dispositivos finais:
    /// numa ponte o valor actual é o do link de baixo, e é normal não coincidir com o máximo.
    /// </summary>
    public bool Reduzido => !Ponte &&
        ((GtsAtual is double a && GtsMax is double m && m > a) ||
         (LarguraAtual is int la && LarguraMax is int lm && lm > la));

    public string Geracao => Placa.Geracao(GtsAtual ?? GtsMax);

    /// <summary>"PCIe 4.0 x16".</summary>
    public string TextoLink
    {
        get
        {
            var g = Geracao;
            var largura = LarguraAtual is int l ? $"x{l}" : "x?";
            return g.Length > 0 ? $"PCIe {g} {largura}" : $"PCIe {largura}";
        }
    }

    /// <summary>Máximo do dispositivo, para comparar quando o link está reduzido.</summary>
    public string TextoMaximo
    {
        get
        {
            var g = Placa.Geracao(GtsMax);
            var largura = LarguraMax is int l ? $"x{l}" : "x?";
            var texto = g.Length > 0 ? $"PCIe {g} {largura}" : $"PCIe {largura}";
            return texto;
        }
    }

    /// <summary>Débito teórico do link actual (já descontada a codificação do barramento).</summary>
    public double? BandaTeoricaGbS => Placa.BandaGbS(GtsAtual, LarguraAtual);
}

/// <summary>Temperatura de um componente da placa (chipset, memória, rede, Wi-Fi, NVMe).</summary>
public sealed record TemperaturaPlaca(string Fonte, string Chip, string Nome, double Celsius)
{
    public string Texto => $"{Celsius:0.0} °C";
}

/// <summary>
/// O que a placa-mãe expõe além da CPU e da GPU: velocidade dos links PCI Express e as temperaturas
/// dos restantes chips. Quando um componente não tem sensor, o cartão di-lo — não se inventa nada.
/// </summary>
public static class Placa
{
    private static readonly Lazy<Dictionary<string, string>> _nomes = new(CarregarNomes);

    // ------------------------------------------------------------------ PCI Express

    /// <summary>Todos os dispositivos com link PCI Express negociado (ordem do endereço).</summary>
    public static List<LinkPcie> Links()
    {
        var lista = new List<LinkPcie>();
        const string raiz = "/sys/bus/pci/devices";
        if (!Directory.Exists(raiz)) return lista;

        foreach (var dir in Directory.GetDirectories(raiz).OrderBy(x => x, StringComparer.Ordinal))
        {
            var addr = Path.GetFileName(dir);
            var atual = Gts(Util.Ler(dir + "/current_link_speed"));
            if (atual <= 0) continue;                      // não é PCIe (PCI clássico, função virtual)

            int? larguraAtual = Largura(Util.LerLong(dir + "/current_link_width", 0));

            // current_link_speed vem como "16.0 GT/s PCIe"
            var maxLido = Gts(Util.Ler(dir + "/max_link_speed"));
            double? max = maxLido > 0 ? maxLido : null;

            var vendor = Util.Ler(dir + "/vendor");
            var device = Util.Ler(dir + "/device");
            var codigoClasse = Util.Ler(dir + "/class");
            var classe = Classe(codigoClasse);
            var nome = Nome(vendor, device) ?? ClasseTecnica(codigoClasse);

            lista.Add(new LinkPcie(
                addr.Length > 5 ? addr[5..] : addr,
                nome,
                classe,
                codigoClasse,
                atual,
                max,
                larguraAtual,
                Largura(Util.LerLong(dir + "/max_link_width", 0))));
        }
        return lista;
    }

    private static int? Largura(long v) => v > 0 ? (int)v : null;

    /// <summary>"16.0 GT/s PCIe" → 16,0. Zero quando o ficheiro não tem um número (link desligado).</summary>
    public static double Gts(string texto)
    {
        var primeiro = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return double.TryParse(primeiro, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>GT/s → geração PCIe (2,5 → 1.0 … 64 → 6.0). Vazio quando não reconhecida.</summary>
    public static string Geracao(double? gts) => gts switch
    {
        2.5 => "1.0",
        5.0 => "2.0",
        8.0 => "3.0",
        16.0 => "4.0",
        32.0 => "5.0",
        64.0 => "6.0",
        _ => ""
    };

    /// <summary>
    /// Débito útil teórico: GT/s × largura ÷ 8 bits, com a codificação do barramento
    /// (8b/10b até à geração 2, 128b/130b da geração 3 em diante).
    /// </summary>
    public static double? BandaGbS(double? gts, int? largura)
    {
        if (gts is not double g || g <= 0 || largura is not int l || l <= 0) return null;
        var codificacao = g >= 8 ? 128.0 / 130.0 : 0.8;
        return g * 1e9 * l * codificacao / 8.0 / 1e9;
    }

    // ------------------------------------------------------------------ temperaturas da placa

    /// <summary>
    /// Sensores de temperatura que não são CPU nem GPU, com a origem identificada pelo driver hwmon.
    /// </summary>
    public static List<TemperaturaPlaca> Temperaturas(List<Sensor>? sensores = null)
    {
        sensores ??= Core.Sensores.Ler();
        var lista = new List<TemperaturaPlaca>();

        foreach (var s in sensores.Where(x => x.Tipo == TipoSensor.Temperatura))
        {
            if (EhCpuOuGpu(s.Chip)) continue;
            var fonte = Fonte(s.Chip);
            lista.Add(new TemperaturaPlaca(fonte, s.Chip, s.Nome, s.Valor));
        }

        return lista
            .OrderBy(t => OrdemFonte(t.Fonte))
            .ThenBy(t => t.Chip, StringComparer.Ordinal)
            .ThenBy(t => t.Nome, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>O que esta máquina não expõe — dito por extenso em vez de mostrar zero.</summary>
    public static List<string> SemSensor(List<TemperaturaPlaca> temperaturas)
    {
        var faltas = new List<string>();
        if (!temperaturas.Any(t => t.Fonte == "Chipset"))
            faltas.Add("chipset");
        faltas.Add("VRM (fases de alimentação)");
        return faltas;
    }

    private static bool EhCpuOuGpu(string chip) =>
        chip.Contains("k10temp", StringComparison.OrdinalIgnoreCase) ||
        chip.Contains("coretemp", StringComparison.OrdinalIgnoreCase) ||
        chip.Contains("zenpower", StringComparison.OrdinalIgnoreCase) ||
        chip.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
        chip.Contains("amdgpu", StringComparison.OrdinalIgnoreCase) ||
        chip.Contains("nouveau", StringComparison.OrdinalIgnoreCase) ||
        chip.Contains("CPU", StringComparison.OrdinalIgnoreCase);

    /// <summary>Driver hwmon → que componente da placa é.</summary>
    public static string Fonte(string chip)
    {
        var c = chip.ToLowerInvariant();
        if (c.Contains("prom21") || c.Contains("chipset") || c.Contains("pch")) return "Chipset";
        if (c.Contains("spd5118") || c.Contains("dimm") || c.Contains("jc42")) return "Memória";
        if (c.Contains("r8169") || c.Contains("igc") || c.Contains("e1000") || c.Contains("tg3")) return "Rede";
        if (c.Contains("ath") || c.Contains("iwl") || c.Contains("mt7") || c.Contains("rtw")) return "Wi-Fi";
        if (c.Contains("nvme")) return "NVMe";
        if (c.Contains("nct") || c.Contains("it87") || c.Contains("asus") || c.Contains("acpitz")) return "Placa-mãe";
        return "Outro";
    }

    private static int OrdemFonte(string fonte) => fonte switch
    {
        "Chipset" => 0,
        "Placa-mãe" => 1,
        "Memória" => 2,
        "NVMe" => 3,
        "Rede" => 4,
        "Wi-Fi" => 5,
        _ => 6
    };

    // ------------------------------------------------------------------ nomes PCI

    private static string? Nome(string vendor, string device)
    {
        if (vendor.Length < 4 || device.Length < 4) return null;
        var chave = vendor[2..].ToLowerInvariant() + ":" + device[2..].ToLowerInvariant();
        return _nomes.Value.TryGetValue(chave, out var nome) ? nome : null;
    }

    /// <summary>
    /// Lê o pci.ids uma única vez. Dentro do Flatpak o ficheiro não existe no runtime, por isso pede-se
    /// ao anfitrião. Sem ele os dispositivos ficam identificados pela classe (nada fica em branco).
    /// </summary>
    private static Dictionary<string, string> CarregarNomes()
    {
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? texto = null;

        foreach (var p in new[] { "/usr/share/hwdata/pci.ids", "/usr/share/misc/pci.ids", "/var/lib/pciutils/pci.ids" })
        {
            try
            {
                if (File.Exists(p)) { texto = File.ReadAllText(p); break; }
            }
            catch { }
        }

        if (texto is null && Util.EmFlatpak)
            texto = Util.Comando("flatpak-spawn", "--host", "cat", "/usr/share/hwdata/pci.ids");

        if (string.IsNullOrEmpty(texto)) return mapa;

        string? vendorNome = null;
        string vendorId = "";
        foreach (var linha in texto.Split('\n'))
        {
            if (linha.Length == 0 || linha[0] == '#') continue;
            if (linha[0] == 'C') break;                       // fim da secção de dispositivos

            if (!char.IsWhiteSpace(linha[0]))
            {
                vendorId = linha.Length >= 4 ? linha[..4] : "";
                vendorNome = linha.Length > 6 ? linha[6..].Trim() : vendorId;
                continue;
            }

            if (vendorNome is null || linha.Length < 2 || linha[0] != '\t' || linha[1] == '\t') continue;

            var resto = linha.TrimStart('\t').Trim();
            if (resto.Length < 4) continue;
            var id = resto[..4];
            var nome = resto.Length > 6 ? resto[6..].Trim() : id;
            mapa.TryAdd(vendorId + ":" + id, $"{VendorCurto(vendorNome)} · {nome}");
        }
        return mapa;
    }

    /// <summary>
    /// Nome de fabricante curto: "Advanced Micro Devices, Inc. [AMD/ATI]" → "AMD/ATI",
    /// "Realtek Semiconductor Co., Ltd." → "Realtek". O pci.ids usa nomes legais compridos.
    /// </summary>
    public static string VendorCurto(string vendor)
    {
        var i = vendor.IndexOf('[');
        if (i >= 0)
        {
            var fim = vendor.IndexOf(']', i);
            if (fim > i + 1) return vendor[(i + 1)..fim].Trim();
        }
        var curto = vendor.Split(',')[0].Trim();
        var palavras = curto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palavras.Length > 1 && palavras[^1].Length <= 3 && !char.IsUpper(palavras[^1][0])
            ? string.Join(" ", palavras[..^1])
            : palavras.FirstOrDefault() ?? vendor;
    }

    /// <summary>Nome genérico a partir do código de classe PCI (0x030000, …).</summary>
    public static string Classe(string codigoHex)
    {
        if (codigoHex.Length < 4 || !codigoHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return "";
        var c = codigoHex.ToLowerInvariant();
        return c switch
        {
            var x when x.StartsWith("0x0604") => "Ponte PCIe",
            var x when x.StartsWith("0x0600") || x.StartsWith("0x0601") => "Ponte do sistema (host)",
            var x when x.StartsWith("0x03") => "Placa gráfica",
            var x when x.StartsWith("0x01") => "Armazenamento",
            var x when x.StartsWith("0x02") => "Rede",
            var x when x.StartsWith("0x04") => "Áudio",
            var x when x.StartsWith("0x0c") => "Barramento série (USB)",
            var x when x.StartsWith("0x05") => "Memória",
            var x when x.StartsWith("0x06") => "Ponte do sistema",
            var x when x.StartsWith("0x07") => "Comunicações",
            var x when x.StartsWith("0x08") => "Outro",
            var x when x.StartsWith("0x09") => "Entrada/saída",
            var x when x.StartsWith("0x0a") => "Docking",
            var x when x.StartsWith("0x0b") => "Processador",
            var x when x.StartsWith("0x0d") => "Rádio",
            var x when x.StartsWith("0x0f") => "Satélite",
            var x when x.StartsWith("0x10") => "Controlador de cifra",
            var x when x.StartsWith("0x11") => "Coprocessador",
            var x when x.StartsWith("0x12") => "Acelerador de I/O",
            var x when x.StartsWith("0x13") => "Processamento de sinais",
            _ => ""
        };
    }

    /// <summary>Classe técnica do PCI-SIG, quando o pci.ids não está disponível.</summary>
    private static string ClasseTecnica(string codigoHex)
    {
        var c = Classe(codigoHex);
        if (c.Length > 0) return c;
        var sub = codigoHex.Length >= 8 ? codigoHex[6..8].ToLowerInvariant() : "";
        return sub switch
        {
            "00" => "Dispositivo PCI",
            "80" => "Dispositivo PCI",
            _ => "Dispositivo PCI " + (codigoHex.Length >= 4 ? codigoHex[2..4] : "")
        };
    }

    /// <summary>Resumo para a linha de comandos: detecta links a correr abaixo do máximo.</summary>
    public static List<LinkPcie> Reduzidos(List<LinkPcie>? links = null) => (links ?? Links()).Where(l => l.Reduzido).ToList();

    /// <summary>Formata um número em pt-PT sem depender da cultura do chamador.</summary>
    public static string Numero(double v, int casas = 1) => v.ToString("0." + new string('0', casas), CultureInfo.GetCultureInfo("pt-PT"));
}

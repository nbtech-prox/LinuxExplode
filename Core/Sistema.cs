using System.Globalization;

namespace LinuxExplode.Core;

public sealed record Campo(string Nome, string Valor);

public sealed record GpuInfo(string Nome, string Pci, string Driver, string? Temp, string? Uso, long? VramTotal, long? VramUsada);

/// <summary>Informação do sistema lida de /proc, /sys e DMI — sem root e sem WMI.</summary>
public sealed class Sistema
{
    public string So { get; private set; } = "";
    public string Kernel { get; private set; } = "";
    public string Arquitetura { get; private set; } = "";
    public string Hostname { get; private set; } = "";
    public string Uptime { get; private set; } = "";

    public string CpuModelo { get; private set; } = "";
    public int NucleosFisicos { get; private set; }
    public int Threads { get; private set; }
    public string CpuFreqAtual { get; private set; } = "";
    public string CpuFreqMax { get; private set; } = "";
    public string CpuFreqPico { get; private set; } = "";
    public string CpuFreqMinAtual { get; private set; } = "";
    public string CpuFreqMinimo { get; private set; } = "";
    public FrequenciaCpu Freq { get; private set; } = new();
    public string CpuCache { get; private set; } = "";

    public long RamTotal { get; private set; }
    public long RamLivre { get; private set; }
    public long SwapTotal { get; private set; }
    public long SwapUsada { get; private set; }

    public string PlacaMae { get; private set; } = "";
    public string Bios { get; private set; } = "";
    public string Maquina { get; private set; } = "";

    public List<GpuInfo> Gpus { get; } = new();

    /// <summary>Módulos de memória com SPD legível (fabricante, part number, velocidade).</summary>
    public List<ModuloRam> Modulos { get; } = new();

    public static Sistema Recolher()
    {
        var s = new Sistema();
        var os = Util.OsRelease();
        s.So = os.Nome;
        s.Kernel = Util.Ler("/proc/sys/kernel/osrelease");
        s.Arquitetura = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
        s.Hostname = Environment.MachineName;
        s.Uptime = Util.Uptime();

        // ---- CPU
        var cpuinfo = Util.Ler("/proc/cpuinfo");
        foreach (var linha in cpuinfo.Split('\n'))
        {
            if (linha.StartsWith("model name", StringComparison.Ordinal))
            {
                s.CpuModelo = linha[(linha.IndexOf(':') + 1)..].Trim();
                break;
            }
            if (s.CpuModelo.Length == 0 && linha.StartsWith("Model", StringComparison.Ordinal))
                s.CpuModelo = linha[(linha.IndexOf(':') + 1)..].Trim();
        }
        s.Threads = 0;
        foreach (var linha in cpuinfo.Split('\n'))
            if (linha.StartsWith("processor", StringComparison.Ordinal)) s.Threads++;

        // Núcleos físicos: core id distintos por package (quando disponível)
        var nucleos = new HashSet<string>();
        string? pacote = null, core = null;
        foreach (var linha in cpuinfo.Split('\n'))
        {
            if (linha.StartsWith("physical id", StringComparison.Ordinal))
                pacote = linha[(linha.IndexOf(':') + 1)..].Trim();
            else if (linha.StartsWith("core id", StringComparison.Ordinal))
                core = linha[(linha.IndexOf(':') + 1)..].Trim();
            else if (linha.Trim().Length == 0 && core is not null)
            {
                nucleos.Add($"{pacote}:{core}");
                pacote = core = null;
            }
        }
        s.NucleosFisicos = nucleos.Count > 0 ? nucleos.Count : Math.Max(1, s.Threads / 2);

        // frequência: média, pico e mínimo actuais por thread (também guarda o detalhe em Freq)
        s.Freq = FrequenciaCpu.Recolher();
        s.CpuFreqAtual = s.Freq.Disponivel ? FrequenciaCpu.Ghz(s.Freq.MediaAtualKHz) : "—";
        s.CpuFreqPico = s.Freq.Disponivel ? FrequenciaCpu.Ghz(s.Freq.MaxAtualKHz) : "—";
        s.CpuFreqMinAtual = s.Freq.Disponivel ? FrequenciaCpu.Ghz(s.Freq.MinAtualKHz) : "—";
        s.CpuFreqMax = s.Freq.TurboKHz > 0 ? FrequenciaCpu.Ghz(s.Freq.TurboKHz) : "—";
        s.CpuFreqMinimo = s.Freq.MinimoKHz > 0 ? FrequenciaCpu.Ghz(s.Freq.MinimoKHz) : "—";

        long cacheL3 = 0;
        try
        {
            foreach (var idx in Directory.GetDirectories("/sys/devices/system/cpu/cpu0/cache"))
            {
                if (Util.LerLong(idx + "/level") == 3)
                {
                    var size = Util.Ler(idx + "/size");
                    cacheL3 = ParseTam(size);
                    break;
                }
            }
        }
        catch { }
        s.CpuCache = cacheL3 > 0 ? Util.Bytes(cacheL3) : "—";

        // ---- RAM
        foreach (var linha in Util.Ler("/proc/meminfo").Split('\n'))
        {
            var p = linha.Split(':', 2);
            if (p.Length < 2) continue;
            var kb = long.TryParse(p[1].Trim().Split(' ')[0], out var v) ? v * 1024 : 0;
            switch (p[0])
            {
                case "MemTotal": s.RamTotal = kb; break;
                case "MemAvailable": s.RamLivre = kb; break;
                case "SwapTotal": s.SwapTotal = kb; break;
                case "SwapFree": s.SwapUsada = Math.Max(0, s.SwapTotal - kb); break;
            }
        }

        // ---- DMI
        string D(string f) => Util.Ler("/sys/class/dmi/id/" + f);
        var fabricantePlaca = D("board_vendor");
        var modeloPlaca = D("board_name");
        var versaoPlaca = D("board_version");
        s.PlacaMae = string.Join(" ", new[] { fabricantePlaca, modeloPlaca, versaoPlaca }
            .Where(x => !string.IsNullOrWhiteSpace(x) && x != "To be filled by O.E.M."));
        s.Bios = string.Join(" · ", new[] { D("bios_vendor"), D("bios_version"), D("bios_date") }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        s.Maquina = string.Join(" · ", new[] { D("sys_vendor"), D("product_name") }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        s.Gpus.AddRange(RecolherGpus());
        s.Modulos.AddRange(MemoriaModulos.Ler());
        return s;
    }

    private static long ParseTam(string size)
    {
        // formato do sysfs: "32K", "1024K", "8M"
        if (string.IsNullOrEmpty(size)) return 0;
        var n = size[..^1];
        var u = char.ToUpperInvariant(size[^1]);
        if (!long.TryParse(n, out var v)) return 0;
        return u switch { 'K' => v * 1024, 'M' => v * 1024 * 1024, 'G' => v * 1024 * 1024 * 1024, _ => v };
    }

    private static IEnumerable<GpuInfo> RecolherGpus()
    {
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? dirDrm = "/sys/class/drm";
        if (!Directory.Exists(dirDrm)) yield break;

        foreach (var card in Directory.GetDirectories(dirDrm).Where(d =>
                     System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(d), "^card[0-9]+$")))
        {
            var dev = Path.Combine(card, "device");
            if (!Directory.Exists(dev)) continue;                 // conectores, não a GPU
            var pci = Path.GetFileName(Path.GetFullPath(dev));
            try
            {
                var alvo = new DirectoryInfo(dev).LinkTarget;
                if (!string.IsNullOrEmpty(alvo)) pci = alvo.TrimEnd('/').Split('/').Last();
            }
            catch { }
            if (!vistos.Add(pci)) continue;

            var vendorId = Util.Ler(dev + "/vendor").Replace("0x", "");
            var deviceId = Util.Ler(dev + "/device").Replace("0x", "");
            var driver = "";
            try
            {
                var link = new DirectoryInfo(dev + "/driver");
                if (link.Exists) driver = link.LinkTarget?.TrimEnd('/').Split('/').Last() ?? "";
            }
            catch { }

            string nome = Util.PciNome(vendorId, deviceId) ?? $"{NomeVendor(vendorId)} ({vendorId}:{deviceId})";

            // Temperatura / uso pela hwmon da própria GPU
            string? temp = null, uso = null;
            long? vramTotal = null, vramUsada = null;
            try
            {
                foreach (var h in Directory.GetDirectories(dev + "/hwmon"))
                {
                    var t = Util.LerLong(h + "/temp1_input");
                    if (t > 0) temp = $"{t / 1000.0:0} °C";
                    var b = Util.LerLong(h + "/power1_average");
                    if (b > 0) uso ??= null;
                }
                var busy = Util.LerLong(dev + "/gpu_busy_percent");
                if (busy > 0) uso = $"{busy} %";
                var vt = Util.LerLong(dev + "/mem_info_vram_total");
                var vu = Util.LerLong(dev + "/mem_info_vram_used");
                if (vt > 0) { vramTotal = vt; vramUsada = vu; }
            }
            catch { }

            yield return new GpuInfo(nome, pci, driver.Length > 0 ? driver : "—", temp, uso, vramTotal, vramUsada);
        }
    }

    private static string NomeVendor(string id) => id.ToLowerInvariant() switch
    {
        "1002" => "AMD/ATI",
        "10de" => "NVIDIA",
        "8086" => "Intel",
        "1af4" => "Red Hat (virtio)",
        _ => "Dispositivo"
    };
}

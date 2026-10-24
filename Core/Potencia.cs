using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>GPU com contador de potência exposto pelo hwmon (amdgpu/i915/xe/nouveau…).</summary>
public sealed record PotenciaGpu(string Slot, string Nome, double Watts, bool Integrada)
{
    /// <summary>Endereço PCI, ou o nome do driver quando o dispositivo não está em PCI.</summary>
    public string Etiqueta => Slot.Length > 0 ? Slot : Nome;
}

/// <summary>Instantâneo do consumo energético do sistema.</summary>
public sealed record LeituraPotencia
{
    /// <summary>Pacote da CPU (domínio RAPL <c>package-N</c>), em watts.</summary>
    public double? PacoteW { get; init; }

    /// <summary>Núcleos da CPU (domínio RAPL <c>core</c>) — já contido no pacote.</summary>
    public double? NucleosW { get; init; }

    /// <summary>Plataforma inteira (domínio RAPL <c>psys</c>), quando a máquina o expõe.</summary>
    public double? PlataformaW { get; init; }

    /// <summary>Energia acumulada no pacote desde o arranque (kWh), lida do contador RAPL.</summary>
    public double? EnergiaKWh { get; init; }

    public List<PotenciaGpu> Gpus { get; init; } = new();

    /// <summary>Existe pelo menos um domínio RAPL legível.</summary>
    public bool TemRapl { get; init; }

    /// <summary>Explicação quando falta alguma coisa (permissões, ausência de RAPL).</summary>
    public string? Aviso { get; init; }

    public bool TemAlgo => PacoteW is not null || PlataformaW is not null || Gpus.Count > 0;

    /// <summary>
    /// Total estimado: usa <c>psys</c> quando existe (plataforma completa); caso contrário
    /// soma o pacote da CPU com as GPUs dedicadas (a GPU integrada já está dentro do pacote).
    /// </summary>
    public double? TotalW => PlataformaW
        ?? (PacoteW is null ? null : PacoteW + Gpus.Where(g => !g.Integrada).Sum(g => g.Watts));

    /// <summary>Rótulo do total, para a interface ser honesta sobre o que está somado.</summary>
    public string TotalRotulo => PlataformaW is not null ? "plataforma (psys)" : "CPU + GPU dedicada";

    public string TotalTexto
    {
        get
        {
            var t = TotalW;
            return t is null ? "—" : $"{t.Value.ToString("0.0", CultureInfo.InvariantCulture)} W";
        }
    }
}

/// <summary>
/// Potência de consumo a partir de /sys/class/powercap (RAPL: energia acumulada em µJ, convertida
/// em watts pela diferença entre leituras) e de /sys/class/hwmon (power1_* das GPUs).
/// </summary>
public static class Potencia
{
    private const string RaizPowercap = "/sys/class/powercap";
    private const string RaizHwmon = "/sys/class/hwmon";

    /// <summary>Raiz do powercap — sobreponível por LINUXEXPLODE_POWERCAP para testes.</summary>
    private static string RaizPowercapAtual =>
        Environment.GetEnvironmentVariable("LINUXEXPLODE_POWERCAP") is { Length: > 0 } p ? p : RaizPowercap;

    /// <summary>Peso da amostra nova na média móvel — o RAPL instantâneo oscila muito.</summary>
    private const double Alfa = 0.4;

    /// <summary>Intervalo mínimo entre amostras para o delta ser válido.</summary>
    private const double DtMinimoMs = 100;

    private readonly record struct Amostra(long Uj, long Nanos);

    private static readonly object _trava = new();
    private static readonly Dictionary<string, Amostra> _anteriores = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> _medias = new(StringComparer.Ordinal);

    /// <summary>Formata watts; abaixo de 1 W passa a miliwatts (a GPU integrada reporta valores mínimos).</summary>
    public static string Watts(double? w) =>
        w is null ? "—" : w.Value >= 1 ? $"{w.Value:0.0} W" : $"{w.Value * 1000:0} mW";

    public static LeituraPotencia Ler()
    {
        lock (_trava)
        {
            var dominios = LerDominios(out bool rapl, out bool semPermissao, out double? energiaKWh);

            var pacote = Somar(dominios, n => n.StartsWith("package", StringComparison.Ordinal));
            var nucleos = Somar(dominios, n => n == "core" || n.StartsWith("core_", StringComparison.Ordinal));
            var plataforma = Somar(dominios, n => n == "psys" || n.StartsWith("psys_", StringComparison.Ordinal));

            var gpus = LerGpus();

            string? aviso = null;
            if (!rapl && semPermissao)
                aviso = "O RAPL da CPU só permite leitura ao root (protecção do kernel contra canais laterais). " +
                        "Corre uma vez:  sudo ./permitir-energia.sh";
            else if (!rapl)
                aviso = "Este kernel não expõe contadores RAPL (/sys/class/powercap vazio) — sem leitura de potência da CPU.";

            if (pacote is null && gpus.Count == 0 && aviso is null)
                aviso = "Ainda sem amostras suficientes (precisa de duas leituras com ≥0,1 s de intervalo).";

            return new LeituraPotencia
            {
                PacoteW = pacote,
                NucleosW = nucleos,
                PlataformaW = plataforma,
                EnergiaKWh = energiaKWh,
                Gpus = gpus,
                TemRapl = rapl,
                Aviso = aviso
            };
        }
    }

    /// <summary>Energia acumulada do pacote em kWh (contador RAPL absoluto, não é delta).</summary>
    private static List<(string Nome, double? Watts)> LerDominios(out bool rapl, out bool semPermissao, out double? energiaKWh)
    {
        var lista = new List<(string, double?)>();
        rapl = false;
        semPermissao = false;
        energiaKWh = null;

        var raiz = RaizPowercapAtual;
        if (!Directory.Exists(raiz)) return lista;

        foreach (var dir in Directory.GetDirectories(raiz).OrderBy(x => x, StringComparer.Ordinal))
        {
            var ficheiro = Path.Combine(dir, "energy_uj");
            if (!File.Exists(ficheiro)) continue;   // /sys/class/powercap/intel-rapl (raiz) não tem contador

            var nome = Util.Ler(Path.Combine(dir, "name")).ToLowerInvariant();
            if (nome.Length == 0) nome = Path.GetFileName(dir).ToLowerInvariant();

            long uj;
            try
            {
                uj = long.Parse(File.ReadAllText(ficheiro).Trim(), CultureInfo.InvariantCulture);
            }
            catch (UnauthorizedAccessException) { semPermissao = true; continue; }
            catch { continue; }

            rapl = true;

            if (nome.StartsWith("package", StringComparison.Ordinal))
                energiaKWh = (energiaKWh ?? 0) + uj / 3_600_000_000_000.0;   // µJ → kWh (3,6e12 µJ por kWh)

            var max = Util.LerLong(Path.Combine(dir, "max_energy_range_uj"));
            lista.Add((nome, PotenciaDe(Path.GetFileName(dir), uj, max)));
        }

        return lista;
    }

    /// <summary>Watts a partir de duas leituras do contador cumulativo (µJ). Devolve null na 1.ª amostra.</summary>
    private static double? PotenciaDe(string chave, long uj, long max)
    {
        var agora = new Amostra(uj, Environment.TickCount64 * 1_000_000);
        if (!_anteriores.TryGetValue(chave, out var antes))
        {
            _anteriores[chave] = agora;
            return null;   // precisa de duas leituras
        }
        _anteriores[chave] = agora;

        double dt = agora.Nanos - antes.Nanos;
        if (dt < DtMinimoMs * 1e6) return _medias.TryGetValue(chave, out var m) ? m : null;

        double delta = agora.Uj - antes.Uj;
        if (delta < 0)
        {
            // contador de 32 bits: dá a volta em max_energy_range_uj (≈ 65,5 kJ nestas máquinas)
            if (max <= 0) return _medias.TryGetValue(chave, out var m2) ? m2 : null;
            delta += max;
            if (delta < 0) return _medias.TryGetValue(chave, out var m3) ? m3 : null;
        }

        // µJ / ns × 1000 = W  (1 µJ/µs = 1 W)
        double watts = delta * 1000.0 / dt;
        if (watts < 0 || watts > 2000) return _medias.TryGetValue(chave, out var m4) ? m4 : null;

        if (_medias.TryGetValue(chave, out var antiga)) watts = Alfa * watts + (1 - Alfa) * antiga;
        _medias[chave] = watts;
        return watts;
    }

    private static double? Somar(List<(string Nome, double? Watts)> dominios, Func<string, bool> filtro)
    {
        double soma = 0;
        bool algum = false;
        foreach (var (nome, w) in dominios)
        {
            if (!filtro(nome) || w is null) continue;
            soma += w.Value;
            algum = true;
        }
        return algum ? soma : null;
    }

    private static List<PotenciaGpu> LerGpus()
    {
        var lista = new List<PotenciaGpu>();
        if (!Directory.Exists(RaizHwmon)) return lista;

        foreach (var dir in Directory.GetDirectories(RaizHwmon).OrderBy(x => x, StringComparer.Ordinal))
        {
            var chip = Util.Ler(Path.Combine(dir, "name")).ToLowerInvariant();
            if (!Gpu(chip)) continue;

            var fMedia = Path.Combine(dir, "power1_average");
            var fInput = Path.Combine(dir, "power1_input");
            var ficheiro = File.Exists(fMedia) ? fMedia : (File.Exists(fInput) ? fInput : null);
            if (ficheiro is null) continue;

            var micro = Util.LerLong(ficheiro);
            if (micro <= 0) continue;

            var real = CaminhoReal(Path.Combine(CaminhoReal(dir), "device"));
            var slot = SlotPci(real);
            lista.Add(new PotenciaGpu(slot, NomeGpu(chip), micro / 1e6, Integrada(real)));
        }

        return lista;
    }

    private static bool Gpu(string chip) => chip is "amdgpu" or "amdgpu_hwmon" or "radeon"
        or "i915" or "xe" or "nvidia" or "nouveau";

    private static string NomeGpu(string chip) => chip switch
    {
        "amdgpu" or "amdgpu_hwmon" or "radeon" => "AMD",
        "i915" or "xe" => "Intel",
        "nvidia" or "nouveau" => "NVIDIA",
        _ => chip
    };

    /// <summary>
    /// Caminho real de um caminho em /sys, seguindo os symlinks à mão. <c>ResolveLinkTarget</c>
    /// falha nestas ligações relativas em cadeia (hwmon → device), por isso segue-se <c>LinkTarget</c>.
    /// </summary>
    private static string CaminhoReal(string caminho)
    {
        try
        {
            caminho = Path.GetFullPath(caminho);
            for (int i = 0; i < 8; i++)
            {
                var alvo = new DirectoryInfo(caminho).LinkTarget;
                if (string.IsNullOrEmpty(alvo)) break;
                caminho = Path.GetFullPath(Path.IsPathRooted(alvo)
                    ? alvo
                    : Path.Combine(Path.GetDirectoryName(caminho) ?? "/", alvo));
            }
        }
        catch { }
        return caminho.TrimEnd('/');
    }

    /// <summary>Endereço PCI (ex.: 03:00.0) a partir do caminho real do dispositivo.</summary>
    private static string SlotPci(string caminhoReal)
    {
        var nome = Path.GetFileName(caminhoReal);
        if (nome.Length >= 7 && nome[4] == ':' && char.IsDigit(nome[0])) return nome[5..];
        foreach (var parte in caminhoReal.Split('/'))
            if (parte.Length >= 7 && parte[4] == ':' && char.IsDigit(parte[0])) return parte[5..];
        return "";
    }

    /// <summary>
    /// GPU integrada: o dispositivo pendura directamente num bridge da barca 0 da CPU
    /// (ex.: 7b:00.0 atrás de 00:08.1). Uma dedicada fica atrás de bidges/switch próprios (02:00.0…).
    /// </summary>
    private static bool Integrada(string caminhoReal)
    {
        var pai = Path.GetFileName(Path.GetDirectoryName(caminhoReal) ?? "");
        return pai.Length >= 7 && pai.StartsWith("0000:00:", StringComparison.Ordinal);
    }
}

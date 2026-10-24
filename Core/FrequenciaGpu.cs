using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>Relógios da GPU em Hz (0 quando o driver não expõe ou está em repouso).</summary>
public sealed record FreqGpu(string Nome, string Pci, long NucleoHz, long NucleoMaxHz, long MemoriaHz, long MemoriaMaxHz)
{
    public bool TemLeitura => NucleoHz > 0 || NucleoMaxHz > 0 || MemoriaHz > 0;

    /// <summary>Nome sem o prefixo do fabricante e truncado — o nome completo do pci.ids não cabe no cartão.</summary>
    public string NomeCurto
    {
        get
        {
            var n = Nome;
            var fecha = n.IndexOf(']', StringComparison.Ordinal);
            if (fecha > 0 && fecha + 1 < n.Length) n = n[(fecha + 1)..].Trim();
            if (n.Length > 44)
            {
                var corte = n.LastIndexOf(' ', 44);
                if (corte < 20) corte = 44;
                n = n[..corte].TrimEnd(' ', '/') + "…";
            }
            return n;
        }
    }
}

/// <summary>
/// Relógio de GPU lido de sysfs, sem root e sem ferramentas externas:
/// amdgpu expõe hwmon/freq1_input (sclk) e freq2_input (mclk) em Hz e as tabelas pp_dpm_sclk/
/// pp_dpm_mclk (com um '*' na linha activa); Intel expõe gt_cur_freq_mhz. NVIDIA não expõe
/// relógios em sysfs — nesse caso não se inventa nada.
/// </summary>
public static class FrequenciaGpu
{
    public static List<FreqGpu> Ler(IEnumerable<GpuInfo> gpus)
    {
        var lista = new List<FreqGpu>();
        foreach (var g in gpus.OrderByDescending(x => x.VramTotal ?? 0))
        {
            var pci = "/sys/bus/pci/devices/" + g.Pci;
            if (!Directory.Exists(pci)) continue;

            long nucleo = 0, memoria = 0;
            foreach (var hw in Hwmons(pci))
            {
                for (int i = 1; i <= 4; i++)
                {
                    var hz = Util.LerLong($"{hw}/freq{i}_input");
                    if (hz <= 0) continue;
                    var rotulo = Util.Ler($"{hw}/freq{i}_label").ToLowerInvariant();
                    // sem rótulo: convenção do amdgpu (1 = sclk, 2 = mclk)
                    if (rotulo.Length == 0) rotulo = i == 1 ? "sclk" : i == 2 ? "mclk" : "";
                    if (rotulo is "sclk" or "gt") { if (nucleo == 0) nucleo = hz; }
                    else if (rotulo is "mclk" or "mem") { if (memoria == 0) memoria = hz; }
                }
            }

            // Intel i915/xe: MHz em vez de hwmon
            if (nucleo == 0)
            {
                var mhz = Util.LerLong(pci + "/gt_cur_freq_mhz");
                if (mhz > 0) nucleo = mhz * 1_000_000;
            }
            var maxIntel = Util.LerLong(pci + "/gt_max_freq_mhz") * 1_000_000;

            var (maxSclk, atualSclk) = TabelaDpm(pci + "/pp_dpm_sclk");
            var (maxMclk, atualMclk) = TabelaDpm(pci + "/pp_dpm_mclk");

            // o hwmon pode reportar 0 em repouso (clock gating): a tabela diz qual é o nível activo
            if (nucleo == 0 && atualSclk > 0) nucleo = atualSclk;
            if (memoria == 0 && atualMclk > 0) memoria = atualMclk;

            var maxNucleo = Math.Max(maxSclk, maxIntel);
            if (maxNucleo == 0) maxNucleo = nucleo;
            var maxMemoria = maxMclk > 0 ? maxMclk : memoria;

            var r = new FreqGpu(g.Nome, g.Pci, nucleo, maxNucleo, memoria, maxMemoria);
            if (r.TemLeitura) lista.Add(r);
        }
        return lista;
    }

    /// <summary>Máximo e valor activo (de "S: 0Mhz *" ou "2: 2855Mhz *"), em Hz.</summary>
    private static (long Max, long Atual) TabelaDpm(string caminho)
    {
        long max = 0, atual = 0;
        foreach (var linha in Util.Ler(caminho).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = linha.IndexOf(':');
            if (i <= 0) continue;
            var valor = linha[(i + 1)..].Trim();
            var ativo = valor.EndsWith('*');
            var num = new string(valor.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
            if (!long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mhz)) continue;
            if (mhz > max) max = mhz;
            if (ativo) atual = mhz;
        }
        return (max * 1_000_000, atual * 1_000_000);
    }

    private static IEnumerable<string> Hwmons(string pci)
    {
        var baseHw = Path.Combine(pci, "hwmon");
        if (!Directory.Exists(baseHw)) yield break;
        foreach (var d in Directory.GetDirectories(baseHw, "hwmon*")) yield return d;
    }

    /// <summary>MHz para leitura de GPU (é a unidade em que se fala destes relógios).</summary>
    public static string Mhz(long hz) => hz > 0 ? $"{hz / 1_000_000.0:0} MHz" : "0 MHz";
}

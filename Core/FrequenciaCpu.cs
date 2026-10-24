using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>Frequência de um thread lógico (kHz) e o núcleo físico a que pertence.</summary>
public sealed record FreqThread(int Cpu, int Pacote, int Nucleo, long AtualKHz, long MaxKHz);

/// <summary>Núcleo físico e os threads que o partilham.</summary>
public sealed record FreqNucleo(int Pacote, int Nucleo, IReadOnlyList<FreqThread> Threads)
{
    public double MediaKHz => Threads.Count == 0 ? 0 : Threads.Average(t => (double)t.AtualKHz);
    public long PicoKHz => Threads.Count == 0 ? 0 : Threads.Max(t => t.AtualKHz);
}

/// <summary>
/// Frequência por thread lida de /sys/devices/system/cpu/cpuN/cpufreq.
/// Sem root e sem executar nada; o agrupamento por núcleo físico vem de topology/core_id.
/// Cai para o campo "cpu MHz" de /proc/cpuinfo em kernels sem cpufreq exposto.
/// </summary>
public sealed class FrequenciaCpu
{
    public List<FreqThread> Threads { get; } = new();
    public string Driver { get; private set; } = "";
    public string Governador { get; private set; } = "";
    public long TurboKHz { get; private set; }
    public long MinimoKHz { get; private set; }
    public long MinAtualKHz { get; private set; }
    public long MaxAtualKHz { get; private set; }
    public long MediaAtualKHz { get; private set; }

    public bool Disponivel => Threads.Count > 0;
    public int NucleosFisicos => Threads.Select(t => (t.Pacote, t.Nucleo)).Distinct().Count();

    /// <summary>Fração do turbo (0..1) para barras.</summary>
    public double Fracao(long khz) => TurboKHz > 0 ? Math.Clamp((double)khz / TurboKHz, 0, 1) : 0;

    public IReadOnlyList<FreqNucleo> PorNucleo() => Threads
        .GroupBy(t => (t.Pacote, t.Nucleo))
        .OrderBy(g => g.Key.Pacote).ThenBy(g => g.Key.Nucleo)
        .Select(g => new FreqNucleo(g.Key.Pacote, g.Key.Nucleo, g.OrderBy(t => t.Cpu).ToList()))
        .ToList();

    public static string Ghz(long khz) => khz > 0 ? $"{khz / 1_000_000.0:0.00} GHz" : "—";

    /// <summary>Só o número (sem "GHz"), para linhas secundárias onde o espaço é curto.</summary>
    public static string GhzCurto(long khz) => khz > 0 ? $"{khz / 1_000_000.0:0.00}" : "—";

    public static FrequenciaCpu Recolher()
    {
        var r = new FrequenciaCpu();
        var topologia = TopologiaDoCpuinfo();
        var mhz = MhzDoCpuinfo();

        foreach (var cpu in CpusOnline())
        {
            var baseCpu = $"/sys/devices/system/cpu/cpu{cpu}";
            var cp = baseCpu + "/cpufreq";

            long atual = Util.LerLong(cp + "/scaling_cur_freq");
            if (atual <= 0) atual = Util.LerLong(cp + "/cpuinfo_cur_freq");
            if (atual <= 0) atual = Util.LerLong(cp + "/cpuinfo_avg_freq");
            if (atual <= 0 && mhz.TryGetValue(cpu, out var valorMhz)) atual = (long)(valorMhz * 1000);
            if (atual <= 0) continue;

            long max = Util.LerLong(cp + "/cpuinfo_max_freq");
            if (max <= 0) max = Util.LerLong(cp + "/scaling_max_freq");
            if (max <= 0) max = atual;

            if (r.Driver.Length == 0) r.Driver = Util.Ler(cp + "/scaling_driver");
            if (r.Governador.Length == 0) r.Governador = Util.Ler(cp + "/scaling_governor");

            var minimo = Util.LerLong(cp + "/cpuinfo_min_freq");
            if (minimo > 0 && (r.MinimoKHz == 0 || minimo < r.MinimoKHz)) r.MinimoKHz = minimo;
            if (max > r.TurboKHz) r.TurboKHz = max;

            // topologia: sysfs primeiro, /proc/cpuinfo como alternativa
            long pacote = Util.LerLong(baseCpu + "/topology/physical_package_id", -1);
            long nucleo = Util.LerLong(baseCpu + "/topology/core_id", -1);
            if (nucleo < 0 && topologia.TryGetValue(cpu, out var t)) { pacote = t.Pacote; nucleo = t.Nucleo; }
            if (nucleo < 0) { pacote = 0; nucleo = cpu; }

            r.Threads.Add(new FreqThread(cpu, (int)pacote, (int)nucleo, atual, max));
        }

        if (r.Threads.Count > 0)
        {
            r.MinAtualKHz = r.Threads.Min(t => t.AtualKHz);
            r.MaxAtualKHz = r.Threads.Max(t => t.AtualKHz);
            r.MediaAtualKHz = (long)r.Threads.Average(t => t.AtualKHz);
        }
        return r;
    }

    /// <summary>Lista de CPUs online (expande "0-15" e "0-7,16-23").</summary>
    private static List<int> CpusOnline()
    {
        var lista = new List<int>();
        var texto = Util.Ler("/sys/devices/system/cpu/online");
        if (texto.Length == 0)
        {
            for (int i = 0; i < Math.Max(1, Environment.ProcessorCount); i++) lista.Add(i);
            return lista;
        }
        foreach (var parte in texto.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var extremos = parte.Split('-');
            if (extremos.Length == 2 && int.TryParse(extremos[0], out var a) && int.TryParse(extremos[1], out var b))
            {
                for (int i = a; i <= b && i - a < 1024; i++) lista.Add(i);
            }
            else if (int.TryParse(extremos[0], out var v)) lista.Add(v);
        }
        return lista;
    }

    /// <summary>"cpu MHz" de /proc/cpuinfo por processador (usado quando não há cpufreq).</summary>
    private static Dictionary<int, double> MhzDoCpuinfo()
    {
        var mapa = new Dictionary<int, double>();
        int? atual = null;
        foreach (var linha in Util.Ler("/proc/cpuinfo").Split('\n'))
        {
            var i = linha.IndexOf(':');
            if (i <= 0) continue;
            var chave = linha[..i].Trim();
            var valor = linha[(i + 1)..].Trim();
            if (chave == "processor" && int.TryParse(valor, out var n)) atual = n;
            else if (chave == "cpu MHz" && atual is int c
                     && double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                mapa[c] = v;
        }
        return mapa;
    }

    /// <summary>physical id / core id por processador, lidos de /proc/cpuinfo.</summary>
    private static Dictionary<int, (int Pacote, int Nucleo)> TopologiaDoCpuinfo()
    {
        var mapa = new Dictionary<int, (int, int)>();
        int? atual = null, pacote = 0, nucleo = -1;
        foreach (var linha in Util.Ler("/proc/cpuinfo").Split('\n'))
        {
            if (linha.Trim().Length == 0)
            {
                if (atual is int c && nucleo >= 0) mapa[c] = ((int)pacote, (int)nucleo);
                atual = null; nucleo = -1; continue;
            }
            var i = linha.IndexOf(':');
            if (i <= 0) continue;
            var chave = linha[..i].Trim();
            var valor = linha[(i + 1)..].Trim();
            switch (chave)
            {
                case "processor": if (int.TryParse(valor, out var n)) atual = n; break;
                case "physical id": if (int.TryParse(valor, out var p)) pacote = p; break;
                case "core id": if (int.TryParse(valor, out var k)) nucleo = k; break;
            }
        }
        if (atual is int ultimo && nucleo >= 0) mapa[ultimo] = ((int)pacote, (int)nucleo);
        return mapa;
    }
}

namespace LinuxExplode.Core;

/// <summary>Como está a correr o CPU: frequência média, tecto e quedas por temperatura/potência.</summary>
public sealed record LeituraThrottle(
    double MediaMhz,
    double MaximoMhz,
    int Nucleos,
    double Percentagem,
    double MinimoMhz,
    long Episodios,
    double SegundosEmQueda,
    double CargaCpu,
    long? ContadorFirmware,
    string Veredito);

/// <summary>
/// Deteta throttling comparando a frequência efectiva (<c>scaling_cur_freq</c>) com o máximo do
/// processador (<c>cpuinfo_max_freq</c>) enquanto a carga está alta. Quando o hardware expõe
/// contadores de firmware (<c>thermal_throttle/*</c>), também os reporta.
/// </summary>
public sealed class MonitorThrottle
{
    private long _ociosoAnterior = -1, _totalAnterior = -1;
    private bool _emQueda;
    private DateTime _inicioQueda;
    private int _nucleos;

    public long Episodios { get; private set; }
    public double SegundosEmQueda { get; private set; }
    public double MinimoMhz { get; private set; } = double.MaxValue;

    public LeituraThrottle Amostrar()
    {
        double carga = CargaCpu();
        var (media, maximo, nucleos) = Frequencias();
        _nucleos = nucleos;

        MinimoMhz = Math.Min(MinimoMhz, media);
        double percentagem = maximo > 0 ? media / maximo * 100 : 0;

        // "Queda" = frequência 15 % abaixo do máximo com o CPU ocupado (mais de metade dos cores).
        bool emQueda = carga > 0.5 && maximo > 0 && media < maximo * 0.85;

        if (emQueda && !_emQueda)
        {
            Episodios++;
            _inicioQueda = DateTime.Now;
        }
        else if (!emQueda && _emQueda)
        {
            SegundosEmQueda += (DateTime.Now - _inicioQueda).TotalSeconds;
        }
        _emQueda = emQueda;

        long? firmware = ContadorFirmware();

        string veredito = Episodios == 0
            ? "sem throttling"
            : $"{Episodios} episódio(s) · {SegundosEmQueda:0} s no total";

        return new LeituraThrottle(media, maximo, nucleos, percentagem,
            MinimoMhz == double.MaxValue ? media : MinimoMhz, Episodios, SegundosEmQueda, carga, firmware, veredito);
    }

    /// <summary>Ocupação da CPU desde a amostra anterior (0 a 1).</summary>
    private double CargaCpu()
    {
        try
        {
            var linha = File.ReadLines("/proc/stat").First();
            var campos = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(long.Parse).ToArray();
            long ocioso = campos[3] + (campos.Length > 4 ? campos[4] : 0);
            long total = campos.Sum();

            if (_totalAnterior < 0) { _ociosoAnterior = ocioso; _totalAnterior = total; return 0; }

            long dTotal = total - _totalAnterior;
            long dOcioso = ocioso - _ociosoAnterior;
            _ociosoAnterior = ocioso;
            _totalAnterior = total;

            return dTotal <= 0 ? 0 : Math.Clamp(1.0 - (double)dOcioso / dTotal, 0, 1);
        }
        catch { return 0; }
    }

    private static (double Media, double Maximo, int Nucleos) Frequencias()
    {
        double soma = 0;
        double maximo = 0;
        int n = 0;

        foreach (var pasta in Directory.EnumerateDirectories("/sys/devices/system/cpu"))
        {
            // "cpu[0-9]*" não é um padrão válido em .NET (só * e ?), por isso o filtro é feito aqui
            string nome = Path.GetFileName(pasta);
            if (!nome.StartsWith("cpu", StringComparison.Ordinal) ||
                !int.TryParse(nome.AsSpan(3), out _)) continue;

            double cur = Util.LerLong(Path.Combine(pasta, "cpufreq", "scaling_cur_freq"));
            double max = Util.LerLong(Path.Combine(pasta, "cpufreq", "cpuinfo_max_freq"));
            if (cur <= 0) continue;

            soma += cur / 1000.0;             // kHz → MHz
            maximo = Math.Max(maximo, max / 1000.0);
            n++;
        }

        return (n == 0 ? 0 : soma / n, maximo, n);
    }

    private static long? ContadorFirmware()
    {
        // Presente em Intel; em AMD normalmente não existe — devolvemos null em vez de inventar 0.
        foreach (var nome in new[] { "core_throttle_count", "package_throttle_count" })
        {
            string caminho = Path.Combine("/sys/devices/system/cpu/cpu0/thermal_throttle", nome);
            if (File.Exists(caminho))
                return Util.LerLong(caminho);
        }
        return null;
    }

    public int Nucleos => _nucleos;
}

/// <summary>Energia de uma sessão de monitorização e custo estimado ao preço do kWh.</summary>
public sealed record CustoEnergia(double Wh, double MediaW, double Horas, double? EurDia, double? EurMes, string? Aviso);

/// <summary>
/// Mede energia integrando a potência lida (RAPL) ao longo do tempo em que a aplicação está aberta.
/// Não se usa <c>energy_uj</c> directamente porque o contador desta plataforma dá a volta aos
/// ~65,5 kJ (poucos minutos a 200 W) — somar uma leitura daria um valor falso.
/// </summary>
public sealed class MedidorEnergia
{
    private DateTime _inicio = DateTime.Now;
    private DateTime _ultimo = DateTime.Now;
    private double _joules;
    private int _amostras;

    public void Amostrar(double? watts)
    {
        var agora = DateTime.Now;

        if (watts is double w && _amostras > 0)
        {
            double segundos = Math.Clamp((agora - _ultimo).TotalSeconds, 0, 5);   // ignora pausas longas
            _joules += w * segundos;
        }

        if (watts is not null) _amostras++;
        _ultimo = agora;
    }

    public CustoEnergia Calcular(double tarifaEurKwh)
    {
        double horas = Math.Max(0, (DateTime.Now - _inicio).TotalHours);

        if (_amostras == 0)
            return new CustoEnergia(0, 0, horas, null, null, "sem leitura de potência (RAPL)");

        double segundos = horas * 3600;
        double media = segundos > 0.1 ? _joules / segundos : 0;
        double wh = _joules / 3600;
        double dia = media * 24 / 1000.0 * tarifaEurKwh;

        return new CustoEnergia(wh, media, horas, dia, dia * 30, null);
    }
}

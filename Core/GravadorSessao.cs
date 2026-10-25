using System.Globalization;
using System.Text;

namespace LinuxExplode.Core;

/// <summary>
/// Uma amostra da sessão gravada: valores medidos e o instante. Qualquer série pode faltar (sensores
/// que esta máquina não tem) — fica `null` no CSV como campo vazio, nunca zero.
/// </summary>
public sealed record AmostraSessao(
    DateTime Quando,
    double? CpuC,
    double? GpuC,
    double? Watts,
    double? FreqMhz);

/// <summary>
/// Resumo de uma sessão gravada: duração, número de amostras e mínimos/máximos/médias das séries que
/// existiram. Serve para o cartão dizer o que aconteceu sem obrigar a abrir o CSV.
/// </summary>
public sealed record ResumoSessao(
    TimeSpan Duracao,
    int Amostras,
    (double? Min, double? Max, double? Media) Cpu,
    (double? Min, double? Max, double? Media) Gpu,
    (double? Min, double? Max, double? Media) Watts,
    (double? Min, double? Max, double? Media) Freq)
{
    public static (double? Min, double? Max, double? Media) Calcular(IEnumerable<double?> serie)
    {
        var v = serie.Where(x => x is not null).Select(x => x!.Value).ToList();
        if (v.Count == 0) return (null, null, null);
        return (v.Min(), v.Max(), v.Average());
    }
}

/// <summary>
/// Grava uma sessão de monitorização em CSV (uma amostra por segundo) e no fim devolve o resumo.
/// O ficheiro vai para <c>~/.local/share/linux-explode/sessoes/</c> com data e hora no nome, para que
/// duas sessões nunca se sobreponham. A escrita é incremental: se a aplicação for fechada a meio, o
/// que já foi medido não se perde.
/// </summary>
public sealed class GravadorSessao : IDisposable
{
    private readonly object _cadeado = new();
    private readonly List<AmostraSessao> _amostras = new();
    private StreamWriter? _escritor;
    private bool _falhouEscrita;

    public GravadorSessao(string? pasta = null)
    {
        Pasta = pasta ?? Path.Combine(Historico.Pasta, "sessoes");
    }

    public string Pasta { get; }

    /// <summary>Caminho do CSV da sessão em curso (null antes de começar).</summary>
    public string? Caminho { get; private set; }

    public bool A_Gravar => _escritor is not null;

    public int Amostras
    {
        get { lock (_cadeado) return _amostras.Count; }
    }

    /// <summary>Diz se o CSV não pôde ser escrito (disco cheio, permissões) — o cartão avisa em vez de fingir.</summary>
    public bool FalhouEscrita => _falhouEscrita;

    /// <summary>Começa a sessão e escreve já o cabeçalho do CSV.</summary>
    public string Iniciar()
    {
        lock (_cadeado)
        {
            PararEscritor();

            Directory.CreateDirectory(Pasta);
            Caminho = Path.Combine(Pasta, $"sessao-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            _amostras.Clear();
            _falhouEscrita = false;

            try
            {
                _escritor = new StreamWriter(Caminho, append: false, new UTF8Encoding(false));
                _escritor.WriteLine("quando;cpu_c;gpu_c;watts;freq_mhz");
                _escritor.Flush();
            }
            catch
            {
                _falhouEscrita = true;
                _escritor = null;
            }

            return Caminho;
        }
    }

    /// <summary>Lê os sensores agora, guarda a amostra em memória e acrescenta-a ao CSV.</summary>
    public AmostraSessao Amostrar()
    {
        var amostra = LerAgora();

        lock (_cadeado)
        {
            _amostras.Add(amostra);

            if (_escritor is not null)
            {
                try
                {
                    _escritor.WriteLine(Linha(amostra, ';'));
                    _escritor.Flush();
                }
                catch
                {
                    _falhouEscrita = true;
                    PararEscritor();
                }
            }
        }

        return amostra;
    }

    /// <summary>Termina a sessão e devolve as amostras + o resumo. Sem amostras, o resumo vem vazio.</summary>
    public (IReadOnlyList<AmostraSessao> Amostras, ResumoSessao Resumo) Parar()
    {
        lock (_cadeado)
        {
            PararEscritor();

            var copia = _amostras.ToList();
            var duracao = copia.Count > 1 ? copia[^1].Quando - copia[0].Quando : TimeSpan.Zero;

            var resumo = new ResumoSessao(
                duracao,
                copia.Count,
                ResumoSessao.Calcular(copia.Select(a => a.CpuC)),
                ResumoSessao.Calcular(copia.Select(a => a.GpuC)),
                ResumoSessao.Calcular(copia.Select(a => a.Watts)),
                ResumoSessao.Calcular(copia.Select(a => a.FreqMhz)));

            return (copia, resumo);
        }
    }

    /// <summary>
    /// Uma leitura de todos os sensores de que a sessão precisa. As temperaturas vêm de
    /// <see cref="Sensores.CpuTemperatura"/> e <see cref="Sensores.GpuTemperatura"/> (as mesmas do resto
    /// da aplicação); a potência é a medida (RAPL + GPUs) e a frequência é a média dos núcleos.
    /// </summary>
    public static AmostraSessao LerAgora()
    {
        List<Sensor>? sensores = null;
        try { sensores = Sensores.Ler(); } catch { }

        double? cpu = sensores is null ? null : Sensores.CpuTemperatura(sensores);
        double? gpu = sensores is null ? null : Sensores.GpuTemperatura(sensores);

        double? watts = null;
        try
        {
            var leitura = Potencia.Ler();
            watts = leitura.TotalW;
        }
        catch { }

        double? freq = null;
        try
        {
            var f = FrequenciaCpu.Recolher();
            freq = f.MediaAtualKHz > 0 ? f.MediaAtualKHz / 1000.0 : null;
        }
        catch { }

        return new AmostraSessao(DateTime.Now, cpu, gpu, watts, freq);
    }

    /// <summary>Linha do CSV. `InvariantCulture` e ponto decimal: é um ficheiro de dados, não a interface.</summary>
    public static string Linha(AmostraSessao a, char separador = ';')
    {
        static string N(double? v) => v is null ? "" : v.Value.ToString("0.###", CultureInfo.InvariantCulture);

        return string.Join(separador,
            a.Quando.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            N(a.CpuC), N(a.GpuC), N(a.Watts), N(a.FreqMhz));
    }

    /// <summary>Cabeçalho do CSV, para os testes e para quem for ler o ficheiro.</summary>
    public const string Cabecalho = "quando;cpu_c;gpu_c;watts;freq_mhz";

    /// <summary>Escreve uma lista de amostras num CSV (usado no teste e no exportar).</summary>
    public static string EscreverCsv(IReadOnlyList<AmostraSessao> amostras, string caminho)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Cabecalho);
        foreach (var a in amostras) sb.AppendLine(Linha(a));

        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, sb.ToString(), new UTF8Encoding(false));
        return caminho;
    }

    private void PararEscritor()
    {
        try { _escritor?.Dispose(); } catch { }
        _escritor = null;
    }

    public void Dispose()
    {
        lock (_cadeado) PararEscritor();
    }
}

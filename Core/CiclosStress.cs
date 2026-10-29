using System.Diagnostics;

namespace LinuxExplode.Core;

/// <summary>Uma métrica agregada dos ciclos: mediana/min/max/variação, ou total quando é um contador.</summary>
public sealed record MetricaCiclos(string Chave, string Rotulo, double Mediana, double Minimo, double Maximo,
    double VariacaoPct, int Amostras, bool Contador);

/// <summary>O que fazer em cada ciclo: qual o alvo, quanto tempo dura e quanto se descansa entre ciclos.</summary>
public sealed record OpcoesCiclos(AlvoStress Alvo, int Ciclos, TimeSpan Duracao, TimeSpan Pausa,
    int Threads = 0, CargaCpu Carga = CargaCpu.Vector, double FracaoRam = 0.5, string? PastaDisco = null,
    long TamanhoDisco = 512L * 1024 * 1024, int IndiceGpu = 0);

/// <summary>
/// Resultado de uma série de ciclos. A nota de cada ciclo fica na lista; o agregado é a mediana — o mesmo
/// princípio do ranking: uma corrida isolada não é resultado, e a variação diz se a máquina esteve estável.
/// </summary>
public sealed record ResultadoCiclos(
    AlvoStress Alvo,
    int Pedidos,
    int Executados,
    int Completos,
    int Falhas,
    bool Parado,
    string? MotivoParagem,
    double PicoCpuC,
    double PicoGpuC,
    double EnergiaKwh,
    double Segundos,
    List<ResultadoStress> Lista,
    List<MetricaCiclos> Agregado,
    List<string> Linhas)
{
    public string Resumo => Parado
        ? $"parado no ciclo {Executados}/{Pedidos}"
        : Falhas == 0
            ? $"{Completos}/{Pedidos} ciclos completos"
            : $"{Completos}/{Pedidos} ciclos completos · {Falhas} falha(s) — {MotivoParagem}";
}

/// <summary>
/// Corre o mesmo teste N vezes, com pausa entre ciclos, e agrega os números. Um teste de estabilidade
/// não é um teste longo: são várias corridas iguais, para ver se a máquina repete o resultado ou se
/// degrada (térmica, throttling, erro de memória).
/// </summary>
public sealed class SessaoCiclos : IDisposable
{
    private readonly OpcoesCiclos _opcoes;
    private readonly CancellationTokenSource _cancelar = new();
    private Thread? _thread;
    private SessaoStress? _atual;

    public event Action<ProgressoStress>? Progresso;
    public event Action<ResultadoCiclos>? Terminou;

    public bool EmCurso => _thread is { IsAlive: true };

    public SessaoCiclos(OpcoesCiclos opcoes)
    {
        if (opcoes.Ciclos < 1) throw new ArgumentOutOfRangeException(nameof(opcoes));
        if (opcoes.Duracao <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(opcoes));
        _opcoes = opcoes;
    }

    public void Iniciar()
    {
        if (EmCurso) throw new InvalidOperationException("já há uma série de ciclos a correr");
        _thread = new Thread(Correr) { IsBackground = true, Name = "stress-ciclos" };
        _thread.Start();
    }

    public void Parar()
    {
        try { _cancelar.Cancel(); } catch { }
        try { _atual?.Parar(); } catch { }
    }

    private void Correr()
    {
        var relogio = Stopwatch.StartNew();
        var lista = new List<ResultadoStress>();
        double picoCpu = 0, picoGpu = 0, energia = 0;
        bool parado = false;
        string? motivo = null;

        for (int i = 0; i < _opcoes.Ciclos; i++)
        {
            if (_cancelar.IsCancellationRequested) { parado = true; motivo ??= "parado pelo utilizador"; break; }

            string ciclo = $"ciclo {i + 1}/{_opcoes.Ciclos}";
            using var sessao = new SessaoStress(_opcoes.Alvo, _opcoes.Duracao, _opcoes.Threads, _opcoes.Carga,
                _opcoes.FracaoRam, _opcoes.PastaDisco, _opcoes.TamanhoDisco, _opcoes.IndiceGpu);

            _atual = sessao;
            ResultadoStress? resultado = null;
            var fim = new ManualResetEventSlim(false);

            sessao.Progresso += p =>
            {
                // o stress interior ainda publica um progresso depois de terminar: não pode chegar
                // à série já fechada (no CLI aparecia uma linha "ciclo N" depois do "concluído")
                if (fim.IsSet) return;
                Progresso?.Invoke(new ProgressoStress(
                    $"{ciclo} · {p.Fase}", relogio.Elapsed.TotalSeconds, p.Ritmo, p.Detalhe));
            };
            sessao.Terminou += r => { resultado = r; fim.Set(); };

            sessao.Iniciar();

            // espera com atenção ao pedido de paragem: parar o ciclo a meio é parar a série
            while (!fim.Wait(250))
                if (_cancelar.IsCancellationRequested) sessao.Parar();

            _atual = null;
            if (resultado is null)
            {
                parado = true;
                motivo = "o ciclo não devolveu resultado";
                break;
            }

            lista.Add(resultado);
            picoCpu = Math.Max(picoCpu, resultado.PicoCpuC);
            picoGpu = Math.Max(picoGpu, resultado.PicoGpuC);
            energia += resultado.EnergiaKwh;
            Progresso?.Invoke(new ProgressoStress(
                $"{ciclo} · {(resultado.Completo ? "concluído" : "falhou")} — {resultado.Resumo}",
                relogio.Elapsed.TotalSeconds, "", Detalhe()));

            // cycle that stopped for the thermal guard ends the series: continuing would keep heating
            if (!resultado.Completo && !_cancelar.IsCancellationRequested)
            {
                motivo = resultado.Resumo;
                break;
            }

            if (_cancelar.IsCancellationRequested) { parado = true; motivo ??= "parado pelo utilizador"; break; }

            if (i < _opcoes.Ciclos - 1 && _opcoes.Pausa > TimeSpan.Zero)
            {
                var fimPausa = DateTime.UtcNow + _opcoes.Pausa;
                while (DateTime.UtcNow < fimPausa)
                {
                    if (_cancelar.IsCancellationRequested) { parado = true; motivo ??= "parado pelo utilizador"; break; }
                    var resta = fimPausa - DateTime.UtcNow;
                    Progresso?.Invoke(new ProgressoStress(
                        $"{ciclo} feito · a pausar", relogio.Elapsed.TotalSeconds,
                        $"faltam {Math.Max(0, resta.TotalSeconds):0} s", Detalhe()));
                    Thread.Sleep(250);
                }
                if (parado) break;
            }
        }

        var agregado = Agregar(lista);
        var linhas = new List<string>();

        for (int i = 0; i < lista.Count; i++)
        {
            var r = lista[i];
            linhas.Add($"ciclo {i + 1}/{_opcoes.Ciclos}: {(r.Completo ? "completo" : "parado")} em {r.Segundos:0} s" +
                       $" · pico CPU {r.PicoCpuC:0} °C · GPU {r.PicoGpuC:0} °C · {r.EnergiaKwh * 1000:0.0} Wh");
        }

        foreach (var m in agregado)
        {
            linhas.Add(m.Contador
                ? $"{m.Rotulo}: total {m.Mediana:0} em {m.Amostras} ciclo(s)"
                : $"{m.Rotulo}: mediana {m.Mediana:N1} · min {m.Minimo:N1} · max {m.Maximo:N1}" +
                  (m.Amostras >= 3 ? $" · variação {m.VariacaoPct:0.0} %" : $" · {m.Amostras} ciclo(s), amostra pequena"));
        }

        var instavel = agregado.FirstOrDefault(m => !m.Contador && m.Amostras >= 3 && m.VariacaoPct > 3);
        if (instavel is not null)
            linhas.Add($"aviso: {instavel.Rotulo} variou {instavel.VariacaoPct:0.0} % entre ciclos — " +
                       "a máquina não repetiu o resultado; vale a mediana e a causa (térmica, turbo, outro processo) tem de ser vista");

        int completos = lista.Count(r => r.Completo);
        var resultadoFinal = new ResultadoCiclos(_opcoes.Alvo, _opcoes.Ciclos, lista.Count, completos,
            lista.Count - completos, parado, motivo, picoCpu, picoGpu, energia,
            relogio.Elapsed.TotalSeconds, lista, agregado, linhas);

        Terminou?.Invoke(resultadoFinal);
    }

    private static string Detalhe()
    {
        var partes = new List<string>();
        try
        {
            if (Sensores.CpuTemperatura() is double c) partes.Add($"CPU {c:0} °C");
            if (Sensores.GpuTemperatura() is double g) partes.Add($"GPU {g:0} °C");
            if (Potencia.Ler().TotalW is double w) partes.Add($"{w:0} W");
        }
        catch { }
        return string.Join(" · ", partes);
    }

    /// <summary>Mediana/min/max/variação por métrica; contadores (erros, falhas) somam-se.</summary>
    public static List<MetricaCiclos> Agregar(List<List<(string Chave, double Valor)>> porCiclo)
    {
        var chaves = porCiclo.SelectMany(c => c.Select(m => m.Chave)).Distinct().ToList();
        var lista = new List<MetricaCiclos>();

        foreach (var chave in chaves)
        {
            var valores = porCiclo.Select(c => c.FirstOrDefault(m => m.Chave == chave))
                .Where(m => m.Chave is not null)
                .Select(m => m.Valor)
                .ToList();
            if (valores.Count == 0) continue;

            bool contador = chave.EndsWith("_erros", StringComparison.Ordinal) ||
                            chave.EndsWith("_falhas", StringComparison.Ordinal) ||
                            chave.EndsWith("falhadas", StringComparison.Ordinal);

            if (contador)
            {
                double total = valores.Sum();
                lista.Add(new MetricaCiclos(chave, Rotulo(chave), total, valores.Min(), valores.Max(), 0, valores.Count, true));
                continue;
            }

            double mediana = Historico.Mediana(valores) ?? 0;
            // com menos de 3 ciclos não se distingue ruído de problema: a variação só conta a partir daí
            double variacao = valores.Count >= 3 ? Historico.Variacao(valores) : 0;
            lista.Add(new MetricaCiclos(chave, Rotulo(chave), mediana, valores.Min(), valores.Max(), variacao,
                valores.Count, false));
        }

        return lista;
    }

    private static List<MetricaCiclos> Agregar(List<ResultadoStress> lista)
        => Agregar(lista.Select(r => (r.Metricas ?? new Dictionary<string, double>())
            .Select(kv => (kv.Key, kv.Value)).ToList()).ToList());

    private static string Rotulo(string chave) => chave switch
    {
        "cpu_milhoes_s" => "CPU (M/s)",
        "cpu_gflops" => "CPU (GFLOP/s)",
        "cpu_gbs_trafego" => "CPU↔RAM (GB/s)",
        "ram_escrita_gbs" => "RAM escrita (GB/s)",
        "ram_leitura_gbs" => "RAM leitura (GB/s)",
        "ram_erros" => "erros de RAM",
        "gpu_milhoes_pts_s" => "GPU (M pts/s)",
        "gpu_falhas" => "falhas de GPU",
        "disco_escrita_mbs" => "disco escrita (MB/s)",
        "disco_leitura_mbs" => "disco leitura (MB/s)",
        "disco_iops4k" => "4 KB (IOPS)",
        "disco_erros" => "bytes diferentes na releitura",
        "tarefas_falhadas" => "tarefas falhadas",
        _ => chave
    };

    public void Dispose()
    {
        Parar();
        _cancelar.Dispose();
    }
}

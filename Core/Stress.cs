using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LinuxExplode.Core;

/// <summary>Tipo de carga que os workers de CPU aplicam.</summary>
public enum CargaCpu
{
    /// <summary>Mistura inteira (xorshift + multiplicações) — aquece ALUs e desloca bits.</summary>
    Inteiro,

    /// <summary>Vírgula flutuante transcendental (sin/cos/sqrt) — unidades FP e escalonamento.</summary>
    Flutuante,

    /// <summary>FMA vetorial (SIMD, 8 floats por vector quando AVX2 está disponível).</summary>
    Vector,

    /// <summary>Leitura e escrita sequencial em memória — controlador e barramento de RAM.</summary>
    Memoria
}

/// <summary>O que está a ser stressado.</summary>
public enum AlvoStress { Cpu, Ram, Gpu, Disco, Tudo }

/// <summary>Instantâneo publicado durante o stress (para a interface).</summary>
public sealed record ProgressoStress(string Fase, double Segundos, string Ritmo, string Detalhe);

/// <summary>Resultado de uma sessão de stress.</summary>
public sealed record ResultadoStress(
    AlvoStress Alvo,
    bool Completo,
    string Resumo,
    List<string> Linhas,
    double PicoCpuC,
    double PicoGpuC,
    double EnergiaKwh,
    double Segundos);

/// <summary>
/// Guarda térmica: para a sessão quando a CPU ou a GPU passam do limite. Os valores são lidos dos
/// mesmos sensores do resto da aplicação (k10temp / amdgpu), não de estimativas.
/// </summary>
public static class GuardaTermica
{
    public static double LimiteCpu { get; set; } = 95;
    public static double LimiteGpu { get; set; } = 95;

    public static (bool Ok, string Motivo, double? Cpu, double? Gpu) Avaliar()
    {
        double? cpu = null, gpu = null;
        try { cpu = Sensores.CpuTemperatura(); } catch { }
        try { gpu = Sensores.GpuTemperatura(); } catch { }

        if (cpu is double c && c >= LimiteCpu)
            return (false, $"guarda térmica: CPU a {c:0} °C (limite {LimiteCpu:0} °C)", cpu, gpu);

        if (gpu is double g && g >= LimiteGpu)
            return (false, $"guarda térmica: GPU a {g:0} °C (limite {LimiteGpu:0} °C)", cpu, gpu);

        return (true, "", cpu, gpu);
    }
}

/// <summary>Carga de CPU: N threads a trabalhar até ao cancelamento.</summary>
public static class StressCpu
{
    /// <summary>Resultado de uma carga: unidades contadas, o que é uma unidade, e o que se mediu.</summary>
    public sealed record Resultado(long Unidades, string Unidade, double Bytes, double Flops, double Segundos);

    public static Resultado Correr(int threads, CargaCpu carga, CancellationToken ct, Action<long>? aoProgresso = null)
    {
        if (threads <= 0) threads = Environment.ProcessorCount;

        // StrongBox: o contador é um campo de um objecto, por isso `ref` funciona em todos os
        // closures e o progresso é visível a meio da carga (não só no fim).
        var total = new StrongBox<long>(0);
        var relogio = Stopwatch.StartNew();
        var trabalhadores = new Thread[threads];

        // Buffer partilhado para a carga de memória (64 MB): maior do que a cache L3, obriga a ir à RAM.
        byte[]? buffer = carga == CargaCpu.Memoria ? GC.AllocateArray<byte>(64 * 1024 * 1024, pinned: false) : null;

        for (int i = 0; i < threads; i++)
        {
            int semente = i + 1;
            trabalhadores[i] = new Thread(() =>
            {
                try
                {
                    Trabalhar(carga, ct, semente, buffer,
                        n => Interlocked.Add(ref total.Value, n));
                }
                catch (OperationCanceledException) { }
            })
            { IsBackground = true, Name = $"stress-cpu-{i}" };
            trabalhadores[i].Start();
        }

        var aviso = new Timer(_ => aoProgresso?.Invoke(Interlocked.Read(ref total.Value)), null, 200, 200);

        foreach (var t in trabalhadores) t.Join();
        aviso.Dispose();
        relogio.Stop();

        double segundos = Math.Max(0.001, relogio.Elapsed.TotalSeconds);
        long u = Interlocked.Read(ref total.Value);

        double flops = carga switch
        {
            CargaCpu.Vector => u * Vector<float>.Count * 2.0,  // um FMA = 2 operações por elemento
            CargaCpu.Flutuante => u * 4.0,                    // sin, cos, sqrt e uma soma
            _ => 0
        };
        double bytes = carga == CargaCpu.Memoria ? u * 8.0 * 2 : 0;  // lê e escreve 8 bytes por unidade

        string unidade = carga switch
        {
            CargaCpu.Inteiro => "misturas inteiras",
            CargaCpu.Flutuante => "blocos transcendentais",
            CargaCpu.Vector => $"FMA de {Vector<float>.Count} floats",
            _ => "blocos de 8 bytes (leitura + escrita)"
        };

        aoProgresso?.Invoke(u);
        return new Resultado(u, unidade, bytes, flops, segundos);
    }

    /// <summary>
    /// Aplica a carga em lotes e publica cada lote, para o progresso aparecer enquanto decorre.
    /// Cada "unidade" é uma passagem pelo corpo do laço — a contagem é feita, não estimada.
    /// </summary>
    private static void Trabalhar(CargaCpu carga, CancellationToken ct, int semente, byte[]? buffer,
        Action<long> acumular)
    {
        const int lote = 1_000_000;

        switch (carga)
        {
            case CargaCpu.Inteiro:
                {
                    ulong x = (ulong)semente * 6364136223846793005UL + 1442695040888963407UL;
                    while (!ct.IsCancellationRequested)
                    {
                        for (int i = 0; i < lote; i++)
                        {
                            x ^= x << 13;
                            x ^= x >> 7;
                            x ^= x << 17;
                            x += 0x9E3779B97F4A7C15UL;
                        }
                        acumular(lote);
                    }
                    break;
                }

            case CargaCpu.Flutuante:
                {
                    double a = semente * 0.5, b = semente * 0.25;
                    while (!ct.IsCancellationRequested)
                    {
                        for (int i = 0; i < lote; i++)
                        {
                            a = Math.Sin(a) + Math.Cos(b);
                            b = Math.Sqrt(Math.Abs(b)) + 0.5;
                        }
                        acumular(lote);
                    }
                    break;
                }

            case CargaCpu.Vector:
                {
                    var acumulador = new Vector<float>(semente);
                    // cadeia estável: converge para 5e6 e nunca chega a infinito (se chegasse, a
                    // thread saía ao fim de poucos lotes e o teste durava menos do que o pedido)
                    var passo = new Vector<float>(0.9999999f);
                    var fator = new Vector<float>(0.5f);
                    while (!ct.IsCancellationRequested)
                    {
                        for (int i = 0; i < lote; i++)
                            acumulador = acumulador * passo + fator;   // FMA
                        acumular(lote);

                        // consome o acumulador: impede que o JIT elimine a cadeia (sem isto o
                        // "GFLOP/s" podia medir um laço que já não fazia contas)
                        if (float.IsInfinity(acumulador[0])) break;
                        GC.KeepAlive(acumulador);
                    }
                    break;
                }

            default: // Memoria
                {
                    if (buffer is null) return;
                    var palavras = MemoryMarshal.Cast<byte, ulong>(buffer.AsSpan());
                    ulong sementeU = (ulong)semente;
                    while (!ct.IsCancellationRequested)
                    {
                        for (int i = 0; i < palavras.Length; i++)
                            palavras[i] = (ulong)i + sementeU;

                        ulong soma = 0;
                        for (int i = 0; i < palavras.Length; i++)
                            soma += palavras[i];

                        acumular(palavras.Length);
                        GC.KeepAlive(soma);   // consome a leitura sem a poder eliminar
                    }
                    break;
                }
        }
    }
}

/// <summary>Stress de memória RAM: enche, escreve um padrão, relê e verifica.</summary>
public static class StressRam
{
    public sealed record Resultado(long BytesAlocados, double EscritaGbs, double LeituraGbs,
        long PalavrasVerificadas, long Erros, double Segundos, int Passagens);

    private const int Bloco = 64 * 1024 * 1024;   // 64 MB por bloco

    /// <summary>
    /// Aloca os blocos uma vez e repete passagens (escrever tudo, reler tudo e verificar) até ao tempo
    /// pedido. As taxas saem dos bytes realmente movimentados, não de uma amostra.
    /// </summary>
    public static Resultado Correr(double fracao, TimeSpan duracao, CancellationToken ct, Action<string>? progresso = null)
    {
        long total = TotalRam();
        long disponivel = RamDisponivel();
        long alvo = (long)(total * Math.Clamp(fracao, 0.05, 0.95));
        long teto = Math.Max(0, disponivel - 1024L * 1024 * 1024);   // deixa 1 GB livre
        alvo = Math.Min(alvo, teto);

        if (alvo < Bloco)
            throw new InvalidOperationException("memória livre insuficiente para o stress de RAM");

        var blocos = new List<byte[]>();
        progresso?.Invoke($"a alocar {Util.Gb(alvo)}…");

        for (long i = 0; i < alvo / Bloco; i++)
        {
            if (ct.IsCancellationRequested) break;
            blocos.Add(GC.AllocateUninitializedArray<byte>(Bloco, pinned: false));
        }
        if (blocos.Count == 0)
            throw new InvalidOperationException("cancelado antes de alocar memória");

        long alocado = (long)blocos.Count * Bloco;
        long bytesEscritos = 0, bytesLidos = 0, erros = 0, verificadas = 0;
        double tempoEscrita = 0, tempoLeitura = 0;
        int passagens = 0;
        var relogioTotal = Stopwatch.StartNew();

        while (relogioTotal.Elapsed < duracao && !ct.IsCancellationRequested)
        {
            var relogio = Stopwatch.StartNew();
            progresso?.Invoke($"passagem {passagens + 1} · a escrever {Util.Gb(alocado)}…");

            for (int b = 0; b < blocos.Count; b++)
            {
                var palavras = MemoryMarshal.Cast<byte, ulong>(blocos[b].AsSpan());
                ulong semente = (ulong)b * 0x9E3779B97F4A7C15UL + (ulong)passagens * 0x2545F4914F6CDD1DUL;
                for (int i = 0; i < palavras.Length; i++)
                    palavras[i] = (ulong)i ^ semente;
                if (ct.IsCancellationRequested) break;
            }
            tempoEscrita += relogio.Elapsed.TotalSeconds;
            bytesEscritos += alocado;

            relogio.Restart();
            progresso?.Invoke($"passagem {passagens + 1} · a reler e a verificar…");

            for (int b = 0; b < blocos.Count; b++)
            {
                var palavras = MemoryMarshal.Cast<byte, ulong>(blocos[b].AsSpan());
                ulong semente = (ulong)b * 0x9E3779B97F4A7C15UL + (ulong)passagens * 0x2545F4914F6CDD1DUL;
                for (int i = 0; i < palavras.Length; i++)
                {
                    if (palavras[i] != ((ulong)i ^ semente)) erros++;
                    verificadas++;
                }
                if (ct.IsCancellationRequested) break;
            }
            tempoLeitura += relogio.Elapsed.TotalSeconds;
            bytesLidos += alocado;
            passagens++;
        }

        blocos.Clear();
        GC.Collect();

        double gbEscrito = bytesEscritos / 1073741824.0;
        double gbLido = bytesLidos / 1073741824.0;

        return new Resultado(alocado,
            gbEscrito / Math.Max(0.001, tempoEscrita),
            gbLido / Math.Max(0.001, tempoLeitura),
            verificadas, erros, relogioTotal.Elapsed.TotalSeconds,
            Math.Max(1, passagens));
    }

    private static long TotalRam()
    {
        foreach (var linha in File.ReadLines("/proc/meminfo"))
            if (linha.StartsWith("MemTotal:"))
                return long.Parse(new string(linha.Where(char.IsDigit).ToArray())) * 1024;
        return 0;
    }

    private static long RamDisponivel()
    {
        foreach (var linha in File.ReadLines("/proc/meminfo"))
            if (linha.StartsWith("MemAvailable:"))
                return long.Parse(new string(linha.Where(char.IsDigit).ToArray())) * 1024;
        return 0;
    }
}

/// <summary>
/// Stress de disco: passagens sequenciais (escrever e reler a verificar) seguidas de escrita aleatória
/// de 4 KB, até ao tempo pedido. O ficheiro é apagado no fim.
/// </summary>
public static class StressDisco
{
    public sealed record Resultado(string Pasta, long BytesPorPassagem, double EscritaMbs, double LeituraMbs,
        double Iops4K, long Erros, double Segundos, int Passagens);

    public static Resultado Correr(string pasta, long tamanho, TimeSpan duracao, CancellationToken ct,
        Action<string>? progresso = null)
    {
        Directory.CreateDirectory(pasta);
        string ficheiro = Path.Combine(pasta, $"stress-{DateTime.Now:HHmmss}.bin");
        long tamanhoFinal = Math.Clamp(tamanho, 16L * 1024 * 1024, 8L * 1024 * 1024 * 1024);

        var dados = GC.AllocateArray<byte>(4 * 1024 * 1024, pinned: true);   // 4 MB por escrita
        var verificacao = GC.AllocateArray<byte>(4 * 1024 * 1024, pinned: true);
        var rnd = new Random(12345);
        rnd.NextBytes(dados);

        long erros = 0, bytesEscritos = 0, bytesLidos = 0, ops = 0;
        double tempoEscrita = 0, tempoLeitura = 0, tempoAleatorio = 0;
        int passagens = 0;

        var total = Stopwatch.StartNew();
        var relogio = new Stopwatch();

        try
        {
            using var escrita = new FileStream(ficheiro, FileMode.Create, FileAccess.Write, FileShare.Read,
                1 << 20, FileOptions.SequentialScan);
            escrita.SetLength(tamanhoFinal);

            // Reserva uma fatia do tempo para os 4 KB aleatórios (senão as passagens sequenciais
            // consomem a sessão inteira e a fase aleatória nunca corre).
            double reserva = Math.Min(3, duracao.TotalSeconds * 0.3);

            while (total.Elapsed.TotalSeconds < duracao.TotalSeconds - reserva && !ct.IsCancellationRequested)
            {
                // ---- escrita sequencial ----
                relogio.Restart();
                progresso?.Invoke($"passagem {passagens + 1} · a escrever {Util.Bytes(tamanhoFinal)}…");
                escrita.Seek(0, SeekOrigin.Begin);
                long escrito = 0;
                while (escrito < tamanhoFinal && !ct.IsCancellationRequested)
                {
                    int n = (int)Math.Min(dados.Length, tamanhoFinal - escrito);
                    escrita.Write(dados, 0, n);
                    escrito += n;
                }
                escrita.Flush(flushToDisk: true);
                tempoEscrita += relogio.Elapsed.TotalSeconds;
                bytesEscritos += escrito;

                // ---- leitura e verificação ----
                relogio.Restart();
                progresso?.Invoke($"passagem {passagens + 1} · a reler e a verificar…");
                long lido = 0;
                using (var leitura = new FileStream(ficheiro, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                           1 << 20, FileOptions.SequentialScan))
                {
                    while (lido < escrito && !ct.IsCancellationRequested)
                    {
                        int n = (int)Math.Min(verificacao.Length, escrito - lido);
                        int l = leitura.Read(verificacao, 0, n);
                        if (l <= 0) break;
                        for (int i = 0; i < l; i++)
                            if (verificacao[i] != dados[i]) erros++;
                        lido += l;
                    }
                }
                tempoLeitura += relogio.Elapsed.TotalSeconds;
                bytesLidos += lido;
                passagens++;
            }

            // ---- 4 KB aleatórios (até 3 s, dentro do tempo que sobra) ----
            double restante = duracao.TotalSeconds - total.Elapsed.TotalSeconds;
            double alvoAleatorio = Math.Min(3, Math.Max(0, restante));
            if (alvoAleatorio > 0.2 && !ct.IsCancellationRequested)
            {
                progresso?.Invoke("a escrever blocos de 4 KB (aleatório)…");
                relogio.Restart();
                using var rw = new FileStream(ficheiro, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite,
                    4096, FileOptions.RandomAccess);
                var bloco = GC.AllocateArray<byte>(4096, pinned: true);
                while (relogio.Elapsed.TotalSeconds < alvoAleatorio && !ct.IsCancellationRequested)
                {
                    long pos = (long)(rnd.NextDouble() * Math.Max(1, tamanhoFinal - 4096)) & ~4095L;
                    rw.Seek(pos, SeekOrigin.Begin);
                    rw.Write(bloco, 0, bloco.Length);
                    ops++;
                    if ((ops & 0x3F) == 0) rw.Flush(flushToDisk: true);
                }
                rw.Flush(flushToDisk: true);
                tempoAleatorio = relogio.Elapsed.TotalSeconds;
            }

            double seg = total.Elapsed.TotalSeconds;
            return new Resultado(pasta, tamanhoFinal,
                bytesEscritos / 1048576.0 / Math.Max(0.001, tempoEscrita),
                bytesLidos / 1048576.0 / Math.Max(0.001, tempoLeitura),
                ops / Math.Max(0.001, tempoAleatorio), erros, seg, passagens);
        }
        finally
        {
            try { File.Delete(ficheiro); } catch { }
            GC.Collect();
        }
    }
}

/// <summary>Stress de GPU: dispatches contínuos do path tracer (o mesmo shader do benchmark).</summary>
public static class StressGpu
{
    public sealed record Resultado(string Nome, long Pontos, double Segundos, int Falhas);

    public static Resultado Correr(int indice, TimeSpan duracao, CancellationToken ct, Action<string>? progresso = null)
    {
        var gpus = BenchGpu.Dispositivos();
        string nome = indice >= 0 && indice < gpus.Count ? gpus[indice].Nome : "GPU";

        const int largura = 640, altura = 360, amostras = 128;   // ~2 s por dispatch nesta máquina
        long pontos = 0;
        int falhas = 0;
        var relogio = Stopwatch.StartNew();

        while (relogio.Elapsed < duracao && !ct.IsCancellationRequested)
        {
            progresso?.Invoke($"compute {largura}×{altura} · {amostras} spp · {pontos / 1_000_000.0:0.0} M pts");
            var r = BenchGpu.Correr(indice, largura, altura, amostras, null, ct);
            if (r is null)
            {
                if (++falhas >= 3) break;
                Thread.Sleep(200);
                continue;
            }
            pontos += r.Pontos;
        }

        relogio.Stop();
        return new Resultado(nome, pontos, relogio.Elapsed.TotalSeconds, falhas);
    }
}

/// <summary>
/// Sessão de stress: corre um alvo (ou todos em paralelo), publica progresso e respeita a guarda
/// térmica. Tudo o que reporta é medido — temperaturas dos sensores, energia do RAPL e contagens
/// feitas pelos próprios workers.
/// </summary>
public sealed class SessaoStress : IDisposable
{
    private readonly AlvoStress _alvo;
    private readonly int _threads;
    private readonly CargaCpu _carga;
    private readonly TimeSpan _duracao;
    private readonly double _fracaoRam;
    private readonly string _pastaDisco;
    private readonly long _tamanhoDisco;
    private readonly int _indiceGpu;

    private readonly CancellationTokenSource _cancelar = new();
    private Thread? _thread;
    private Timer? _vigia;

    private string _fase = "a preparar…";
    private long _ritmo;
    private readonly Stopwatch _relogio = Stopwatch.StartNew();

    public event Action<ProgressoStress>? Progresso;
    public event Action<ResultadoStress>? Terminou;

    public bool EmCurso => _thread is { IsAlive: true };

    public SessaoStress(AlvoStress alvo, TimeSpan duracao, int threads = 0, CargaCpu carga = CargaCpu.Vector,
        double fracaoRam = 0.5, string? pastaDisco = null, long tamanhoDisco = 512L * 1024 * 1024, int indiceGpu = 0)
    {
        _alvo = alvo;
        _duracao = duracao;
        _threads = threads <= 0 ? Environment.ProcessorCount : threads;
        _carga = carga;
        _fracaoRam = fracaoRam;
        _pastaDisco = pastaDisco ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "linux-explode", "stress");
        _tamanhoDisco = tamanhoDisco;
        _indiceGpu = indiceGpu;
    }

    public void Iniciar()
    {
        if (EmCurso) throw new InvalidOperationException("já há uma sessão de stress a correr");

        lock (_abertas) _abertas.Add(this);

        _thread = new Thread(Correr) { IsBackground = true, Name = "stress-sessao" };
        _thread.Start();
    }

    // ------------------------------------------------------------------ registo das sessões abertas

    private static readonly List<SessaoStress> _abertas = new();

    /// <summary>Sessões iniciadas e ainda não libertadas (o ícone da bandeja usa esta lista).</summary>
    public static List<SessaoStress> Abertas
    {
        get { lock (_abertas) return _abertas.ToList(); }
    }

    /// <summary>Quantas sessões estão a correr agora.</summary>
    public static int EmCursoAgora => Abertas.Count(s => s.EmCurso);

    /// <summary>Pára todas as sessões a correr (menu do ícone da bandeja). Devolve quantas parou.</summary>
    public static int PararTodas()
    {
        var paradas = 0;
        foreach (var s in Abertas)
        {
            if (!s.EmCurso) continue;
            s.Parar();
            paradas++;
        }
        return paradas;
    }

    public void Parar()
    {
        try { _cancelar.Cancel(); } catch { }
    }

    private void Correr()
    {
        var relogio = Stopwatch.StartNew();
        var inicio = DateTime.Now;
        double energiaAntes = EnergiaKwh();
        double picoCpu = 0, picoGpu = 0;
        string? motivo = null;

        using var porDuracao = new CancellationTokenSource(_duracao);
        using var ligado = CancellationTokenSource.CreateLinkedTokenSource(_cancelar.Token, porDuracao.Token);
        var token = ligado.Token;

        _vigia = new Timer(_ =>
        {
            try
            {
                var (ok, porque, cpu, gpu) = GuardaTermica.Avaliar();
                if (cpu is double c) picoCpu = Math.Max(picoCpu, c);
                if (gpu is double g) picoGpu = Math.Max(picoGpu, g);

                if (!ok)
                {
                    motivo = porque;
                    ligado.Cancel();
                }

                Progresso?.Invoke(new ProgressoStress(_fase, relogio.Elapsed.TotalSeconds, Ritmo(), Detalhe(cpu, gpu)));
            }
            catch { }
        }, null, 0, 400);

        var linhas = new List<string>();
        bool completo = true;

        try
        {
            switch (_alvo)
            {
                case AlvoStress.Cpu:
                    {
                        _fase = $"CPU · {_threads} thread(s) · {NomeCarga(_carga)}";
                        var r = StressCpu.Correr(_threads, _carga, token, n => _ritmo = n);
                        linhas.Add($"CPU: {r.Unidades:N0} {r.Unidade} em {r.Segundos:0.00} s" +
                                   $" → {r.Unidades / r.Segundos / 1_000_000.0:0.0} M/s");
                        if (r.Flops > 0) linhas.Add($"CPU: {r.Flops / 1e9 / r.Segundos:0.0} GFLOP/s ({_threads} threads)");
                        if (r.Bytes > 0) linhas.Add($"CPU↔RAM: {r.Bytes / r.Segundos / 1e9:0.0} GB/s de tráfego");
                        break;
                    }

                case AlvoStress.Ram:
                    {
                        _fase = $"RAM · {_fracaoRam:P0} da memória";
                        var r = StressRam.Correr(_fracaoRam, _duracao, token, m => _fase = "RAM · " + m);
                        linhas.Add($"RAM: {Util.Gb(r.BytesAlocados)} × {r.Passagens} passagem(ns) em {r.Segundos:0.0} s");
                        linhas.Add($"RAM: escrita {r.EscritaGbs:0.0} GB/s · leitura {r.LeituraGbs:0.0} GB/s");
                        linhas.Add(r.Erros == 0
                            ? $"RAM: {r.PalavrasVerificadas:N0} palavras verificadas, sem erros"
                            : $"RAM: {r.Erros:N0} ERROS em {r.PalavrasVerificadas:N0} palavras — memória instável");
                        break;
                    }

                case AlvoStress.Gpu:
                    {
                        _fase = "GPU · compute contínuo";
                        var r = StressGpu.Correr(_indiceGpu, _duracao, token, m => _fase = "GPU · " + m);
                        linhas.Add($"GPU ({r.Nome}): {r.Pontos / 1e6:0.0} M pontos em {r.Segundos:0.0} s" +
                                   $" → {r.Pontos / r.Segundos / 1e6:0.0} M pts/s");
                        if (r.Falhas > 0) linhas.Add($"GPU: {r.Falhas} dispatch(es) falharam — ver o registo");
                        break;
                    }

                case AlvoStress.Disco:
                    {
                        _fase = "Disco · sequencial e 4 KB";
                        var r = StressDisco.Correr(_pastaDisco, _tamanhoDisco, _duracao, token, m => _fase = "Disco · " + m);
                        linhas.Add($"Disco: {Util.Bytes(r.BytesPorPassagem)} × {r.Passagens} passagem(ns) em {r.Segundos:0.0} s");
                        linhas.Add($"Disco: escrita {r.EscritaMbs:0} MB/s · leitura {r.LeituraMbs:0} MB/s · 4 KB {r.Iops4K:0} IOPS");
                        linhas.Add(r.Erros == 0
                            ? "Disco: dados relidos iguais aos escritos (sem corrupção)"
                            : $"Disco: {r.Erros:N0} bytes diferentes na releitura");
                        break;
                    }

                default: // Tudo
                    {
                        _fase = "Tudo · CPU + RAM + GPU + disco";
                        // Sem token no Task.Run: os motores já param sozinhos e devolvem o que fizeram,
                        // e um trabalho cancelado no arranque perdia a linha toda do resultado.
                        var cpu = Task.Run(() => StressCpu.Correr(_threads, _carga, token, n => _ritmo = n));
                        var ram = Task.Run(() => StressRam.Correr(_fracaoRam, _duracao, token));
                        var gpu = Task.Run(() => StressGpu.Correr(_indiceGpu, _duracao, token));
                        var disco = Task.Run(() => StressDisco.Correr(_pastaDisco, _tamanhoDisco, _duracao, token));

                        try { Task.WaitAll(new Task[] { cpu, ram, gpu, disco }); }
                        catch (AggregateException) { }

                        if (cpu.Status == TaskStatus.RanToCompletion)
                        {
                            var r = cpu.Result;
                            linhas.Add($"CPU: {r.Unidades:N0} {r.Unidade} em {r.Segundos:0.00} s" +
                                       $" → {r.Unidades / r.Segundos / 1e6:0.0} M/s · {r.Flops / 1e9 / r.Segundos:0.0} GFLOP/s");
                        }
                        if (ram.Status == TaskStatus.RanToCompletion)
                        {
                            var r = ram.Result;
                            linhas.Add($"RAM: {Util.Gb(r.BytesAlocados)} × {r.Passagens} · escrita {r.EscritaGbs:0.0} GB/s · " +
                                       $"leitura {r.LeituraGbs:0.0} GB/s · {r.Erros} erro(s)");
                        }
                        if (gpu.Status == TaskStatus.RanToCompletion)
                        {
                            var r = gpu.Result;
                            linhas.Add($"GPU ({r.Nome}): {r.Pontos / r.Segundos / 1e6:0.0} M pts/s");
                        }
                        if (disco.Status == TaskStatus.RanToCompletion)
                        {
                            var r = disco.Result;
                            linhas.Add($"Disco: escrita {r.EscritaMbs:0} MB/s · leitura {r.LeituraMbs:0} MB/s · {r.Erros} erro(s)");
                        }
                        break;
                    }
            }
        }
        catch (OperationCanceledException)
        {
            completo = false;
        }
        catch (Exception ex)
        {
            completo = false;
            motivo ??= ex.Message;
            linhas.Add("Falhou: " + ex.Message);
        }
        finally
        {
            _vigia?.Dispose();
            _vigia = null;
            relogio.Stop();
        }

        bool porTempo = porDuracao.IsCancellationRequested && !_cancelar.IsCancellationRequested;

        // Chegar ao fim do tempo pedido é uma conclusão normal; parar à mão ou pela guarda térmica não.
        if (porTempo)
        {
            completo = true;
            motivo = null;
        }
        else if (motivo is not null || _cancelar.IsCancellationRequested)
        {
            completo = false;
            motivo ??= "parado pelo utilizador";
        }

        double energia = Math.Max(0, EnergiaKwh() - energiaAntes);
        var resumo = motivo is null || motivo.StartsWith("concluído")
            ? $"concluído em {relogio.Elapsed.TotalSeconds:0} s"
            : motivo;

        var resultado = new ResultadoStress(_alvo, completo, resumo, linhas, picoCpu, picoGpu, energia,
            relogio.Elapsed.TotalSeconds);

        Terminou?.Invoke(resultado);
    }

    private long _ritmoAnterior;
    private long _msAnterior;

    /// <summary>
    /// Ritmo da carga de CPU em milhões de unidades por segundo, entre amostras consecutivas.
    /// Usa o relógio do sistema (ms) para o intervalo — o mesmo que a contagem usa para crescer.
    /// </summary>
    private string Ritmo()
    {
        if (_alvo is not (AlvoStress.Cpu or AlvoStress.Tudo)) return "";

        long agora = Interlocked.Read(ref _ritmo);
        long ms = Environment.TickCount64;

        if (_msAnterior == 0)
        {
            _ritmoAnterior = agora;
            _msAnterior = ms;
            return "";
        }

        double intervalo = (ms - _msAnterior) / 1000.0;
        if (intervalo < 0.4) return "";

        double taxa = Math.Max(0, agora - _ritmoAnterior) / intervalo / 1e6;
        _ritmoAnterior = agora;
        _msAnterior = ms;
        return $"{taxa:0} M/s";
    }

    private string Detalhe(double? cpu, double? gpu)
    {
        var partes = new List<string>();
        if (cpu is double c) partes.Add($"CPU {c:0} °C");
        if (gpu is double g) partes.Add($"GPU {g:0} °C");
        var p = Potencia.Ler();
        if (p.TotalW is double w) partes.Add($"{w:0} W");
        return string.Join(" · ", partes);
    }

    private static string NomeCarga(CargaCpu carga) => carga switch
    {
        CargaCpu.Inteiro => "inteiro",
        CargaCpu.Flutuante => "vírgula flutuante",
        CargaCpu.Vector => "FMA vetorial",
        _ => "memória"
    };

    private static double EnergiaKwh()
    {
        try { return Potencia.Ler().EnergiaKWh ?? 0; } catch { return 0; }
    }

    public void Dispose()
    {
        try { _cancelar.Cancel(); } catch { }
        _vigia?.Dispose();
        _cancelar.Dispose();
        lock (_abertas) _abertas.Remove(this);
    }
}

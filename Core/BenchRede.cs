using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LinuxExplode.Core;

/// <summary>
/// Resultado de um teste de débito real entre dois processos: o que subiu, o que desceu, a latência de
/// ida e volta e o jitter. Os valores saem de bytes contados e de um cronómetro — não de uma estimativa
/// a partir da velocidade da interface.
/// </summary>
public sealed record ResultadoRedeBench(string Destino, int Porta, long Bytes,
    double SubidaMbs, double DescidaMbs, double LatenciaMs, double JitterMs, double Segundos, List<string> Linhas)
{
    public string Resumo =>
        $"subida {SubidaMbs:0.0} MB/s · descida {DescidaMbs:0.0} MB/s · latência {LatenciaMs:0.00} ms";
}

/// <summary>
/// Benchmark de rede a sério: um lado serve, o outro envia N MB e recebe os mesmos N MB de volta.
/// O protocolo é mínimo — 20 idas e voltas de 8 bytes para a latência, depois o tamanho e o corpo —
/// e é o mesmo código nos dois lados, por isso o teste local prova o caminho todo sem inventar nada.
/// </summary>
public static class BenchRede
{
    public const int PortaPadrao = 47820;
    private const int RodadasLatencia = 20;
    private const int Bloco = 1024 * 1024;

    /// <summary>Uma porta livre atribuída pelo kernel (para o teste local e para exemplos).</summary>
    public static int PortaLivre()
    {
        var ouvinte = new TcpListener(IPAddress.Loopback, 0);
        ouvinte.Start();
        int porta = ((IPEndPoint)ouvinte.LocalEndpoint).Port;
        ouvinte.Stop();
        return porta;
    }

    /// <summary>
    /// Cliente: liga-se, mede latência com 20 ecos, envia o corpo e lê-o de volta. Devolve sempre um
    /// resultado com as linhas do que foi medido (mesmo quando o servidor fecha a ligação a meio, o erro
    /// é dito em vez de escondido).
    /// </summary>
    public static ResultadoRedeBench Cliente(string host, int porta, int megabytes,
        Action<string>? progresso = null, CancellationToken ct = default)
    {
        megabytes = Math.Clamp(megabytes, 1, 4096);
        long total = (long)megabytes * 1024 * 1024;
        var linhas = new List<string>();

        using var cliente = new TcpClient();
        var ligacao = cliente.ConnectAsync(host, porta, ct).AsTask();

        // Task.Wait lança AggregateException quando a ligação é recusada (não devolve false): o erro
        // tem de sair em pt-PT com a instrução, não como "One or more errors occurred"
        try
        {
            if (!ligacao.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException($"o servidor {host}:{porta} não respondeu em 10 s");
        }
        catch (AggregateException ex)
        {
            throw new IOException($"não consegui ligar a {host}:{porta} — verifica se o servidor está a correr " +
                                  $"(--cli netbench servidor) e se a porta está aberta na firewall " +
                                  $"({ex.InnerException?.Message ?? ex.Message})", ex);
        }

        cliente.NoDelay = true;
        using var s = cliente.GetStream();
        s.ReadTimeout = 120_000;
        s.WriteTimeout = 120_000;

        // latência: 8 bytes de ida e volta, 20 vezes, sem dados de fundo
        progresso?.Invoke("a medir latência (20 idas e voltas)…");
        var eco = new byte[8];
        var amostras = new List<double>(RodadasLatencia);
        for (int i = 0; i < RodadasLatencia; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t0 = Stopwatch.GetTimestamp();
            s.Write(eco, 0, eco.Length);
            s.Flush();
            LerExato(s, eco, eco.Length);
            amostras.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
        }

        double latencia = amostras.Average();
        double jitter = Math.Sqrt(amostras.Select(a => (a - latencia) * (a - latencia)).Average());

        // tamanho do corpo, para o servidor saber quando parar
        var cabecalho = BitConverter.GetBytes(total);
        s.Write(cabecalho, 0, cabecalho.Length);
        s.Flush();

        var buffer = new byte[Bloco];
        Random.Shared.NextBytes(buffer);

        progresso?.Invoke($"a enviar {Util.Bytes(total)}…");
        var cron = Stopwatch.StartNew();
        long enviado = 0;
        while (enviado < total)
        {
            ct.ThrowIfCancellationRequested();
            int n = (int)Math.Min(buffer.Length, total - enviado);
            s.Write(buffer, 0, n);
            enviado += n;
            if (enviado % (Bloco * 8) == 0) progresso?.Invoke($"a enviar… {enviado * 100.0 / total:0} %");
        }
        s.Flush();
        double segundosSubida = Math.Max(1e-6, cron.Elapsed.TotalSeconds);

        progresso?.Invoke($"a receber {Util.Bytes(total)} de volta…");
        cron.Restart();
        long recebido = 0;
        while (recebido < total)
        {
            ct.ThrowIfCancellationRequested();
            int n = s.Read(buffer, 0, (int)Math.Min(buffer.Length, total - recebido));
            if (n <= 0) throw new IOException("o servidor fechou a ligação antes de devolver tudo");
            recebido += n;
            if (recebido % (Bloco * 8) == 0) progresso?.Invoke($"a receber… {recebido * 100.0 / total:0} %");
        }
        double segundosDescida = Math.Max(1e-6, cron.Elapsed.TotalSeconds);

        double subida = enviado / 1048576.0 / segundosSubida;
        double descida = recebido / 1048576.0 / segundosDescida;
        double segundos = segundosSubida + segundosDescida;

        linhas.Add($"corpo: {Util.Bytes(enviado)} enviados e {Util.Bytes(recebido)} devolvidos");
        linhas.Add($"subida: {enviado / 1048576.0:0.0} MB em {segundosSubida:0.00} s → {subida:0.0} MB/s");
        linhas.Add($"descida: {recebido / 1048576.0:0.0} MB em {segundosDescida:0.00} s → {descida:0.0} MB/s");
        linhas.Add($"latência: média {latencia:0.00} ms · mín {amostras.Min():0.00} · máx {amostras.Max():0.00} · " +
                   $"jitter {jitter:0.00} ms ({RodadasLatencia} amostras)");
        if (subida < 1 && descida < 1)
            linhas.Add("aviso: menos de 1 MB/s nos dois sentidos — verifica se não estás limitado pela interface");

        return new ResultadoRedeBench($"{host}:{porta}", porta, enviado, subida, descida, latencia, jitter, segundos, linhas);
    }

    /// <summary>
    /// Servidor: escuta numa porta, atende um cliente de cada vez (para a medição não competir com
    /// outro tráfego) e devolve exactamente o que recebeu. <paramref name="umaVez"/> serve ao teste
    /// local, que não quer um processo pendurado.
    /// </summary>
    public static async Task ServidorAsync(int porta, Action<string>? log = null,
        bool umaVez = false, CancellationToken ct = default)
    {
        var ouvinte = new TcpListener(IPAddress.Any, porta);
        ouvinte.Start();
        log?.Invoke($"à espera de um cliente em 0.0.0.0:{porta} …");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var cliente = await ouvinte.AcceptTcpClientAsync(ct);
                cliente.NoDelay = true;
                Atender(cliente, log, ct);
                if (umaVez) break;
                log?.Invoke($"à espera do próximo cliente em 0.0.0.0:{porta} …");
            }
        }
        catch (OperationCanceledException) { }
        finally { ouvinte.Stop(); }
    }

    private static void Atender(TcpClient cliente, Action<string>? log, CancellationToken ct)
    {
        using var s = cliente.GetStream();
        s.ReadTimeout = 120_000;
        s.WriteTimeout = 120_000;

        var buffer = new byte[Bloco];

        // 20 ecos de 8 bytes: é a latência do cliente que está a ser medida
        for (int i = 0; i < RodadasLatencia && !ct.IsCancellationRequested; i++)
        {
            LerExato(s, buffer, 8);
            s.Write(buffer, 0, 8);
        }
        s.Flush();

        var cabecalho = new byte[8];
        LerExato(s, cabecalho, 8);
        long total = BitConverter.ToInt64(cabecalho);
        if (total <= 0 || total > 8L * 1024 * 1024 * 1024)
        {
            log?.Invoke("pedido inválido: tamanho fora dos limites de 1 B a 8 GB");
            return;
        }

        var cron = Stopwatch.StartNew();
        long recebido = 0;
        while (recebido < total)
        {
            int n = s.Read(buffer, 0, (int)Math.Min(buffer.Length, total - recebido));
            if (n <= 0) return;
            recebido += n;
        }
        double segundosReceber = Math.Max(1e-6, cron.Elapsed.TotalSeconds);

        cron.Restart();
        long enviado = 0;
        while (enviado < total)
        {
            int n = (int)Math.Min(buffer.Length, total - enviado);
            s.Write(buffer, 0, n);
            enviado += n;
        }
        s.Flush();
        double segundosEnviar = Math.Max(1e-6, cron.Elapsed.TotalSeconds);

        log?.Invoke($"cliente atendido: {Util.Bytes(recebido)} a receber a {recebido / 1048576.0 / segundosReceber:0.0} MB/s · " +
                    $"a devolver a {enviado / 1048576.0 / segundosEnviar:0.0} MB/s");
    }

    /// <summary>Lê exactamente <paramref name="quantos"/> bytes; a meio de um pacote não é fim da ligação.</summary>
    private static void LerExato(Stream s, byte[] destino, int quantos)
    {
        int lido = 0;
        while (lido < quantos)
        {
            int n = s.Read(destino, lido, quantos - lido);
            if (n <= 0) throw new IOException("ligação fechada a meio de uma leitura");
            lido += n;
        }
    }
}

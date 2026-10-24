using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LinuxExplode.Core;

public sealed record RamResulto(double EscritaMBs, double LeituraMBs, double CopiaMBs, double LatenciaNs, long Tamanho);

public sealed record DiscoResulto(double SeqEscritaMBs, double SeqLeituraMBs, double RandLeituraMBs, double RandEscritaMBs,
    long Ficheiro, bool Directo);

/// <summary>Benchmark de memória: banda de escrita/leitura/cópia e latência com pointer chasing.</summary>
public static class BenchRam
{
    public static RamResulto Correr(Action<string>? progresso = null, CancellationToken ct = default)
    {
        long disponivel = 0;
        foreach (var l in Util.Ler("/proc/meminfo").Split('\n'))
            if (l.StartsWith("MemAvailable:"))
                disponivel = long.TryParse(l.Split(':')[1].Trim().Split(' ')[0], out var v) ? v * 1024 : 0;

        long bytes = Math.Min(256L * 1024 * 1024, Math.Max(32L * 1024 * 1024, disponivel / 8));
        int n = (int)(bytes / 8);
        var a = new long[n];
        var b = new long[n];

        progresso?.Invoke("a medir escrita sequencial…");
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < n; i++) a[i] = i;
        sw.Stop();
        double escrita = n * 8.0 / 1048576 / Math.Max(0.0001, sw.Elapsed.TotalSeconds);

        ct.ThrowIfCancellationRequested();
        progresso?.Invoke("a medir leitura sequencial…");
        sw.Restart();
        long soma = 0;
        for (int i = 0; i < n; i++) soma += a[i];
        sw.Stop();
        double leitura = n * 8.0 / 1048576 / Math.Max(0.0001, sw.Elapsed.TotalSeconds);

        ct.ThrowIfCancellationRequested();
        progresso?.Invoke("a medir cópia…");
        sw.Restart();
        Array.Copy(a, b, n);
        sw.Stop();
        double copia = n * 8.0 / 1048576 / Math.Max(0.0001, sw.Elapsed.TotalSeconds);

        ct.ThrowIfCancellationRequested();
        progresso?.Invoke("a medir latência…");
        double latencia = Latencia();

        GC.KeepAlive(soma);
        return new RamResulto(escrita, leitura, copia, latencia, bytes);
    }

    /// <summary>Pointer chasing por uma permutação aleatória: mede latência real de acesso à RAM.</summary>
    private static double Latencia()
    {
        int n = 8 * 1024 * 1024;                       // 64 MB → muito acima de qualquer cache
        var dados = new int[n];
        var idx = new int[n];
        for (int i = 0; i < n; i++) idx[i] = i;
        var rnd = new Random(12345);
        for (int i = n - 1; i > 0; i--)                // Fisher-Yates
        {
            int j = rnd.Next(i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }
        for (int i = 0; i < n; i++) dados[idx[i]] = idx[(i + 1) % n];

        const int acessos = 20_000_000;
        int pos = 0;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < acessos; i++) pos = dados[pos];
        sw.Stop();
        GC.KeepAlive(pos);
        return sw.Elapsed.TotalMilliseconds * 1_000_000 / acessos;
    }
}

/// <summary>
/// Benchmark de disco com O_DIRECT (sem cache do sistema), como o original em Windows usava
/// FILE_FLAG_NO_BUFFERING. Se O_DIRECT não estiver disponível, cai para ficheiro normal e avisa.
/// </summary>
public static class BenchDisco
{
    private const int O_RDWR = 2;
    private const int O_CREAT = 0x40;
    private const int O_TRUNC = 0x200;
    private const int O_DIRECT = 0x4000;

    [DllImport("libc", SetLastError = true)]
    private static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string caminho, int flags, uint modo);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    public static unsafe DiscoResulto Correr(string pasta, long tamanhoMB = 256, Action<string>? progresso = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(pasta);
        var ficheiro = Path.Combine(pasta, $"resetbench-{Environment.ProcessId}.tmp");
        long bytes = tamanhoMB * 1024 * 1024;
        const int blocoTamanho = 4 * 1024 * 1024;     // 4 MB alinhado

        IntPtr buffer = IntPtr.Zero;
        SafeFileHandle? handle = null;
        bool directo = false;

        try
        {
            buffer = (IntPtr)NativeMemory.AlignedAlloc((nuint)blocoTamanho, 4096);
            var span = new Span<byte>((void*)buffer, blocoTamanho);
            Random.Shared.NextBytes(span);

            int fd = open(ficheiro, O_RDWR | O_CREAT | O_TRUNC | O_DIRECT, 0b110_100_100);
            if (fd >= 0)
            {
                handle = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
                directo = true;
            }
            else
            {
                handle = File.OpenHandle(ficheiro, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                    FileOptions.WriteThrough);
            }

            progresso?.Invoke($"a escrever {tamanhoMB} MB…");
            var sw = Stopwatch.StartNew();
            for (long pos = 0; pos < bytes; pos += blocoTamanho)
            {
                ct.ThrowIfCancellationRequested();
                RandomAccess.Write(handle, span, pos);
            }
            RandomAccess.FlushToDisk(handle);
            sw.Stop();
            double seqEscrita = bytes / 1048576.0 / Math.Max(0.0001, sw.Elapsed.TotalSeconds);

            progresso?.Invoke("a ler sequencialmente…");
            sw.Restart();
            for (long pos = 0; pos < bytes; pos += blocoTamanho)
            {
                ct.ThrowIfCancellationRequested();
                RandomAccess.Read(handle, span, pos);
            }
            sw.Stop();
            double seqLeitura = bytes / 1048576.0 / Math.Max(0.0001, sw.Elapsed.TotalSeconds);

            // 4K aleatório: o buffer tem de continuar alinhado
            var pequeno = new Span<byte>((void*)buffer, 4096);
            long posicoes = bytes / 4096;
            const int amostras = 4000;

            progresso?.Invoke("a medir 4K aleatório (leitura)…");
            sw.Restart();
            for (int i = 0; i < amostras; i++)
            {
                ct.ThrowIfCancellationRequested();
                RandomAccess.Read(handle, pequeno, Random.Shared.NextInt64(posicoes) * 4096);
            }
            sw.Stop();
            double randLeitura = amostras * 4096.0 / 1048576.0 / Math.Max(0.0001, sw.Elapsed.TotalSeconds);

            progresso?.Invoke("a medir 4K aleatório (escrita)…");
            sw.Restart();
            for (int i = 0; i < amostras; i++)
            {
                ct.ThrowIfCancellationRequested();
                RandomAccess.Write(handle, pequeno, Random.Shared.NextInt64(posicoes) * 4096);
            }
            RandomAccess.FlushToDisk(handle);
            sw.Stop();
            double randEscrita = amostras * 4096.0 / 1048576.0 / Math.Max(0.0001, sw.Elapsed.TotalSeconds);

            return new DiscoResulto(seqEscrita, seqLeitura, randLeitura, randEscrita, bytes, directo);
        }
        finally
        {
            handle?.Dispose();
            if (buffer != IntPtr.Zero) NativeMemory.AlignedFree((void*)buffer);
            try { File.Delete(ficheiro); } catch { }
        }
    }
}

using System.Diagnostics;

namespace LinuxExplode.Core;

/// <summary>Um ponto da curva de latência: tamanho do conjunto de trabalho e nanossegundos por acesso.</summary>
public sealed record LatenciaBloco(long Bytes, double Ns)
{
    /// <summary>Tamanho em texto curto: `4 KB`, `1 MB`, `256 MB`.</summary>
    public string Tamanho => Bytes >= 1024L * 1024
        ? $"{Bytes / (1024.0 * 1024):0} MB"
        : $"{Bytes / 1024:0} KB";

    /// <summary>Fase da hierarquia onde este tamanho cai, para o texto do cartão.</summary>
    public string Fase => Bytes switch
    {
        <= 32 * 1024 => "L1",
        <= 1024 * 1024 => "L2",
        <= 32L * 1024 * 1024 => "L3",
        _ => "RAM"
    };
}

/// <summary>
/// Latência de memória por tamanho de bloco (4 KB a 256 MB), com <i>pointer chasing</i> sobre uma
/// permutação aleatória de blocos de 64 B (uma linha de cache). O percurso salta de bloco em bloco,
/// por isso o pré-buscador não consegue adivinhar o próximo endereço — o que se mede é a latência
/// real de cada nível (L1 → RAM) e não a banda sequencial.
/// </summary>
public static class BenchMemoria
{
    private const int LinhaBytes = 64;

    /// <summary>Tamanhos medidos: potências de 4, de 4 KB a 256 MB (a curva que mostra as caches).</summary>
    public static IReadOnlyList<long> Tamanhos { get; } = new long[]
    {
        4 * 1024, 16 * 1024, 64 * 1024, 256 * 1024,
        1024 * 1024, 4L * 1024 * 1024, 16L * 1024 * 1024, 64L * 1024 * 1024, 256L * 1024 * 1024
    };

    /// <summary>Corre a curva completa. Cancela entre pontos, nunca a meio de um ponto (para não sair um valor truncado).</summary>
    public static List<LatenciaBloco> Curva(Action<string>? progresso = null, CancellationToken ct = default)
    {
        var pontos = new List<LatenciaBloco>(Tamanhos.Count);

        foreach (long bytes in Tamanhos)
        {
            ct.ThrowIfCancellationRequested();
            progresso?.Invoke($"latência com {bytes / 1024,7:N0} KB de conjunto de trabalho…");
            pontos.Add(new LatenciaBloco(bytes, Medir(bytes, ct)));
        }

        return pontos;
    }

    /// <summary>
    /// Mede um tamanho. Os dados ocupam <paramref name="bytes"/> (arredondado a blocos de 64 B); o
    /// primeiro inteiro de cada bloco guarda o índice do bloco seguinte, e a ordem é uma permutação
    /// com semente fixa — duas corridas percorrem exactamente os mesmos endereços.
    /// </summary>
    public static double Medir(long bytes, CancellationToken ct = default)
    {
        long blocos = Math.Max(64, bytes / LinhaBytes);       // mínimo 64 blocos (4 KB)
        int n = checked((int)blocos);
        var dados = new int[n * (LinhaBytes / sizeof(int))];
        var ordem = Ordem(n);

        // Encadeia os blocos: dados[bloco] = bloco seguinte (a permutação fecha o ciclo).
        for (int i = 0; i < n; i++)
            dados[ordem[i] * (LinhaBytes / sizeof(int))] = ordem[(i + 1) % n];

        // Uma passagem toca todos os blocos uma vez. O número de acessos é fixo (e não proporcional ao
        // tamanho): com poucas amostras os tamanhos pequenos mediam ruído de arranque e saíam mais
        // lentos do que a RAM — a curva tem de ser monótona para significar alguma coisa.
        const int acessos = 16_000_000;

        // Aquecimento: 1 M de acessos (ou uma passagem completa, se for maior) para o percurso estar
        // estável na TLB e nas caches antes de contar.
        int pos = 0;
        int aquecimento = Math.Max(n, 1_000_000);
        for (int i = 0; i < aquecimento; i++) pos = dados[pos * (LinhaBytes / sizeof(int))];

        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < acessos; i++) pos = dados[pos * (LinhaBytes / sizeof(int))];
        sw.Stop();
        GC.KeepAlive(pos);

        return sw.Elapsed.TotalMilliseconds * 1_000_000 / acessos;
    }

    /// <summary>Permutação determinística de 0..n-1 (Fisher-Yates com semente fixa).</summary>
    private static int[] Ordem(int n)
    {
        var ordem = new int[n];
        for (int i = 0; i < n; i++) ordem[i] = i;

        var rnd = new Random(20251025);
        for (int i = n - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (ordem[i], ordem[j]) = (ordem[j], ordem[i]);
        }

        return ordem;
    }

    /// <summary>
    /// Texto que resume a curva: a latência de cada fase (o menor valor de cada patamar) e o salto
    /// para a RAM. É o que se mostra por baixo do gráfico.
    /// </summary>
    public static string Resumo(IReadOnlyList<LatenciaBloco> pontos)
    {
        if (pontos.Count == 0) return "sem medições";

        var partes = pontos
            .GroupBy(p => p.Fase)
            .OrderBy(g => g.Min(p => p.Bytes))
            .Select(g => $"{g.Key} {g.Min(p => p.Ns):0.0} ns");

        return string.Join(" · ", partes);
    }
}

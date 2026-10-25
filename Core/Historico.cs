using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LinuxExplode.Core;

/// <summary>Uma medição guardada (benchmark ou stress).</summary>
public sealed record RegistoBench(
    string Tipo,        // "cpu", "gpu", "rt", "ram", "disco", "stress-cpu", …
    string Alvo,        // nome da GPU, "16 threads", "30,2 GB", …
    double Nota,
    string Detalhe,
    DateTime Quando,
    string Versao);

/// <summary>
/// Histórico de resultados em <c>~/.local/share/linux-explode/resultados.json</c>. É o que permite
/// comparar a corrida de hoje com as anteriores (melhor, mediana, variação) e exportar/importar.
/// </summary>
public static class Historico
{
    private static readonly object Cadeado = new();

    public static string Pasta =>
        Environment.GetEnvironmentVariable("LINUXEXPLODE_DADOS") is { Length: > 0 } forcar
            ? forcar
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "linux-explode");

    public static string Caminho => Path.Combine(Pasta, "resultados.json");

    public static List<RegistoBench> Todos()
    {
        try
        {
            if (!File.Exists(Caminho)) return new List<RegistoBench>();
            return JsonSerializer.Deserialize<List<RegistoBench>>(File.ReadAllText(Caminho)) ?? new();
        }
        catch
        {
            return new List<RegistoBench>();
        }
    }

    /// <summary>Guarda uma medição. Repetições com a mesma nota são ignoradas (o histórico não engorda).</summary>
    public static void Registar(string tipo, string alvo, double nota, string detalhe)
    {
        if (nota <= 0) return;

        lock (Cadeado)
        {
            var lista = Todos();

            var ultimo = lista.FirstOrDefault(r => r.Tipo == tipo && r.Alvo == alvo);
            if (ultimo is not null && Math.Abs(ultimo.Nota - nota) < 0.001) return;

            lista.Add(new RegistoBench(tipo, alvo, nota, detalhe, DateTime.Now, Relatorio.Versao));
            if (lista.Count > 500) lista.RemoveRange(0, lista.Count - 500);
            Gravar(lista);
        }
    }

    private static void Gravar(List<RegistoBench> lista)
    {
        try
        {
            Directory.CreateDirectory(Pasta);
            File.WriteAllText(Caminho, JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>Melhor, mediana e última nota de um tipo/alvo.</summary>
    public static (double? Melhor, double? Mediana, double? Ultima, int N) Estatisticas(string tipo, string alvo)
    {
        var valores = Todos()
            .Where(r => r.Tipo == tipo && r.Alvo == alvo)
            .Select(r => r.Nota)
            .ToList();

        if (valores.Count == 0) return (null, null, null, 0);

        var ordenados = valores.OrderBy(v => v).ToList();
        double mediana = ordenados.Count % 2 == 1
            ? ordenados[ordenados.Count / 2]
            : (ordenados[ordenados.Count / 2 - 1] + ordenados[ordenados.Count / 2]) / 2.0;

        return (ordenados[^1], mediana, valores[^1], valores.Count);
    }

    /// <summary>
    /// Frase para o cartão: "melhor 13 355 364 · mediana 12 981 000 · esta −1,2 % (12 corridas)".
    /// Devolve null quando ainda não há histórico.
    /// </summary>
    public static string? Comparacao(string tipo, string alvo, double nota)
    {
        var (melhor, mediana, _, n) = Estatisticas(tipo, alvo);
        if (melhor is null || mediana is null) return null;

        string delta = mediana.Value <= 0
            ? "—"
            : $"{(nota - mediana.Value) / mediana.Value * 100:+0.0;-0.0;0.0} %";

        return $"melhor {melhor.Value:N0} · mediana {mediana.Value:N0} · esta {delta} (mediana de {n} corrida(s))";
    }

    public static string ExportarCsv(string caminho)
    {
        var sb = new StringBuilder();
        sb.AppendLine("quando;tipo;alvo;nota;detalhe;versao");
        foreach (var r in Todos().OrderBy(r => r.Quando))
            sb.AppendLine(string.Join(';',
                r.Quando.ToString("yyyy-MM-dd HH:mm:ss"),
                r.Tipo, r.Alvo,
                r.Nota.ToString("0.###", CultureInfo.InvariantCulture),
                r.Detalhe.Replace(';', ','),
                r.Versao));

        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, sb.ToString());
        return caminho;
    }

    public static string ExportarJson(string caminho)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, JsonSerializer.Serialize(Todos(), new JsonSerializerOptions { WriteIndented = true }));
        return caminho;
    }

    /// <summary>Importa outro ficheiro de resultados (junta, sem duplicar o que já existe).</summary>
    public static int Importar(string caminho)
    {
        var novos = JsonSerializer.Deserialize<List<RegistoBench>>(File.ReadAllText(caminho)) ?? new();

        lock (Cadeado)
        {
            var lista = Todos();
            int antes = lista.Count;

            foreach (var r in novos)
            {
                if (lista.Any(x => x.Tipo == r.Tipo && x.Alvo == r.Alvo &&
                                   Math.Abs(x.Nota - r.Nota) < 0.001 &&
                                   Math.Abs((x.Quando - r.Quando).TotalSeconds) < 2))
                    continue;
                lista.Add(r);
            }

            Gravar(lista);
            return lista.Count - antes;
        }
    }

    // ---------------------------------------------------------------- ranking local (0.20.7)

    /// <summary>Mediana de uma série. Devolve null com a série vazia.</summary>
    public static double? Mediana(IEnumerable<double> valores)
    {
        var ordenados = valores.OrderBy(v => v).ToList();
        if (ordenados.Count == 0) return null;
        return ordenados.Count % 2 == 1
            ? ordenados[ordenados.Count / 2]
            : (ordenados[ordenados.Count / 2 - 1] + ordenados[ordenados.Count / 2]) / 2.0;
    }

    /// <summary>
    /// Amplitude relativa da série em percentagem do maior valor: 0 % = todas as corridas iguais,
    /// 5 % = a maior está 5 % acima da menor. É o que decide se a nota é de confiança.
    /// </summary>
    public static double Variacao(IReadOnlyList<double> valores)
    {
        if (valores.Count < 2) return 0;
        double max = valores.Max(), min = valores.Min();
        if (max <= 0) return 0;
        return (max - min) / max * 100.0;
    }

    /// <summary>Últimas N notas de um tipo/alvo, da mais antiga para a mais recente.</summary>
    public static List<double> Ultimas(string tipo, string alvo, int n = 3) =>
        Todos()
            .Where(r => r.Tipo == tipo && r.Alvo == alvo)
            .OrderBy(r => r.Quando)
            .Select(r => r.Nota)
            .TakeLast(Math.Max(1, n))
            .ToList();

    /// <summary>Quantas corridas entram na mediana do ranking local.</summary>
    public const int CorridasRanking = 3;

    /// <summary>
    /// Ranking local: uma linha por tipo/alvo, com a mediana das últimas corridas, a variação entre
    /// elas e o aviso de instabilidade. Ordenado pela melhor nota de cada alvo.
    /// </summary>
    public static List<RankingLocal> Rankings(int n = CorridasRanking)
    {
        var linhas = new List<RankingLocal>();

        foreach (var grupo in Todos().GroupBy(r => (r.Tipo, r.Alvo)))
        {
            var ordenados = grupo.OrderBy(r => r.Quando).Select(r => r.Nota).ToList();
            var ultimas = ordenados.TakeLast(n).ToList();
            double variacao = Variacao(ultimas);

            linhas.Add(new RankingLocal(
                grupo.Key.Tipo,
                grupo.Key.Alvo,
                ordenados.Max(),
                Mediana(ultimas) ?? 0,
                ordenados[^1],
                variacao,
                ordenados.Count,
                ultimas.Count >= n && variacao > RankingLocal.ToleranciaPct));
        }

        return linhas.OrderByDescending(l => l.Melhor).ToList();
    }
}

/// <summary>
/// Uma linha do ranking local: a mediana das últimas corridas, a variação entre elas e o aviso de
/// instabilidade. `Instavel` só é verdadeiro quando há pelo menos <see cref="Historico.CorridasRanking"/>
/// corridas — com duas não há como distinguir variação de ruído de uma máquina descontrolada.
/// </summary>
public sealed record RankingLocal(
    string Tipo,
    string Alvo,
    double Melhor,
    double Mediana,
    double Ultima,
    double VariacaoPct,
    int Corridas,
    bool Instavel)
{
    /// <summary>Tolerância aceite entre as últimas corridas (a mesma do ROADMAP).</summary>
    public const double ToleranciaPct = 3.0;

    /// <summary>Frase do cartão: "mediana 12 981 000 · últimas 3 variam 1,2 % · última +0,4 %".</summary>
    public string Linha
    {
        get
        {
            string delta = Mediana <= 0 ? "—" : $"{(Ultima - Mediana) / Mediana * 100:+0.0;-0.0;0.0} %";
            int naAmostra = Math.Min(Corridas, Historico.CorridasRanking);
            string variacao = Corridas < 2
                ? "variação —"
                : $"últimas {naAmostra} variam {VariacaoPct:0.0} %" +
                  (Corridas < Historico.CorridasRanking ? " (amostra pequena)" : "");
            return $"mediana {Mediana:N0} · {variacao} · última {delta}";
        }
    }

    /// <summary>Aviso a mostrar quando a variação passa da tolerância; null quando está tudo estável.</summary>
    public string? Aviso =>
        Instavel
            ? $"variação {VariacaoPct:0.0} % nas últimas corridas — acima dos {ToleranciaPct:0} %. " +
              "A máquina não está estável: fecha o que estiver a correr e repete."
            : null;
}

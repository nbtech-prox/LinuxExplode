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

    public static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "linux-explode");

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
}

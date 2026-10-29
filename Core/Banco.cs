using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LinuxExplode.Core;

/// <summary>
/// Histórico em SQLite (<c>historico.db</c>). Substitui o <c>resultados.json</c>, que não escala para
/// meses de amostras e obrigava a ler o ficheiro inteiro para responder a uma pesquisa. O JSON antigo
/// é migrado uma única vez e guardado como <c>resultados.json.migrado</c> — nada se perde.
/// </summary>
public static class Banco
{
    /// <summary>Formato de data no banco: ordenável como texto e igual ao do CSV das sessões.</summary>
    public const string Formato = "yyyy-MM-dd HH:mm:ss";

    public static string Caminho => Path.Combine(Historico.Pasta, "historico.db");

    private static SqliteConnection Abrir()
    {
        Directory.CreateDirectory(Historico.Pasta);

        // uma base apagada por fora não pode ser "ressuscitada" por um WAL órfão
        if (!File.Exists(Caminho))
        {
            TryDelete(Caminho + "-wal");
            TryDelete(Caminho + "-shm");
        }

        // sem pooling: uma ligação em cache continuaria a apontar para o ficheiro apagado (inode
        // antigo) e as leituras seguintes mentiam. Abrir uma base local é barato.
        var ligacao = new SqliteConnectionStringBuilder { DataSource = Caminho, Pooling = false }.ToString();
        var c = new SqliteConnection(ligacao);
        c.Open();

        using (var pragma = c.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
        }

        CriarEsquema(c);
        MigrarJson(c);
        return c;

        static void TryDelete(string ficheiro)
        {
            try { if (File.Exists(ficheiro)) File.Delete(ficheiro); } catch { }
        }
    }

    private static void CriarEsquema(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
CREATE TABLE IF NOT EXISTS resultados (
  id      INTEGER PRIMARY KEY AUTOINCREMENT,
  quando  TEXT    NOT NULL,
  tipo    TEXT    NOT NULL,
  alvo    TEXT    NOT NULL,
  nota    REAL    NOT NULL,
  detalhe TEXT    NOT NULL DEFAULT '',
  versao  TEXT    NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_resultados_tipo_alvo ON resultados(tipo, alvo);
CREATE INDEX IF NOT EXISTS ix_resultados_quando    ON resultados(quando);
CREATE TABLE IF NOT EXISTS migracoes (
  nome  TEXT PRIMARY KEY,
  feito TEXT NOT NULL
);
""";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Migra o <c>resultados.json</c> antigo uma única vez. A marca fica na tabela <c>migracoes</c>: se o
    /// JSON não puder ser renomeado (permissões), não se volta a importar o mesmo em cada arranque.
    /// </summary>
    private static void MigrarJson(SqliteConnection c)
    {
        var json = Path.Combine(Historico.Pasta, "resultados.json");
        if (!File.Exists(json)) return;

        using (var verifica = c.CreateCommand())
        {
            verifica.CommandText = "SELECT 1 FROM migracoes WHERE nome = 'resultados-json'";
            if (verifica.ExecuteScalar() is not null) return;
        }

        List<RegistoBench> antigos;
        try { antigos = JsonSerializer.Deserialize<List<RegistoBench>>(File.ReadAllText(json)) ?? new(); }
        catch { antigos = new(); }

        Inserir(c, antigos);

        using (var marca = c.CreateCommand())
        {
            marca.CommandText = "INSERT OR REPLACE INTO migracoes(nome, feito) VALUES('resultados-json', $quando)";
            marca.Parameters.AddWithValue("$quando", DateTime.Now.ToString(Formato, CultureInfo.InvariantCulture));
            marca.ExecuteNonQuery();
        }

        try { File.Move(json, json + ".migrado", overwrite: true); } catch { }
    }

    /// <summary>Insere sem duplicar (mesma data, tipo, alvo e nota). Devolve quantas linhas entraram.</summary>
    private static int Inserir(SqliteConnection c, IEnumerable<RegistoBench> lista)
    {
        var registos = lista.ToList();
        if (registos.Count == 0) return 0;

        // `ExecuteNonQuery` de um INSERT … SELECT não é fiável para contar (devolvia 0 com a linha a
        // entrar). A contagem antes/depois, dentro da mesma ligação, é a resposta honesta.
        int antes = ContarEm(c);

        using var tx = c.BeginTransaction();

        foreach (var r in registos)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
INSERT INTO resultados(quando, tipo, alvo, nota, detalhe, versao)
SELECT $quando, $tipo, $alvo, $nota, $detalhe, $versao
WHERE NOT EXISTS (
  SELECT 1 FROM resultados WHERE quando = $quando AND tipo = $tipo AND alvo = $alvo AND nota = $nota
);
""";
            cmd.Parameters.AddWithValue("$quando", r.Quando.ToString(Formato, CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$tipo", r.Tipo);
            cmd.Parameters.AddWithValue("$alvo", r.Alvo);
            cmd.Parameters.AddWithValue("$nota", r.Nota);
            cmd.Parameters.AddWithValue("$detalhe", r.Detalhe);
            cmd.Parameters.AddWithValue("$versao", r.Versao);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return ContarEm(c) - antes;
    }

    private static int ContarEm(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM resultados";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Guarda uma medição. Repetir a mesma nota do mesmo tipo/alvo não acrescenta nada (era a regra do
    /// JSON e continua a ser): o histórico não engorda com corridas idênticas.
    /// </summary>
    public static void Registar(RegistoBench r)
    {
        if (r.Nota <= 0) return;

        using var c = Abrir();

        using (var ultima = c.CreateCommand())
        {
            ultima.CommandText = "SELECT nota FROM resultados WHERE tipo = $tipo AND alvo = $alvo " +
                                 "ORDER BY quando DESC, id DESC LIMIT 1";
            ultima.Parameters.AddWithValue("$tipo", r.Tipo);
            ultima.Parameters.AddWithValue("$alvo", r.Alvo);
            if (ultima.ExecuteScalar() is double anterior && Math.Abs(anterior - r.Nota) < 0.001) return;
        }

        Inserir(c, new[] { r });
        Podar(c);
    }

    /// <summary>Insere vários (importação) e devolve quantos entraram de facto.</summary>
    public static int RegistarVarios(IEnumerable<RegistoBench> lista)
    {
        using var c = Abrir();
        int n = Inserir(c, lista);
        Podar(c);
        return n;
    }

    /// <summary>Mantém as 500 entradas mais recentes — o mesmo tecto que o JSON tinha.</summary>
    private static void Podar(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM resultados WHERE id NOT IN " +
                          "(SELECT id FROM resultados ORDER BY quando DESC, id DESC LIMIT 500)";
        cmd.ExecuteNonQuery();
    }

    public static List<RegistoBench> Todos(int limite = 500)
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT quando, tipo, alvo, nota, detalhe, versao FROM resultados " +
                          "ORDER BY quando, id LIMIT $limite";
        cmd.Parameters.AddWithValue("$limite", Math.Max(1, limite));

        var lista = new List<RegistoBench>();
        using var leitor = cmd.ExecuteReader();
        while (leitor.Read())
            lista.Add(new RegistoBench(leitor.GetString(1), leitor.GetString(2), leitor.GetDouble(3),
                leitor.GetString(4), Quando(leitor.GetString(0)), leitor.GetString(5)));
        return lista;
    }

    /// <summary>
    /// Pesquisa por texto (tipo, alvo ou detalhe) e/ou período. Devolve do mais recente para o mais
    /// antigo — é o que a lista da interface e o <c>--cli historico</c> mostram.
    /// </summary>
    public static List<RegistoBench> Pesquisar(string? texto = null, DateTime? desde = null, DateTime? ate = null, int limite = 200)
    {
        using var c = Abrir();

        var sql = new StringBuilder("SELECT quando, tipo, alvo, nota, detalhe, versao FROM resultados WHERE 1=1");
        if (!string.IsNullOrWhiteSpace(texto)) sql.Append(" AND (tipo LIKE $texto OR alvo LIKE $texto OR detalhe LIKE $texto)");
        if (desde is not null) sql.Append(" AND quando >= $desde");
        if (ate is not null) sql.Append(" AND quando <= $ate");
        sql.Append(" ORDER BY quando DESC, id DESC LIMIT $limite");

        using var cmd = c.CreateCommand();
        cmd.CommandText = sql.ToString();
        if (!string.IsNullOrWhiteSpace(texto)) cmd.Parameters.AddWithValue("$texto", "%" + texto.Trim() + "%");
        if (desde is not null) cmd.Parameters.AddWithValue("$desde", desde.Value.ToString(Formato, CultureInfo.InvariantCulture));
        if (ate is not null) cmd.Parameters.AddWithValue("$ate", ate.Value.ToString(Formato, CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$limite", Math.Max(1, limite));

        var lista = new List<RegistoBench>();
        using var leitor = cmd.ExecuteReader();
        while (leitor.Read())
            lista.Add(new RegistoBench(leitor.GetString(1), leitor.GetString(2), leitor.GetDouble(3),
                leitor.GetString(4), Quando(leitor.GetString(0)), leitor.GetString(5)));
        return lista;
    }

    public static int Contar()
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM resultados";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }

    /// <summary>Apaga o histórico (usado pelos testes e por um "limpar" explícito).</summary>
    public static void Limpar()
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM resultados";
        cmd.ExecuteNonQuery();
    }

    private static DateTime Quando(string texto) =>
        DateTime.TryParseExact(texto, Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : DateTime.MinValue;
}

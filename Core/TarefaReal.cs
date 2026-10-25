using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace LinuxExplode.Core;

/// <summary>
/// Resultado da carga "do mundo real": descomprimir um bloco fixo e compilar um ficheiro C fixo.
/// As duas notas são independentes (MB/s e kB/s) para poderem ser comparadas entre corridas sem
/// misturar unidades; o tempo total serve só para dizer quanto demorou a tarefa completa.
/// </summary>
public sealed record TarefaRealResultado(
    long BytesDescomprimidos,
    double DescompressaoMBs,
    string Sha256,
    double? CompilacaoSegundos,
    double? CompilacaoKBs,
    string? Compilador,
    string? CompilacaoMotivo,
    double TotalSegundos);

/// <summary>
/// Carga de trabalho fixa e repetível, sem rede: (1) descomprimir 48 MB de dados determinísticos e
/// verificar o SHA-256 do resultado — o trabalho é real e o resultado é conferido; (2) compilar um
/// ficheiro C gerado com 1 500 funções. Se não houver compilador, a segunda parte é declarada em
/// falta em vez de inventar um número; dentro do Flatpak usa-se o compilador do sistema anfitrião
/// por <c>flatpak-spawn --host</c>.
/// </summary>
public static class TarefaReal
{
    /// <summary>Conteúdo a descomprimir (comprime ~1:1, porque é pseudo-aleatório).</summary>
    public const int BytesPayload = 48 * 1024 * 1024;

    /// <summary>Funções C geradas — o tamanho da tarefa de compilação.</summary>
    public const int FuncoesC = 1500;

    public static TarefaRealResultado Correr(Action<string>? progresso = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        progresso?.Invoke($"a descomprimir {BytesPayload / (1024 * 1024)} MB…");
        var (bytes, sha, descompressaoMBs) = Descomprimir(ct);

        ct.ThrowIfCancellationRequested();

        progresso?.Invoke($"a compilar {FuncoesC} funções C…");
        var compilacao = Compilar(ct);

        sw.Stop();
        return new TarefaRealResultado(
            bytes, descompressaoMBs, sha,
            compilacao.Segundos, compilacao.KBs, compilacao.Compilador, compilacao.Motivo,
            sw.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Gera o bloco determinístico (xorshift64*), comprime-o para memória e mede só a descompressão.
    /// O SHA-256 do que sai é comparado com o do que entrou: se a descompressão falhar, o número é
    /// descartado em vez de reportado.
    /// </summary>
    private static (long Bytes, string Sha, double MBs) Descomprimir(CancellationToken ct)
    {
        var dados = new byte[BytesPayload];
        ulong estado = 0x2545F4914F6CDD1D;
        for (int i = 0; i < dados.Length; i++)
        {
            estado ^= estado >> 12;
            estado ^= estado << 25;
            estado ^= estado >> 27;
            dados[i] = (byte)(estado * 0x2545F4914F6CDD1D >> 56);
        }

        byte[] comprimido;
        using (var memoria = new MemoryStream())
        {
            using (var gz = new GZipStream(memoria, CompressionLevel.Fastest, leaveOpen: true))
                gz.Write(dados, 0, dados.Length);
            comprimido = memoria.ToArray();
        }

        ct.ThrowIfCancellationRequested();

        string sha;
        double mbs;
        using (var origem = new MemoryStream(comprimido))
        using (var gz = new GZipStream(origem, CompressionMode.Decompress))
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var buffer = new byte[1 << 20];
            long total = 0;

            var sw = Stopwatch.StartNew();
            int lidos;
            while ((lidos = gz.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, lidos);
                total += lidos;
            }
            sw.Stop();

            sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            mbs = total / (1024.0 * 1024) / Math.Max(0.0001, sw.Elapsed.TotalSeconds);
        }

        // Verificação a sério: a descompressão tem de devolver exactamente o que foi comprimido.
        var esperado = Convert.ToHexString(SHA256.HashData(dados)).ToLowerInvariant();
        if (sha != esperado)
            throw new InvalidOperationException("a descompressão não devolveu os dados originais (SHA-256 diferente)");

        return (dados.Length, sha, mbs);
    }

    private static (double? Segundos, double? KBs, string? Compilador, string? Motivo) Compilar(CancellationToken ct)
    {
        var fonte = GerarFonteC();
        var (comando, argumentos) = LocalizarCompilador();

        if (comando is null)
            return (null, null, null, "sem compilador C (cc/gcc/clang) neste sistema — a tarefa de compilação fica de fora");

        try
        {
            var psi = new ProcessStartInfo(comando)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var a in argumentos) psi.ArgumentList.Add(a);

            var sw = Stopwatch.StartNew();
            using var processo = Process.Start(psi) ?? throw new InvalidOperationException("não arrancou");
            var leituraSaida = processo.StandardOutput.ReadToEndAsync();
            var leituraErro = processo.StandardError.ReadToEndAsync();

            // O fonte entra por stdin: não precisa de ficheiros temporários nem de caminhos visíveis
            // dentro do sandbox (o output vai para stdout em assembly).
            processo.StandardInput.Write(fonte);
            processo.StandardInput.Close();

            if (!processo.WaitForExit(TimeSpan.FromMinutes(5)))
            {
                try { processo.Kill(entireProcessTree: true); } catch { }
                return (null, null, comando, "o compilador não terminou em 5 minutos");
            }

            sw.Stop();
            ct.ThrowIfCancellationRequested();

            string assembly = leituraSaida.GetAwaiter().GetResult();
            string erros = leituraErro.GetAwaiter().GetResult();

            if (processo.ExitCode != 0 || assembly.Length == 0)
                return (null, null, comando, $"o compilador falhou (código {processo.ExitCode}): {PrimeiraLinha(erros)}");

            double segundos = sw.Elapsed.TotalSeconds;
            double kbs = Encoding.UTF8.GetByteCount(fonte) / 1024.0 / Math.Max(0.0001, segundos);
            return (segundos, kbs, comando, null);
        }
        catch (Exception ex)
        {
            return (null, null, comando, $"não foi possível correr o compilador: {ex.Message}");
        }
    }

    /// <summary>
    /// Descobre o compilador: no sandbox do Flatpak só existe no anfitrião, por isso tenta-se
    /// <c>cc --version</c> localmente e depois <c>flatpak-spawn --host</c>.
    /// </summary>
    private static (string? Comando, List<string> Argumentos) LocalizarCompilador()
    {
        var args = new List<string> { "-O2", "-S", "-o", "/dev/stdout", "-xc", "-" };

        foreach (var nome in new[] { "cc", "gcc", "clang" })
        {
            var (ok, _) = Executar(nome, new[] { "--version" });
            if (ok) return (nome, args);

            if (Environment.GetEnvironmentVariable("FLATPAK_ID") is { Length: > 0 })
            {
                var (okAnfitriao, _) = Executar("flatpak-spawn", new[] { "--host", nome, "--version" });
                if (okAnfitriao) return ("flatpak-spawn", new[] { "--host", nome }.Concat(args).ToList());
            }
        }

        return (null, args);
    }

    private static (bool Ok, string Saida) Executar(string comando, IEnumerable<string> argumentos)
    {
        try
        {
            var psi = new ProcessStartInfo(comando)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var a in argumentos) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return (false, "");

            string saida = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return (p.ExitCode == 0, saida);
        }
        catch
        {
            return (false, "");
        }
    }

    /// <summary>
    /// Fonte C determinístico: funções com aritmética inteira e de vírgula flutuante e um ciclo
    /// curto em cada uma, para o optimizador ter trabalho real (não basta declarar funções vazias).
    /// </summary>
    public static string GerarFonteC()
    {
        var sb = new StringBuilder(1024 * 512);
        sb.AppendLine("/* tarefa de compilação do LINUX EXPLODE — gerada, não editar */");
        sb.AppendLine("#include <stdint.h>");
        sb.AppendLine("static double soma_total;");

        for (int f = 0; f < FuncoesC; f++)
        {
            sb.AppendLine($"int f{f}(int x) {{");
            sb.AppendLine($"    int64_t a = (int64_t)x * {f + 3} + 0x{(0x10000 + f * 7919) & 0xFFFF:X4};");
            sb.AppendLine("    int r = 0;");
            sb.AppendLine("    for (int i = 0; i < 16; i++) {");
            sb.AppendLine($"        a = (a ^ (a >> {3 + f % 5})) + i * {f % 13 + 1};");
            sb.AppendLine("        r += (int)(a & 0xFF);");
            sb.AppendLine("    }");
            sb.AppendLine($"    soma_total += (double)r / {(f % 7) + 1}.0;");
            sb.AppendLine("    return r ^ x;");
            sb.AppendLine("}");
        }

        sb.AppendLine("int main(void) { int t = 0;");
        for (int f = 0; f < FuncoesC; f++) sb.AppendLine($"    t += f{f}(t);");
        sb.AppendLine("    return t & 0x7F; }");
        return sb.ToString();
    }

    private static string PrimeiraLinha(string texto)
    {
        var linha = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        return string.IsNullOrEmpty(linha) ? "(sem mensagem)" : linha;
    }
}

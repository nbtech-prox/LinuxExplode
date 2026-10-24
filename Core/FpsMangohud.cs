using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>Resumo de uma sessão de FPS registada pelo MangoHud (CSV).</summary>
public sealed record SessaoFps
{
    public string Ficheiro { get; init; } = "";
    public string App { get; init; } = "";
    public string Os { get; init; } = "";
    public string Cpu { get; init; } = "";
    public string Gpu { get; init; } = "";
    public string Driver { get; init; } = "";
    public string Kernel { get; init; } = "";
    public int Amostras { get; init; }
    public double DuracaoSegundos { get; init; }
    public double FpsMedio { get; init; }
    public double FpsMinimo1 { get; init; }
    public double FpsMinimo01 { get; init; }
    public double FrametimeMedioMs { get; init; }
    public double FrametimePiorMs { get; init; }
    public double GpuLoad { get; init; }
    public double CpuLoad { get; init; }
    public double GpuTemp { get; init; }
    public double GpuPower { get; init; }

    public bool Valida => Amostras > 0;

    /// <summary>Linha de identificação do sistema, só com os campos preenchidos.</summary>
    public string Sistema => string.Join(" · ",
        new[] { Os, Cpu, Gpu, Driver, Kernel }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

/// <summary>
/// Leitura dos CSV do MangoHud — a forma honesta de ter FPS por aplicação em Wayland:
/// não existe API no compositor para introspecção global de frames, por isso usa-se o
/// Vulkan/OpenGL layer do MangoHud (que a app lança e depois lê).
///
/// Formato (src/logging.cpp do MangoHud): duas linhas de sistema
/// (<c>os,cpu,gpu,ram,kernel,driver,cpuscheduler</c> + valores), uma linha de colunas
/// <c>fps,frametime,cpu_load,cpu_power,gpu_load,cpu_temp,gpu_temp,gpu_core_clock,
/// gpu_mem_clock,gpu_vram_used,gpu_power,ram_used,swap_used,process_rss,cpu_mhz,elapsed</c>
/// e uma linha por amostra (<c>elapsed</c> em nanossegundos).
/// </summary>
public static class FpsMangohud
{
    private const string CabecalhoMetricas = "fps,frametime";

    /// <summary>Pastas onde o MangoHud costuma deixar os logs, por ordem de procura.</summary>
    public static List<string> Pastas()
    {
        var casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pastas = new List<string>();

        var daConfig = PastaDaConfig();
        if (daConfig is not null) pastas.Add(daConfig);

        var env = Environment.GetEnvironmentVariable("MANGOHUD_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(env)) pastas.Add(env);

        pastas.Add(Path.Combine(casa, "mangologs"));
        pastas.Add(Path.Combine(casa, ".local", "share", "MangoHud"));
        pastas.Add("/tmp/MangoHud");

        return pastas.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Lê <c>output_folder=</c> de ~/.config/MangoHud/MangoHud.conf, se existir.</summary>
    private static string? PastaDaConfig()
    {
        var casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var conf = Path.Combine(casa, ".config", "MangoHud", "MangoHud.conf");
        foreach (var linha in Util.Ler(conf).Split('\n'))
        {
            var l = linha.Trim();
            if (!l.StartsWith("output_folder", StringComparison.Ordinal)) continue;
            var i = l.IndexOf('=');
            if (i <= 0) continue;
            var valor = l[(i + 1)..].Trim().Trim('"');
            if (valor.Length > 0) return valor;
        }
        return null;
    }

    /// <summary>CSV mais recente entre as pastas conhecidas (null se não houver nenhum).</summary>
    public static string? Localizar()
    {
        string? melhor = null;
        DateTime melhorData = DateTime.MinValue;

        foreach (var pasta in Pastas())
        {
            if (!Directory.Exists(pasta)) continue;
            try
            {
                foreach (var f in Directory.EnumerateFiles(pasta, "*.csv", SearchOption.TopDirectoryOnly))
                {
                    var data = File.GetLastWriteTimeUtc(f);
                    if (data > melhorData) { melhorData = data; melhor = f; }
                }
            }
            catch { }
        }
        return melhor;
    }

    public static SessaoFps Analisar(string ficheiro)
    {
        var sessao = new SessaoFps { Ficheiro = ficheiro, App = NomeDaApp(ficheiro) };
        string[] linhas;
        try { linhas = File.ReadAllLines(ficheiro); }
        catch { return sessao; }

        int cabecalho = -1;
        for (int i = 0; i < linhas.Length; i++)
        {
            if (!linhas[i].StartsWith(CabecalhoMetricas, StringComparison.Ordinal)) continue;
            cabecalho = i;
            break;
        }
        if (cabecalho < 0) return LerResumo(linhas, sessao);

        // bloco de sistema: linha 0 = nomes, linha 1 = valores
        if (cabecalho >= 2)
        {
            var nomes = linhas[0].Split(',');
            var valores = linhas[1].Split(',');
            string Campo(string chave)
            {
                for (int i = 0; i < nomes.Length && i < valores.Length; i++)
                    if (nomes[i].Trim().Equals(chave, StringComparison.OrdinalIgnoreCase)) return valores[i].Trim();
                return "";
            }
            sessao = sessao with
            {
                Os = Campo("os"),
                Cpu = Campo("cpu"),
                Gpu = Campo("gpu"),
                Driver = Campo("driver"),
                Kernel = Campo("kernel")
            };
        }

        var frametimes = new List<double>();
        double somaGpu = 0, somaCpu = 0, somaTempGpu = 0, somaPotGpu = 0;
        int nGpu = 0, nCpu = 0, nTemp = 0, nPot = 0;
        double primeiroElapsed = 0, ultimoElapsed = 0;

        for (int i = cabecalho + 1; i < linhas.Length; i++)
        {
            var c = linhas[i].Split(',');
            if (c.Length < 16) continue;

            if (!Num(c[1], out var frametime) || frametime <= 0) continue;

            frametimes.Add(frametime);
            if (Num(c[4], out var gpuLoad) && gpuLoad >= 0) { somaGpu += gpuLoad; nGpu++; }
            if (Num(c[2], out var cpuLoad) && cpuLoad >= 0) { somaCpu += cpuLoad; nCpu++; }
            if (Num(c[6], out var tempGpu) && tempGpu > 0) { somaTempGpu += tempGpu; nTemp++; }
            if (Num(c[10], out var potGpu) && potGpu > 0) { somaPotGpu += potGpu; nPot++; }

            if (Num(c[15], out var elapsed))
            {
                if (frametimes.Count == 1) primeiroElapsed = elapsed;
                ultimoElapsed = elapsed;
            }
        }

        if (frametimes.Count == 0) return sessao;

        frametimes.Sort();
        double soma = 0;
        foreach (var f in frametimes) soma += f;

        return sessao with
        {
            Amostras = frametimes.Count,
            DuracaoSegundos = ultimoElapsed > primeiroElapsed ? (ultimoElapsed - primeiroElapsed) / 1e9 : 0,
            FpsMedio = 1000.0 / (soma / frametimes.Count),
            FpsMinimo1 = 1000.0 / MediaDosPiores(frametimes, 0.01),
            FpsMinimo01 = 1000.0 / MediaDosPiores(frametimes, 0.001),
            FrametimeMedioMs = soma / frametimes.Count,
            FrametimePiorMs = frametimes[^1],
            GpuLoad = nGpu > 0 ? somaGpu / nGpu : 0,
            CpuLoad = nCpu > 0 ? somaCpu / nCpu : 0,
            GpuTemp = nTemp > 0 ? somaTempGpu / nTemp : 0,
            GpuPower = nPot > 0 ? somaPotGpu / nPot : 0
        };
    }

    /// <summary>
    /// CSV de resumo que o MangoHud escreve ao lado do log
    /// (<c>0.1% Min FPS,1% Min FPS,97% Percentile FPS,Average FPS,…</c>).
    /// </summary>
    private static SessaoFps LerResumo(string[] linhas, SessaoFps sessao)
    {
        for (int i = 0; i < linhas.Length; i++)
        {
            var nomes = linhas[i].Split(',');
            int iMedia = Array.FindIndex(nomes, n => n.Trim().Equals("Average FPS", StringComparison.OrdinalIgnoreCase));
            if (iMedia < 0 || i + 1 >= linhas.Length) continue;

            var valores = linhas[i + 1].Split(',');
            double Campo(string chave)
            {
                int j = Array.FindIndex(nomes, n => n.Trim().Equals(chave, StringComparison.OrdinalIgnoreCase));
                return j >= 0 && j < valores.Length && Num(valores[j], out var v) ? v : 0;
            }

            return sessao with
            {
                FpsMedio = Campo("Average FPS"),
                FpsMinimo1 = Campo("1% Min FPS"),
                FpsMinimo01 = Campo("0.1% Min FPS"),
                FrametimeMedioMs = Campo("Average Frame Time"),
                GpuLoad = Campo("GPU Load"),
                CpuLoad = Campo("CPU Load"),
                GpuTemp = Campo("Average GPU Temp"),
                Amostras = 1   // o resumo não traz contagem de amostras
            };
        }
        return sessao;
    }

    /// <summary>Média dos piores <paramref name="fracao"/> dos frametimes (já ordenados).</summary>
    private static double MediaDosPiores(List<double> ordenados, double fracao)
    {
        int quantos = Math.Max(1, (int)Math.Round(ordenados.Count * fracao));
        double soma = 0;
        for (int i = ordenados.Count - quantos; i < ordenados.Count; i++) soma += ordenados[i];
        return soma / quantos;
    }

    private static bool Num(string texto, out double valor)
        => double.TryParse(texto.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);

    /// <summary>"Cyberpunk2077_2026-10-24_18-40-12.csv" → "Cyberpunk2077".</summary>
    private static string NomeDaApp(string ficheiro)
    {
        var nome = Path.GetFileNameWithoutExtension(ficheiro);
        if (nome.EndsWith("_summary", StringComparison.Ordinal)) nome = nome[..^"_summary".Length];
        var m = System.Text.RegularExpressions.Regex.Match(
            nome, @"^(?<app>.+?)_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}$");
        return m.Success ? m.Groups["app"].Value : nome;
    }
}

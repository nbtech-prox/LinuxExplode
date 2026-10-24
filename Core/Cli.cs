using System.Globalization;
using System.Text;

namespace LinuxExplode.Core;

/// <summary>Modo de linha de comandos: útil para servidores, scripts e verificação automática.</summary>
public static class Cli
{
    public static int Executar(string[] args)
    {
        var cmd = args.Length > 1 ? args[1] : "info";
        switch (cmd)
        {
            case "info":
                Info();
                return 0;
            case "freq":
                FreqInfo();
                return 0;
            case "sensores":
                SensoresInfo();
                return 0;
            case "potencia":
                PotenciaInfo();
                return 0;
            case "fps":
                return FpsInfo(args.Length > 2 ? args[2] : null);
            case "discos":
                DiscosInfo();
                return 0;
            case "rede":
                return Rede().GetAwaiter().GetResult();
            case "relatorio":
                RelatorioCli(args.Length > 2 ? args[2] : "html");
                return 0;
            case "bench":
                return Bench(args);
            default:
                Console.WriteLine("uso: LinuxExplode --cli [info|freq|sensores|potencia|fps|discos|rede|bench|relatorio] [html|json]\n" +
                              "     LinuxExplode --cli bench [multi|single|N|gpu [índice]|rt [índice]] [ram] [disco]");
                return 2;
        }
    }

    private static void Info()
    {
        var s = Sistema.Recolher();
        Console.WriteLine($"LINUX EXPLODE v{Relatorio.Versao}");
        Console.WriteLine($"  SO         {s.So}  (kernel {s.Kernel}, {s.Arquitetura})");
        Console.WriteLine($"  Máquina    {s.Maquina}");
        Console.WriteLine($"  Placa      {s.PlacaMae}");
        Console.WriteLine($"  BIOS       {s.Bios}");
        Console.WriteLine($"  CPU        {s.CpuModelo}");
        Console.WriteLine($"  Núcleos    {s.NucleosFisicos} físicos / {s.Threads} threads, {s.CpuFreqAtual} (máx {s.CpuFreqMax}), L3 {s.CpuCache}");
        Console.WriteLine($"  RAM        {Util.Bytes(s.RamTotal)} total, {Util.Bytes(s.RamLivre)} disponível, swap {Util.Bytes(s.SwapUsada)}/{Util.Bytes(s.SwapTotal)}");
        foreach (var m in s.Modulos)
            Console.WriteLine($"  {m.Posicao,-10} {m.Descricao}, {m.Fabricante} {m.PartNumber}" +
                              (m.VelocidadeJedecMts is int j && m.VelocidadeMts is int v && j != v ? $"  (JEDEC {j})" : "") +
                              (m.Fabrico.Length > 0 ? $"  {m.Fabrico}" : ""));
        Console.WriteLine($"  Uptime     {s.Uptime}");
        foreach (var g in s.Gpus)
            Console.WriteLine($"  GPU        {g.Nome}  [{g.Pci}, {g.Driver}]" +
                              (g.Temp is null ? "" : $"  {g.Temp}") +
                              (g.VramTotal is long vt ? $"  VRAM {Util.Bytes(g.VramUsada ?? 0)}/{Util.Bytes(vt)}" : ""));
    }

    private static void FreqInfo()
    {
        var s = Sistema.Recolher();
        var f = s.Freq;
        if (!f.Disponivel)
        {
            Console.WriteLine("sem leitura de frequência de CPU: o kernel não expõe /sys/devices/system/cpu/*/cpufreq");
            Console.WriteLine("(comum em ARM sem driver de escala e dentro de máquinas virtuais)");
        }
        else
        {
            Console.WriteLine($"frequências — {f.NucleosFisicos} núcleos físicos / {f.Threads.Count} threads" +
                              (f.Driver.Length > 0 ? $", {f.Driver}" : "") +
                              (f.Governador.Length > 0 ? $"/{f.Governador}" : ""));
            Console.WriteLine($"  agora    mín {FrequenciaCpu.Ghz(f.MinAtualKHz)} · média {FrequenciaCpu.Ghz(f.MediaAtualKHz)} · pico {FrequenciaCpu.Ghz(f.MaxAtualKHz)}");
            Console.WriteLine($"  limites  {FrequenciaCpu.Ghz(f.MinimoKHz)} – {FrequenciaCpu.Ghz(f.TurboKHz)} (turbo)");
            foreach (var n in f.PorNucleo())
                Console.WriteLine($"  núcleo {n.Nucleo,-3} " +
                                  string.Join("   ", n.Threads.Select(t => $"T{t.Cpu} {FrequenciaCpu.Ghz(t.AtualKHz)}")));
        }

        var gpus = FrequenciaGpu.Ler(s.Gpus);
        Console.WriteLine();
        if (gpus.Count == 0)
        {
            Console.WriteLine("GPU        sem relógio exposto pelo driver (NVIDIA não publica em sysfs)");
            return;
        }
        foreach (var g in gpus)
            Console.WriteLine($"GPU        {g.Nome}  [{g.Pci}]" +
                              $"  núcleo {FrequenciaGpu.Mhz(g.NucleoHz)} (máx {FrequenciaGpu.Mhz(g.NucleoMaxHz)})" +
                              (g.MemoriaMaxHz > 0
                                  ? $"  memória {FrequenciaGpu.Mhz(g.MemoriaHz)} (máx {FrequenciaGpu.Mhz(g.MemoriaMaxHz)})"
                                  : ""));
    }

    private static void SensoresInfo()
    {
        var lista = Core.Sensores.Ler();
        Console.WriteLine($"sensores hwmon: {lista.Count}");
        foreach (var g in lista.GroupBy(x => x.Chip))
        {
            Console.WriteLine($"- {g.Key}");
            foreach (var x in g) Console.WriteLine($"    {x.Nome,-22} {x.ValorTexto}");
        }
    }

    /// <summary>Consumo do PC: potência da CPU (RAPL) e das GPUs (hwmon), com total estimado.</summary>
    private static void PotenciaInfo()
    {
        Potencia.Ler();                      // 1.ª amostra só serve para ter referência dos contadores
        Thread.Sleep(1200);
        var p = Potencia.Ler();

        Console.WriteLine("consumo medido (CPU socket + GPUs) — não é o consumo da tomada");
        Console.WriteLine($"  CPU socket (RAPL)   {Potencia.Watts(p.PacoteW)}");
        if (p.NucleosW is not null) Console.WriteLine($"  CPU núcleos         {Potencia.Watts(p.NucleosW)}");
        if (p.PlataformaW is not null) Console.WriteLine($"  plataforma (psys)   {Potencia.Watts(p.PlataformaW)}");
        foreach (var g in p.Gpus)
            Console.WriteLine($"  GPU {g.Etiqueta,-12} {g.Nome,-7} {Potencia.Watts(g.Watts),7}" + (g.Integrada ? "  (integrada, dentro do pacote)" : ""));
        Console.WriteLine($"  total medido        {p.TotalTexto}  ({p.TotalRotulo})");
        if (p.EnergiaKWh is not null) Console.WriteLine($"  desde o arranque    {p.EnergiaKWh:0.000} kWh (pacote)");
        if (p.Aviso is not null) Console.WriteLine("  nota: " + p.Aviso);
    }

    /// <summary>Resumo de uma sessão do MangoHud (FPS, 1% low, frametimes).</summary>
    private static int FpsInfo(string? ficheiro)
    {
        var caminho = ficheiro ?? FpsMangohud.Localizar();
        if (caminho is null)
        {
            Console.WriteLine("sem logs do MangoHud.");
            Console.WriteLine("  o MangoHud só grava CSV com uma pasta de saída configurada, por exemplo:");
            Console.WriteLine("  MANGOHUD_CONFIG=\"output_folder=$HOME/mangologs,autostart_log=5\" mangohud <programa>");
            Console.WriteLine("  pastas procuradas: " + string.Join(", ", FpsMangohud.Pastas()));
            return 1;
        }

        var s = FpsMangohud.Analisar(caminho);
        Console.WriteLine("ficheiro   " + caminho);
        Console.WriteLine("aplicação  " + s.App);
        if (!s.Valida)
        {
            Console.WriteLine("  sem amostras válidas no CSV");
            return 1;
        }

        if (s.Sistema.Length > 0) Console.WriteLine("sistema    " + s.Sistema);
        Console.WriteLine($"amostras   {s.Amostras} · {s.DuracaoSegundos:0.0} s");
        Console.WriteLine($"FPS        médio {s.FpsMedio:0.0} · 1% low {s.FpsMinimo1:0.0} · 0.1% low {s.FpsMinimo01:0.0}");
        Console.WriteLine($"frametime  médio {s.FrametimeMedioMs:0.00} ms" +
                          (s.FrametimePiorMs > 0 ? $" · pior {s.FrametimePiorMs:0.00} ms" : ""));
        Console.WriteLine($"GPU        {s.GpuLoad:0}% · {s.GpuTemp:0} °C · {s.GpuPower:0} W · CPU {s.CpuLoad:0}%");
        return 0;
    }

    private static void DiscosInfo()
    {
        foreach (var d in Discos.Ler())
        {
            Console.WriteLine($"/dev/{d.Dev}  {d.Modelo}  {Util.Bytes(d.Tamanho)}  {(d.Ssd ? "SSD" : "HDD")}  saúde={d.Smart.Veredito}  temp={d.Smart.Temperatura ?? "—"}");
            if (d.Smart.Nota is not null) Console.WriteLine($"    {d.Smart.Nota}");
            foreach (var p in d.Particoes)
                Console.WriteLine($"    {p.Nome,-14} {p.Montagem,-18} {p.Fs,-6} " +
                                  (p.Total > 0 ? $"{Util.Bytes(p.Livre)} livres de {Util.Bytes(p.Total)}" : "não montada"));
        }
    }

    private static async Task<int> Rede()
    {
        Console.WriteLine("a testar rede…");
        var r = await Core.Rede.Correr(t => Console.WriteLine("  " + t));
        foreach (var a in r.Alvos)
            Console.WriteLine($"  {a.Nome,-20} {a.Media,7:0.0} ms  (min {a.Min:0.0}, max {a.Max:0.0}, jitter {a.Jitter:0.0}, perda {a.Perda:0}%)");
        Console.WriteLine($"  DNS {r.DnsMs:0} ms · download {r.DownloadMBs:0.0} MB/s · upload {r.UploadMBs:0.0} MB/s");
        Console.WriteLine($"  veredito: {r.Veredito} — {r.Detalhe}");
        return 0;
    }

    private static void RelatorioCli(string formato)
    {
        var (s, sensores, discos) = (Sistema.Recolher(), Sensores.Ler(), Discos.Ler());
        var texto = formato == "json" ? Relatorio.GerarJson(s, sensores, discos) : Relatorio.GerarHtml(s, sensores, discos);
        var caminho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            $"linux-explode-{DateTime.Now:yyyyMMdd-HHmmss}.{formato}");
        File.WriteAllText(caminho, texto);
        Console.WriteLine("gravado: " + caminho);
    }

    private static int Bench(string[] args)
    {
        var alvo = args.Length > 2 ? args[2] : "multi";
        if (alvo == "gpu") return BenchGpuCli(args.Length > 3 ? args[3] : null);
        if (alvo == "rt") return BenchRtCli(args.Length > 3 ? args[3] : null);

        // 2.º argumento opcional: threads ("multi" ou um número)
        int threads = Environment.ProcessorCount;
        if (args.Length > 2)
            threads = args[2] switch
            {
                "single" => 1,
                "multi" => Environment.ProcessorCount,
                _ => int.TryParse(args[2], out var n) ? n : Environment.ProcessorCount
            };

        Console.WriteLine($"EXPLODE BENCH — CPU com {threads} thread(s), {BenchRenderer.Width}x{BenchRenderer.Height}, {BenchRenderer.Spp} spp");
        var bench = new BenchRenderer();
        var ultimo = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cts = new CancellationTokenSource();
        var progresso = new Thread(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                int feito = bench.BucketsDone;
                if (feito != ultimo && feito % 20 == 0)
                {
                    Console.Write($"\r  {feito}/{bench.BucketsTotal} blocos…");
                    ultimo = feito;
                }
                Thread.Sleep(400);
            }
        }) { IsBackground = true };
        progresso.Start();

        var res = bench.Run(threads, TimeSpan.Zero, CancellationToken.None);
        cts.Cancel();
        sw.Stop();
        Console.WriteLine($"\r  nota: {res.Score:N0} pts   ({res.Threads}T, {res.Seconds:0.00}s, {res.Samples:N0} amostras)");

        if (args.Length > 3)
        {
            var extras = args[3..];
            if (extras.Contains("ram"))
            {
                Console.WriteLine("RAM:");
                var r = BenchRam.Correr(t => Console.Write($"\r  {t,-40}"));
                Console.WriteLine($"\r  escrita {r.EscritaMBs:N0} MB/s · leitura {r.LeituraMBs:N0} MB/s · cópia {r.CopiaMBs:N0} MB/s · latência {r.LatenciaNs:N1} ns");
            }
            if (extras.Contains("disco"))
            {
                var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "linuxexplode");
                Console.WriteLine("Disco:");
                var d = BenchDisco.Correr(pasta, 256, t => Console.Write($"\r  {t,-45}"));
                Console.WriteLine($"\r  seq escrita {d.SeqEscritaMBs:N0} MB/s · seq leitura {d.SeqLeituraMBs:N0} MB/s · " +
                                  $"4K leitura {d.RandLeituraMBs:N1} MB/s · 4K escrita {d.RandEscritaMBs:N1} MB/s");
            }
        }
        return 0;
    }

    private static int BenchRtCli(string? indice)
    {
        var gpus = BenchRt.Dispositivos();
        if (gpus.Count == 0)
        {
            Console.WriteLine("nenhuma GPU com VK_KHR_ray_query — o modo ray tracing não está disponível neste driver");
            return 1;
        }

        Console.WriteLine("GPUs com ray tracing (VK_KHR_ray_query):");
        foreach (var g in gpus)
            Console.WriteLine($"  [{g.Indice}] {g.Nome}  ({g.Tipo})");

        int escolhida = gpus[0].Indice;
        if (indice is not null && int.TryParse(indice, out var v) && gpus.Any(g => g.Indice == v))
            escolhida = v;

        Console.WriteLine($"\npath tracer + BVH por hardware — {BenchRt.LarguraPadrao}x{BenchRt.AlturaPadrao}, {BenchRt.AmostrasPadrao} spp…");
        var r = BenchRt.Correr(escolhida);
        if (r is null)
        {
            Console.WriteLine("falhou: não foi possível construir o BLAS/TLAS nesta GPU");
            return 1;
        }

        Console.WriteLine($"  {r.Nome}  ·  {r.Esferas} esferas no BVH");
        Console.WriteLine($"  {r.Segundos:0.000} s  ·  {r.Pontos:N0} pts  ·  {r.Largura}x{r.Altura}, {r.Amostras} spp, {r.Passagens} passagens");

        var destino = Environment.GetEnvironmentVariable("EXPLODE_GPU_DUMP");
        if (!string.IsNullOrWhiteSpace(destino))
        {
            using var fs = File.Create(destino);
            fs.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{r.Largura} {r.Altura}\n255\n"));
            for (int i = 0; i < r.Rgba.Length; i += 4)
            {
                fs.WriteByte(r.Rgba[i]); fs.WriteByte(r.Rgba[i + 1]); fs.WriteByte(r.Rgba[i + 2]);
            }
            Console.WriteLine("  render: " + destino);
        }
        return 0;
    }

    private static int BenchGpuCli(string? indice)
    {
        var gpus = BenchGpu.Dispositivos();
        if (gpus.Count == 0)
        {
            Console.WriteLine("nenhuma GPU Vulkan com fila de computação — instala o driver Vulkan (mesa-vulkan-drivers / vulkan-radeon)");
            return 1;
        }

        Console.WriteLine("GPUs disponíveis:");
        foreach (var g in gpus)
            Console.WriteLine($"  [{g.Indice}] {g.Nome}  ({g.Tipo})");

        int escolhida = gpus[0].Indice;
        if (indice is not null && int.TryParse(indice, out var v) && gpus.Any(g => g.Indice == v))
            escolhida = v;
        Console.WriteLine($"\npath tracer Vulkan — {BenchGpu.LarguraPadrao}x{BenchGpu.AlturaPadrao}, {BenchGpu.AmostrasPadrao} spp…");

        var r = BenchGpu.Correr(escolhida);
        if (r is null)
        {
            Console.WriteLine("falhou: não foi possível criar o dispositivo Vulkan (driver em falta?)");
            return 1;
        }

        Console.WriteLine($"  {r.Nome}");
        Console.WriteLine($"  {r.Segundos:0.000} s  ·  {r.Pontos:N0} pts  ·  {r.Largura}x{r.Altura}, {r.Amostras} spp, {r.Passagens} passagens");

        // depuração: despeja o render em PPM para inspeção
        var destino = Environment.GetEnvironmentVariable("EXPLODE_GPU_DUMP");
        if (!string.IsNullOrWhiteSpace(destino))
        {
            using var fs = File.Create(destino);
            var cabecalho = System.Text.Encoding.ASCII.GetBytes($"P6\n{r.Largura} {r.Altura}\n255\n");
            fs.Write(cabecalho);
            for (int i = 0; i < r.Rgba.Length; i += 4)
            {
                fs.WriteByte(r.Rgba[i]);
                fs.WriteByte(r.Rgba[i + 1]);
                fs.WriteByte(r.Rgba[i + 2]);
            }
            Console.WriteLine("  render: " + destino);
        }
        return 0;
    }
}

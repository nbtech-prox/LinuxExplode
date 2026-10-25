using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinuxExplode.Core;

/// <summary>Modo de linha de comandos: útil para servidores, scripts e verificação automática.</summary>
public static class Cli
{
    public static int Executar(string[] args)
    {
        var cmd = args.Length > 1 ? args[1] : "info";

        // Saída para scripts: --cli <comando> --json devolve um envelope JSON e nada mais.
        if (args.Any(a => a is "--json" or "-j"))
            return JsonCli(cmd, args);

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
                return RelatorioCli(args);
            case "bench":
                return Bench(args);
            case "stress":
                return Stress(args);
            default:
                Console.WriteLine("uso: LinuxExplode --cli [info|freq|sensores|potencia|fps|discos|rede|bench|stress|relatorio] [html|json]\n" +
                              "     LinuxExplode --cli bench [multi|single|N|gpu [índice]|rt [índice]] [ram] [disco]\n" +
                              "     LinuxExplode --cli stress [cpu|ram|gpu|disco|tudo] [segundos] [--threads N] [--carga inteiro|flutuante|vector|memoria] [--limite N]");
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

    private static int RelatorioCli(string[] args)
    {
        var formato = args.Length > 2 ? args[2].ToLowerInvariant() : "html";
        if (formato is not ("html" or "json"))
        {
            Console.Error.WriteLine("uso: --cli relatorio [html|json] [ficheiro de saída]");
            return 2;
        }

        var (s, sensores, discos) = (Sistema.Recolher(), Sensores.Ler(), Discos.Ler());
        var texto = formato == "json" ? Relatorio.GerarJson(s, sensores, discos) : Relatorio.GerarHtml(s, sensores, discos);

        var caminho = args.Length > 3
            ? Path.GetFullPath(args[3])
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                $"linux-explode-{DateTime.Now:yyyyMMdd-HHmmss}.{formato}");

        var pasta = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

        File.WriteAllText(caminho, texto);
        Console.WriteLine("gravado: " + caminho);
        return 0;
    }


    /// <summary>
    /// <c>--cli stress [cpu|ram|gpu|disco|tudo] [segundos]</c>: corre a mesma sessão da interface, sem
    /// janela, e imprime o resultado. Serve para verificar o stress em script/CI.
    /// </summary>
    private static int Stress(string[] args)
    {
        string alvoTexto = args.Length > 2 ? args[2].ToLowerInvariant() : "cpu";

        var alvo = alvoTexto switch
        {
            "cpu" => AlvoStress.Cpu,
            "ram" or "memoria" => AlvoStress.Ram,
            "gpu" => AlvoStress.Gpu,
            "disco" => AlvoStress.Disco,
            "tudo" => AlvoStress.Tudo,
            _ => (AlvoStress?)null
        };

        if (alvo is null)
        {
            Console.WriteLine("uso: LinuxExplode --cli stress [cpu|ram|gpu|disco|tudo] [segundos] [--threads N] [--carga inteiro|flutuante|vector|memoria] [--limite N]");
            return 2;
        }

        double segundos = 10;
        int threads = 0;
        var carga = CargaCpu.Vector;

        for (int i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--threads" when i + 1 < args.Length:
                    int.TryParse(args[++i], out threads);
                    break;
                case "--limite" when i + 1 < args.Length:
                    if (double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out double limite))
                    {
                        GuardaTermica.LimiteCpu = limite;
                        GuardaTermica.LimiteGpu = limite;
                    }
                    break;
                case "--carga" when i + 1 < args.Length:
                    carga = args[++i].ToLowerInvariant() switch
                    {
                        "inteiro" => CargaCpu.Inteiro,
                        "flutuante" => CargaCpu.Flutuante,
                        "memoria" or "memória" => CargaCpu.Memoria,
                        _ => CargaCpu.Vector
                    };
                    break;
                default:
                    if (double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                        segundos = Math.Clamp(s, 1, 3600);
                    break;
            }
        }

        threads = threads <= 0 ? Environment.ProcessorCount : threads;
        Console.WriteLine($"stress: {alvoTexto} · {segundos:0} s · CPU {threads} thread(s) · carga {carga}");
        Console.WriteLine($"limites: CPU {GuardaTermica.LimiteCpu:0} °C · GPU {GuardaTermica.LimiteGpu:0} °C");

        using var sessao = new SessaoStress(alvo.Value, TimeSpan.FromSeconds(segundos), threads, carga);
        var fim = new ManualResetEventSlim(false);
        ResultadoStress? resultado = null;
        DateTime inicio = DateTime.Now;
        string ultima = "";

        sessao.Progresso += p =>
        {
            string ritmo = string.IsNullOrEmpty(p.Ritmo) ? "" : $" · {p.Ritmo}";
            string linha = $"  [{p.Segundos,5:0.0} s] {p.Fase}{ritmo} · {p.Detalhe}";
            if (linha != ultima && DateTime.Now - inicio > TimeSpan.FromSeconds(2))
            {
                ultima = linha;
                Console.WriteLine(linha);
            }
        };

        sessao.Terminou += r => { resultado = r; fim.Set(); };
        sessao.Iniciar();

        // o tempo máximo é o da sessão + folga para o encerramento
        if (!fim.Wait(TimeSpan.FromSeconds(segundos + 180)))
            sessao.Parar();
        fim.Wait(TimeSpan.FromSeconds(30));

        if (resultado is null)
        {
            Console.WriteLine("stress: sem resultado (a sessão não terminou)");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"resultado: {(resultado.Completo ? "completo" : "parado")} — {resultado.Resumo}");
        foreach (var l in resultado.Linhas) Console.WriteLine("  " + l);
        Console.WriteLine($"picos: CPU {resultado.PicoCpuC:0} °C · GPU {resultado.PicoGpuC:0} °C · energia {resultado.EnergiaKwh * 1000:0.0} Wh");
        return resultado.Completo ? 0 : 1;
    }


    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// <c>--cli &lt;comando&gt; --json</c>: o mesmo que o comando normal, mas em JSON — para scripts,
    /// cron e monitorização. Nada de texto solto no stdout.
    /// </summary>
    private static int JsonCli(string cmd, string[] args)
    {
        try
        {
            switch (cmd)
            {
                case "info":
                    return EmitirJson("info", Sistema.Recolher());

                case "freq":
                    return EmitirJson("freq", FrequenciaCpu.Recolher());

                case "sensores":
                    return EmitirJson("sensores", Sensores.Ler());

                case "potencia":
                    // a potência sai de uma diferença entre duas leituras do contador RAPL: a primeira
                    // serve só para ter referência, o valor vem da segunda
                    Potencia.Ler();
                    Thread.Sleep(900);
                    return EmitirJson("potencia", Potencia.Ler());

                case "discos":
                    return EmitirJson("discos", new
                    {
                        discos = Discos.Ler(),
                        particoes = Util.Espaco().Select(p => new
                        {
                            nome = p.Key,
                            total = p.Value.Total,
                            livre = p.Value.Livre,
                            montagem = Util.Montagens().TryGetValue(p.Key, out var m) ? m.Montagem : null,
                            fs = Util.Montagens().TryGetValue(p.Key, out var m2) ? m2.Fs : null
                        })
                    });

                case "fps":
                    {
                        var ficheiros = FpsMangohud.Listar(10);
                        var sessoes = ficheiros.Select(f => FpsMangohud.Analisar(f)).ToList();
                        return EmitirJson("fps", new
                        {
                            pastas = FpsMangohud.Pastas(),
                            sessoes
                        });
                    }

                case "rede":
                    return EmitirJson("rede", Core.Rede.Correr().GetAwaiter().GetResult());

                case "relatorio":
                    {
                        // o relatório já é JSON: imprime-se tal e qual (sem envelope) para poder ser
                        // redireccionado para um ficheiro
                        var sis = Sistema.Recolher();
                        Console.WriteLine(Relatorio.GerarJson(sis, Sensores.Ler(), Discos.Ler()));
                        return 0;
                    }

                case "bench":
                    return BenchJson(args);

                case "stress":
                    return StressJson(args);

                default:
                    Console.Error.WriteLine(
                        "uso: LinuxExplode --cli [info|freq|sensores|potencia|fps|discos|rede|bench|stress|relatorio] --json");
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"erro: {ex.Message}");
            return 1;
        }
    }

    private static int BenchJson(string[] args)
    {
        var alvo = args.Length > 2 && !args[2].StartsWith("--") ? args[2].ToLowerInvariant() : "multi";

        switch (alvo)
        {
            case "gpu":
                {
                    var gpus = BenchGpu.Dispositivos();
                    var r = BenchGpu.Correr(0);
                    return EmitirJson("bench", new { modo = "gpu", gpus, resultado = r });
                }

            case "rt":
                {
                    var gpus = BenchGpu.Dispositivos();
                    var r = BenchRt.Correr(0);
                    return EmitirJson("bench", new { modo = "rt", gpus, resultado = r });
                }

            case "ram":
                return EmitirJson("bench", new { modo = "ram", resultado = BenchRam.Correr() });

            case "disco":
                {
                    var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".cache", "linuxexplode");
                    return EmitirJson("bench", new { modo = "disco", resultado = BenchDisco.Correr(pasta, 256) });
                }

            default:
                {
                    int threads = alvo switch
                    {
                        "single" => 1,
                        "multi" => Environment.ProcessorCount,
                        _ => int.TryParse(alvo, out int n) ? n : Environment.ProcessorCount
                    };

                    var renderizador = new BenchRenderer();
                    var r = renderizador.Run(threads, TimeSpan.Zero, CancellationToken.None);
                    return EmitirJson("bench", new { modo = "cpu", resultado = r });
                }
        }
    }

    private static int StressJson(string[] args)
    {
        string alvoTexto = args.Length > 2 && !args[2].StartsWith("--") ? args[2].ToLowerInvariant() : "cpu";

        var alvo = alvoTexto switch
        {
            "cpu" => AlvoStress.Cpu,
            "ram" or "memoria" => AlvoStress.Ram,
            "gpu" => AlvoStress.Gpu,
            "disco" => AlvoStress.Disco,
            "tudo" => AlvoStress.Tudo,
            _ => (AlvoStress?)null
        };

        if (alvo is null)
        {
            Console.Error.WriteLine("uso: LinuxExplode --cli stress [cpu|ram|gpu|disco|tudo] [segundos] --json");
            return 2;
        }

        double segundos = 10;
        int threads = 0;
        double limite = 0;

        for (int i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--threads" when i + 1 < args.Length: int.TryParse(args[++i], out threads); break;
                case "--limite" when i + 1 < args.Length:
                    double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out limite);
                    break;
                default:
                    if (double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                        segundos = Math.Clamp(s, 1, 3600);
                    break;
            }
        }

        if (limite > 0) { GuardaTermica.LimiteCpu = limite; GuardaTermica.LimiteGpu = limite; }

        using var sessao = new SessaoStress(alvo.Value, TimeSpan.FromSeconds(segundos),
            threads <= 0 ? Environment.ProcessorCount : threads);

        var fim = new ManualResetEventSlim(false);
        ResultadoStress? resultado = null;
        sessao.Terminou += r => { resultado = r; fim.Set(); };
        sessao.Iniciar();

        if (!fim.Wait(TimeSpan.FromSeconds(segundos + 180))) sessao.Parar();
        fim.Wait(TimeSpan.FromSeconds(30));

        if (resultado is null)
        {
            Console.Error.WriteLine("a sessão de stress não terminou");
            return 1;
        }

        EmitirJson("stress", resultado);
        return resultado.Completo ? 0 : 1;
    }

    private static int EmitirJson(string comando, object? dados)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            aplicacao = "LINUX EXPLODE",
            versao = Relatorio.Versao,
            comando,
            quando = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
            dados
        }, OpcoesJson));
        return 0;
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

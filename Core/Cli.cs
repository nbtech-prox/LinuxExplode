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
            case "ranking":
                return RankingCli();
            case "historico" or "histórico":
                return HistoricoCli(args);
            case "gravar":
                return GravarCli(args);
            case "stress":
                return Stress(args);
            case "autoteste" or "smart":
                return AutoTesteCli(args);
            case "ciclos":
                return CiclosCli(args);
            case "vram":
                return VramCli(args);
            case "suspender":
                return SuspenderCli(args);
            case "alertas":
                return AlertasCli();
            case "placa":
                return PlacaCli();
            case "usb":
                return UsbCli();
            case "audio" or "áudio":
                return AudioCli();
            default:
                Console.WriteLine("uso: LinuxExplode --cli [info|freq|sensores|potencia|fps|discos|rede|placa|usb|audio|bench|ranking|gravar|stress|relatorio|autoteste|ciclos|vram|suspender] [html|json]\n" +
                              "     LinuxExplode --cli bench [multi|single|N|gpu [índice]|rt [índice]|latmem|real] [ram] [disco] [--repeticoes N]\n" +
                              "     LinuxExplode --cli gravar [segundos]     (grava uma sessão de monitorização em CSV)\n" +
                              "     LinuxExplode --cli stress [cpu|ram|gpu|disco|tudo] [segundos] [--threads N] [--carga inteiro|flutuante|vector|memoria] [--limite N]\n" +
                              "     LinuxExplode --cli ciclos [cpu|ram|gpu|disco|tudo] [segundos] [--ciclos N] [--pausa SEG] [--threads N]\n" +
                              "     LinuxExplode --cli vram [--indice N] [--limite MB] [--bloco MB]     (0 = até o driver recusar)\n" +
                              "     LinuxExplode --cli autoteste [estado|curto|longo|parar] [disco]   (auto-teste S.M.A.R.T.)\n" +
                              "     LinuxExplode --cli suspender [estado|testar] [--segundos N] [--simular]\n" +
            "     LinuxExplode --cli historico [texto] [--desde AAAA-MM-DD] [--ate AAAA-MM-DD] [--limite N]");
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

    /// <summary><c>--cli alertas</c>: os mesmos avisos do cartão de sensores, em texto.</summary>
    private static int AlertasCli()
    {
        var alertas = Alertas.Avaliar();

        if (alertas.Count == 0)
        {
            Console.WriteLine("sem alertas — temperaturas, ventoinhas e espaço dentro dos limites");
            return 0;
        }

        foreach (var a in alertas)
            Console.WriteLine($"  [{(a.Grave ? "grave" : "aviso")}] {a.Titulo} — {a.Detalhe}");

        Console.WriteLine($"limites: CPU {Config.Atual.LimiteCpuAviso:0} °C · GPU {Config.Atual.LimiteGpuAviso:0} °C · " +
                          $"disco {Config.Atual.EspacoMinimoGb:0} GB · notificações {(Config.Atual.Notificacoes ? "ligadas" : "desligadas")}");
        return 0;
    }

    /// <summary><c>--cli placa</c>: links PCI Express e temperaturas de chipset/memória/rede/Wi-Fi.</summary>
    private static int PlacaCli()
    {
        var links = Placa.Links();
        var finais = links.Where(l => !l.Ponte).ToList();
        var pontes = links.Where(l => l.Ponte).ToList();
        var reduzidos = finais.Where(l => l.Reduzido).ToList();

        Console.WriteLine($"dispositivos PCI Express: {finais.Count} finais e {pontes.Count} pontes" +
                          (reduzidos.Count > 0
                              ? $"  — {reduzidos.Count} a correr abaixo do máximo anunciado"
                              : "  — todos os dispositivos finais negociaram o máximo que anunciam"));

        foreach (var l in finais)
        {
            var banda = l.BandaTeoricaGbS is double g ? $"  ≈ {Placa.Numero(g)} GB/s úteis (teórico)" : "";
            Console.WriteLine($"  {l.Endereco,-10} {l.TextoLink,-14} {l.Classe,-22} {l.Nome}{banda}");
            if (l.Reduzido)
                Console.WriteLine($"      anuncia {l.TextoMaximo} — o link em uso é mais estreito/lento" +
                                  " (slot, riser, ou poupança de energia do driver)");
        }

        Console.WriteLine();
        Console.WriteLine("pontes (o valor é o link que estão a servir):");
        foreach (var p in pontes)
            Console.WriteLine($"  {p.Endereco,-10} {p.TextoLink,-14} {p.Nome}");

        var temps = Placa.Temperaturas();
        Console.WriteLine();
        if (temps.Count == 0)
        {
            Console.WriteLine("temperaturas da placa: nenhum sensor fora da CPU e da GPU");
        }
        else
        {
            Console.WriteLine("temperaturas da placa (fora da CPU e da GPU):");
            foreach (var t in temps)
                Console.WriteLine($"  {t.Fonte,-10} {t.Chip,-28} {t.Nome,-22} {t.Texto}");
        }

        var faltas = Placa.SemSensor(temps);
        if (faltas.Count > 0)
            Console.WriteLine($"sem sensor nesta máquina: {string.Join(", ", faltas)} — a aplicação não inventa valores");
        return 0;
    }

    /// <summary><c>--cli usb</c>: dispositivos ligados, velocidade negociada e drivers.</summary>
    private static int UsbCli()
    {
        var todos = Usb.Listar();
        if (todos.Count == 0)
        {
            // Não é erro: num contentor ou num runner não há /sys/bus/usb enumerável. A mensagem diz
            // porquê e o código de saída fica 0 para o comando ser utilizável em pipelines e no CI.
            Console.WriteLine("sem dispositivos USB legíveis em /sys/bus/usb/devices");
            return 0;
        }

        var dispositivos = todos.Where(d => !d.Hub).ToList();
        var hubs = todos.Where(d => d.Hub || d.Id.StartsWith("usb", StringComparison.Ordinal)).ToList();

        Console.WriteLine($"USB: {dispositivos.Count} dispositivo(s) e {hubs.Count} concentrador(es)/controlador(es)");
        foreach (var d in dispositivos)
            Console.WriteLine("  " + Usb.Linha(d));
        if (hubs.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("concentradores e controladores:");
            foreach (var h in hubs)
                Console.WriteLine("  " + Usb.Linha(h));
        }
        return 0;
    }

    /// <summary><c>--cli audio</c>: placas ALSA, fluxos e xruns do PipeWire.</summary>
    private static int AudioCli()
    {
        var a = Core.Audio.Ler();
        Console.WriteLine(Core.Audio.Resumo(a));

        if (a.Placas.Count == 0)
        {
            Console.WriteLine("nenhuma placa de som em /proc/asound/cards");
        }
        else
        {
            Console.WriteLine();
            foreach (var p in a.Placas)
                Console.WriteLine($"  card{p.Indice}  {p.Id,-14} {p.Nome}  [{p.Driver}]" +
                                  (p.Barramento is { Length: > 0 } b ? $"  {b}" : ""));
        }

        var activos = a.Activos;
        Console.WriteLine();
        if (activos.Count == 0)
            Console.WriteLine("fluxos PCM: nenhum aberto agora (tudo \"closed\" em /proc/asound)");
        else
            foreach (var f in activos) Console.WriteLine("  " + f.Texto);

        if (a.Nos.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"PipeWire: {a.Nos.Count} nó(s) · {a.Xruns} xrun(s) acumulado(s) no servidor" +
                              (a.LatenciaPadraoMs is double l ? $" · quantum {l:0.00} ms" : ""));
            foreach (var n in a.Nos.Where(n => n.Xruns > 0).Take(8))
                Console.WriteLine($"  {n.Xruns,6} xrun(s)  {n.Nome}  ({n.Quantum} frames @ {n.Taxa} Hz)");
        }

        if (a.Aviso is not null) Console.WriteLine("nota: " + a.Aviso);
        return 0;
    }

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

        RelatorioCompleto.GuardarUltimoStress(resultado);

        Console.WriteLine();
        Console.WriteLine($"resultado: {(resultado.Completo ? "completo" : "parado")} — {resultado.Resumo}");
        foreach (var l in resultado.Linhas) Console.WriteLine("  " + l);
        Console.WriteLine($"picos: CPU {resultado.PicoCpuC:0} °C · GPU {resultado.PicoGpuC:0} °C · energia {resultado.EnergiaKwh * 1000:0.0} Wh");
        return resultado.Completo ? 0 : 1;
    }

    // ------------------------------------------------------------------ auto-teste S.M.A.R.T.

    /// <summary>
    /// <c>--cli autoteste [estado|curto|longo|parar] [disco]</c>: dispara e acompanha o auto-teste do
    /// disco. O smartctl pede o teste ao controlador e devolve logo o controlo — o progresso e o
    /// resultado aparecem no log do próprio disco e são lidos daí, não guardados pela aplicação.
    /// </summary>
    private static int AutoTesteCli(string[] args)
    {
        var (acao, dispositivo) = LerAcaoEDispositivo(args);
        var discos = Discos.Ler();
        var disco = EscolherDisco(discos, dispositivo);
        if (disco is null)
        {
            Console.WriteLine(dispositivo is null
                ? "nenhum disco físico encontrado em /sys/block"
                : $"disco não encontrado: {dispositivo} (vê a lista com --cli discos)");
            return 2;
        }

        Console.WriteLine($"EXPLODE SMART — auto-teste do disco {disco.Dev} ({disco.Modelo})");

        if (acao is "curto" or "longo")
        {
            var tipo = acao == "curto" ? TipoAutoTeste.Curto : TipoAutoTeste.Longo;
            var (ok, mensagem) = Discos.IniciarAutoTeste(disco.Dev, tipo);
            Console.WriteLine($"  pedido    {acao} → {(ok ? "iniciado" : "falhou")}: {mensagem}");
            MostrarAutoTeste(Discos.LerAutoTeste(disco.Dev));
            Console.WriteLine("  nota      o teste corre no disco; consulta com `--cli autoteste`");
            return ok ? 0 : 1;
        }

        if (acao == "parar")
        {
            var (ok, mensagem) = Discos.PararAutoTeste(disco.Dev);
            Console.WriteLine($"  parar     {(ok ? "ok" : "falhou")}: {mensagem}");
            return ok ? 0 : 1;
        }

        MostrarAutoTeste(acao == "estado" ? disco.AutoTeste : Discos.LerAutoTeste(disco.Dev));
        return 0;
    }

    private static void MostrarAutoTeste(AutoTesteInfo a)
    {
        Console.WriteLine($"  estado    {a.Estado}");
        Console.WriteLine($"  suporte   {(a.Suportado ? "sim" : "não")}");
        if (a.Historico.Count == 0)
        {
            Console.WriteLine("  histórico nenhum teste anterior");
        }
        else
        {
            Console.WriteLine($"  histórico {a.Historico.Count} teste(s), o mais recente primeiro:");
            foreach (var h in a.Historico.Take(6))
                Console.WriteLine($"    {h.Tipo,-9} {h.Resultado,-30} {h.HorasLigado?.ToString("N0") ?? "—",8} h ligado" +
                                  (h.Passou is bool p && !p ? "  FALHOU" : ""));
        }
        if (a.Nota is not null) Console.WriteLine("  nota      " + a.Nota);
    }

    /// <summary>Ação + disco a partir dos argumentos posicionais (ou <c>--dispositivo</c>).</summary>
    private static (string Acao, string? Dispositivo) LerAcaoEDispositivo(string[] args)
    {
        string acao = "estado";
        string? dispositivo = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] is "--json" or "-j") continue;
            if (args[i] == "--dispositivo" && i + 1 < args.Length) { dispositivo = args[++i]; continue; }
            if (args[i].StartsWith("--")) continue;
            if (acao == "estado" && args[i] is "estado" or "curto" or "longo" or "parar" or "listar")
                acao = args[i];
            else
                dispositivo = args[i];
        }
        return (acao, dispositivo?.Replace("/dev/", ""));
    }

    private static DiscoInfo? EscolherDisco(List<DiscoInfo> discos, string? dispositivo)
        => dispositivo is not null
            ? discos.FirstOrDefault(d => d.Dev == dispositivo)
            : discos.FirstOrDefault(d => d.Dev.StartsWith("nvme")) ?? discos.FirstOrDefault();

    /// <summary>Versão JSON do auto-teste: o pedido (se houve) e o estado lido do disco.</summary>
    private static object AutoTesteJson(string[] args)
    {
        var (acao, dispositivo) = LerAcaoEDispositivo(args);
        var disco = EscolherDisco(Discos.Ler(), dispositivo);
        if (disco is null)
            return new { erro = "sem discos", pedido = dispositivo };

        object? pedido = null;
        AutoTesteInfo estado;

        if (acao is "curto" or "longo")
        {
            var tipo = acao == "curto" ? TipoAutoTeste.Curto : TipoAutoTeste.Longo;
            var (ok, mensagem) = Discos.IniciarAutoTeste(disco.Dev, tipo);
            pedido = new { acao, ok, mensagem };
            estado = Discos.LerAutoTeste(disco.Dev);
        }
        else if (acao == "parar")
        {
            var (ok, mensagem) = Discos.PararAutoTeste(disco.Dev);
            pedido = new { acao, ok, mensagem };
            estado = Discos.LerAutoTeste(disco.Dev);
        }
        else
        {
            estado = acao == "estado" ? disco.AutoTeste : Discos.LerAutoTeste(disco.Dev);
        }

        return new { dispositivo = disco.Dev, modelo = disco.Modelo, acao, pedido, estado };
    }

    // ------------------------------------------------------------------ ciclos de stress

    /// <summary>
    /// <c>--cli ciclos [cpu|ram|gpu|disco|tudo] [segundos] --ciclos N --pausa S</c>: o mesmo teste
    /// repetido N vezes com pausa, para ver se a máquina repete o resultado ou degrada.
    /// </summary>
    private static int CiclosCli(string[] args)
    {
        var opcoes = LerOpcoesCiclos(args, out string alvoTexto);
        if (opcoes is null)
        {
            Console.WriteLine("uso: LinuxExplode --cli ciclos [cpu|ram|gpu|disco|tudo] [segundos] " +
                              "--ciclos N --pausa SEG [--threads N] [--limite N] [--carga inteiro|flutuante|vector|memoria]");
            return 2;
        }

        Console.WriteLine($"EXPLODE CICLOS — {alvoTexto} · {opcoes.Ciclos} ciclo(s) de {opcoes.Duracao.TotalSeconds:0} s" +
                          $", pausa {opcoes.Pausa.TotalSeconds:0} s");

        using var sessao = new SessaoCiclos(opcoes);
        var fim = new ManualResetEventSlim(false);
        ResultadoCiclos? resultado = null;
        var inicio = DateTime.Now;
        string ultima = "";

        sessao.Progresso += p =>
        {
            string linha = $"  [{p.Segundos,6:0.0} s] {p.Fase}" +
                           (string.IsNullOrEmpty(p.Ritmo) ? "" : $" · {p.Ritmo}") +
                           (string.IsNullOrEmpty(p.Detalhe) ? "" : $" · {p.Detalhe}");
            if (linha != ultima && DateTime.Now - inicio > TimeSpan.FromSeconds(2))
            {
                ultima = linha;
                Console.WriteLine(linha);
            }
        };
        sessao.Terminou += r => { resultado = r; fim.Set(); };
        sessao.Iniciar();

        var limite = opcoes.Ciclos * (opcoes.Duracao + opcoes.Pausa) + TimeSpan.FromSeconds(180);
        if (!fim.Wait(limite)) sessao.Parar();
        fim.Wait(TimeSpan.FromSeconds(30));

        if (resultado is null)
        {
            Console.WriteLine("ciclos: sem resultado (a série não terminou)");
            return 1;
        }

        RelatorioCompleto.GuardarUltimosCiclos(resultado);

        Console.WriteLine();
        Console.WriteLine($"resultado: {resultado.Resumo}");
        Console.WriteLine($"picos: CPU {resultado.PicoCpuC:0} °C · GPU {resultado.PicoGpuC:0} °C · " +
                          $"energia {resultado.EnergiaKwh * 1000:0.0} Wh em {resultado.Segundos:0} s");
        foreach (var l in resultado.Linhas) Console.WriteLine("  " + l);
        return resultado is { Falhas: 0, Parado: false } ? 0 : 1;
    }

    private static OpcoesCiclos? LerOpcoesCiclos(string[] args, out string alvoTexto)
    {
        alvoTexto = args.Length > 2 && !args[2].StartsWith("--") ? args[2].ToLowerInvariant() : "cpu";
        var alvo = alvoTexto switch
        {
            "cpu" => AlvoStress.Cpu,
            "ram" or "memoria" => AlvoStress.Ram,
            "gpu" => AlvoStress.Gpu,
            "disco" => AlvoStress.Disco,
            "tudo" => AlvoStress.Tudo,
            _ => (AlvoStress?)null
        };
        if (alvo is null) return null;

        double segundos = 10;
        int ciclos = 3, threads = 0, pausa = 30;
        var carga = CargaCpu.Vector;

        for (int i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--ciclos" when i + 1 < args.Length:
                    int.TryParse(args[++i], out ciclos);
                    ciclos = Math.Clamp(ciclos, 1, 100);
                    break;
                case "--pausa" when i + 1 < args.Length:
                    int.TryParse(args[++i], out pausa);
                    pausa = Math.Clamp(pausa, 0, 3600);
                    break;
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

        return new OpcoesCiclos(alvo.Value, ciclos, TimeSpan.FromSeconds(segundos), TimeSpan.FromSeconds(pausa),
            threads, carga);
    }

    private static object CiclosJson(string[] args)
    {
        var opcoes = LerOpcoesCiclos(args, out string alvoTexto);
        if (opcoes is null) return new { erro = "alvo inválido", uso = "cpu|ram|gpu|disco|tudo" };

        using var sessao = new SessaoCiclos(opcoes);
        var fim = new ManualResetEventSlim(false);
        ResultadoCiclos? resultado = null;
        sessao.Terminou += r => { resultado = r; fim.Set(); };
        sessao.Iniciar();

        var limite = opcoes.Ciclos * (opcoes.Duracao + opcoes.Pausa) + TimeSpan.FromSeconds(180);
        if (!fim.Wait(limite)) sessao.Parar();
        fim.Wait(TimeSpan.FromSeconds(30));

        if (resultado is not null) RelatorioCompleto.GuardarUltimosCiclos(resultado);
        return resultado ?? (object)new { erro = "a série não terminou", alvo = alvoTexto };
    }

    // ------------------------------------------------------------------ VRAM

    /// <summary>
    /// <c>--cli vram [--indice N] [--limite MB] [--bloco MB]</c>: enche a VRAM até ao limite (0 = até o
    /// driver recusar), escrevendo e relendo cada bloco. O limite por omissão é 4 GB para o teste não
    /// deixar o ambiente gráfico sem memória.
    /// </summary>
    private static int VramCli(string[] args)
    {
        var infos = StressVram.Dispositivos();
        if (infos.Count == 0)
        {
            Console.WriteLine("nenhuma GPU Vulkan com memória alocável — instala o driver Vulkan");
            return 1;
        }

        Console.WriteLine("GPUs disponíveis:");
        foreach (var g in infos)
            Console.WriteLine($"  [{g.Indice}] {g.Nome}  ({g.Tipo}) · heap {Util.Bytes(g.HeapBytes)}" +
                              (g.SysfsUsado is long u ? $" · {Util.Bytes(u)} em uso" : ""));

        int indice = LerInteiro(args, "--indice", infos[0].Indice, 0, 32);
        long limiteMb = LerLongo(args, "--limite", 4096, 0, 1024 * 1024);
        long blocoMb = LerLongo(args, "--bloco", 128, 16, 16384);
        long limite = limiteMb * 1024 * 1024;

        Console.WriteLine();
        Console.WriteLine($"a encher VRAM: limite {(limiteMb == 0 ? "até o driver recusar" : $"{limiteMb} MB")}, " +
                          $"blocos de {blocoMb} MB…");

        using var cts = new CancellationTokenSource();
        var resultado = StressVram.Correr(indice, limite, blocoMb * 1024 * 1024,
            p => Console.Write($"\r  {p,-58}"), cts.Token);

        RelatorioCompleto.GuardarUltimoVram(resultado);

        Console.WriteLine($"\r  GPU: {resultado.Gpu}");
        foreach (var l in resultado.Linhas) Console.WriteLine("  " + l);
        return resultado.ErrosVerificacao == 0 ? 0 : 1;
    }

    private static object VramJson(string[] args)
    {
        var infos = StressVram.Dispositivos();
        if (infos.Count == 0) return new { erro = "sem GPU Vulkan com memória alocável", gpus = infos };

        int indice = LerInteiro(args, "--indice", infos[0].Indice, 0, 32);
        long limiteMb = LerLongo(args, "--limite", 4096, 0, 1024 * 1024);
        long blocoMb = LerLongo(args, "--bloco", 128, 16, 16384);

        var resultado = StressVram.Correr(indice, limiteMb * 1024 * 1024, blocoMb * 1024 * 1024);
        RelatorioCompleto.GuardarUltimoVram(resultado);
        return new { gpus = infos, resultado };
    }

    // ------------------------------------------------------------------ suspender/retomar

    /// <summary>
    /// <c>--cli suspender [estado|testar] [--segundos N] [--simular]</c>: mede o tempo que a máquina
    /// esteve suspensa e se os sensores voltaram. <c>estado</c> só mostra o suporte e o último teste.
    /// </summary>
    private static int SuspenderCli(string[] args)
    {
        string acao = args.Length > 2 && !args[2].StartsWith("--") ? args[2].ToLowerInvariant() : "estado";

        if (acao is "testar" or "teste")
        {
            int segundos = LerInteiro(args, "--segundos", 15, 5, 3600);
            bool simular = args.Any(a => a == "--simular");

            var suporte = Suspender.Suporte();
            Console.WriteLine(suporte.PodeAcordarSozinho && !simular
                ? $"a suspender por {segundos} s — a máquina acorda sozinha com o rtcwake"
                : "a testar a suspensão — se a máquina dormir, acorda-a com uma tecla");
            Console.WriteLine("  " + suporte.Nota);

            var resultado = Suspender.Testar(segundos, simular, s => Console.WriteLine("  " + s));
            Console.WriteLine();
            Console.WriteLine("resultado: " + resultado.Veredito);
            foreach (var l in resultado.Linhas) Console.WriteLine("  " + l);
            Console.WriteLine($"  sensores  CPU {Snap(resultado.Antes.CpuC)} → {Snap(resultado.Depois.CpuC)} · " +
                              $"GPU {Snap(resultado.Antes.GpuC)} → {Snap(resultado.Depois.GpuC)}");
            return resultado.Suspendeu || simular ? 0 : 1;

            static string Snap(double? v) => v is double d ? $"{d:0} °C" : "—";
        }

        var suporte2 = Suspender.Suporte();
        Console.WriteLine("SUSPENSÃO — suporte deste sistema");
        Console.WriteLine($"  estados   {(suporte2.Estados.Length > 0 ? suporte2.Estados : "—")}");
        Console.WriteLine($"  modo      {suporte2.ModoMemoria ?? "—"}");
        Console.WriteLine($"  suspender {Sim(suporte2.PodeSuspender)} · acordar sozinho {Sim(suporte2.PodeAcordarSozinho)}");
        Console.WriteLine("  nota      " + suporte2.Nota);

        var ultimo = Suspender.LerUltimo();
        if (ultimo is null)
        {
            Console.WriteLine("  último    nenhum teste gravado");
            return 0;
        }

        Console.WriteLine($"  último    {ultimo.Quando:yyyy-MM-dd HH:mm} · {ultimo.Veredito}");
        foreach (var l in ultimo.Linhas) Console.WriteLine("            " + l);
        return 0;

        static string Sim(bool v) => v ? "sim" : "não";
    }

    private static object SuspenderJson(string[] args)
    {
        string acao = args.Length > 2 && !args[2].StartsWith("--") ? args[2].ToLowerInvariant() : "estado";
        var suporte = Suspender.Suporte();

        if (acao is "testar" or "teste")
        {
            int segundos = LerInteiro(args, "--segundos", 15, 5, 3600);
            bool simular = args.Any(a => a == "--simular");
            return new { suporte, simulado = simular, resultado = Suspender.Testar(segundos, simular) };
        }

        return new { suporte, ultimo = Suspender.LerUltimo() };
    }

    // ------------------------------------------------------------------ histórico (SQLite)

    /// <summary>
    /// <c>--cli historico [texto] [--desde AAAA-MM-DD] [--ate AAAA-MM-DD] [--limite N]</c>: procura no
    /// histórico (tipo, alvo ou detalhe) e/ou limita por período. Sem filtro mostra os mais recentes.
    /// </summary>
    private static int HistoricoCli(string[] args)
    {
        var (texto, desde, ate, limite) = LerFiltroHistorico(args);
        var lista = Historico.Pesquisar(texto, desde, ate, limite);
        int total = Historico.Todos().Count;

        Console.WriteLine($"HISTÓRICO — {lista.Count} de {total} resultado(s)" +
                          (texto is { Length: > 0 } ? $" · procura \"{texto}\"" : "") +
                          (desde is not null ? $" · desde {desde:yyyy-MM-dd}" : "") +
                          (ate is not null ? $" · até {ate:yyyy-MM-dd}" : ""));

        if (lista.Count == 0)
        {
            Console.WriteLine(total == 0 ? "  ainda sem resultados guardados" : "  nada encontrado com esse filtro");
            return 0;
        }

        foreach (var r in lista)
            Console.WriteLine($"  {r.Quando:yyyy-MM-dd HH:mm}  {r.Tipo,-6} {r.Alvo,-34} {r.Nota,12:N0}  {r.Detalhe}");
        return 0;
    }

    private static object HistoricoJson(string[] args)
    {
        var (texto, desde, ate, limite) = LerFiltroHistorico(args);
        return new
        {
            total = Historico.Todos().Count,
            filtro = new
            {
                texto,
                desde = desde?.ToString("yyyy-MM-dd"),
                ate = ate?.ToString("yyyy-MM-dd"),
                limite
            },
            resultados = Historico.Pesquisar(texto, desde, ate, limite)
        };
    }

    private static (string? Texto, DateTime? Desde, DateTime? Ate, int Limite) LerFiltroHistorico(string[] args)
    {
        string? texto = args.Length > 2 && !args[2].StartsWith("--") ? args[2] : null;
        DateTime? desde = null, ate = null;
        int limite = 200;

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--desde" when i + 1 < args.Length:
                    if (DateTime.TryParse(args[++i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                        desde = d.Date;
                    break;
                case "--ate" when i + 1 < args.Length:
                    if (DateTime.TryParse(args[++i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var a))
                        ate = a.Date.AddDays(1).AddSeconds(-1);
                    break;
                case "--limite" when i + 1 < args.Length:
                    int.TryParse(args[++i], out limite);
                    limite = Math.Clamp(limite, 1, 500);
                    break;
            }
        }

        return (texto, desde, ate, limite);
    }

    private static int LerLongo(string[] args, string nome, int porOmissao, int min, int max)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == nome && long.TryParse(args[i + 1], out long v))
                return (int)Math.Clamp(v, min, max);
        return porOmissao;
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

                case "ranking":
                    return EmitirJson("ranking", Historico.Rankings());

                case "historico" or "histórico":
                    return EmitirJson("historico", HistoricoJson(args));

                case "gravar":
                    return EmitirJson("gravar", GravarJson(args));

                case "bench":
                    return BenchJson(args);

                case "stress":
                    return StressJson(args);

                case "autoteste" or "smart":
                    return EmitirJson("autoteste", AutoTesteJson(args));

                case "ciclos":
                    return EmitirJson("ciclos", CiclosJson(args));

                case "vram":
                    return EmitirJson("vram", VramJson(args));

                case "suspender":
                    return EmitirJson("suspender", SuspenderJson(args));

                case "alertas":
                    return EmitirJson("alertas", Alertas.Avaliar());

                case "placa":
                    {
                        var links = Placa.Links();
                        var temps = Placa.Temperaturas();
                        return EmitirJson("placa", new
                        {
                            pci = links,
                            reduzidos = links.Count(l => l.Reduzido),
                            temperaturas = temps,
                            sem_sensor = Placa.SemSensor(temps)
                        });
                    }

                case "usb":
                    return EmitirJson("usb", Usb.Listar());

                case "audio" or "áudio":
                    return EmitirJson("audio", Core.Audio.Ler());

                default:
                    Console.Error.WriteLine(
                        "uso: LinuxExplode --cli [info|freq|sensores|potencia|fps|discos|rede|placa|usb|audio|bench|ranking|gravar|stress|relatorio] --json");
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

            case "latmem":
            case "latencia":
                {
                    var curva = BenchMemoria.Curva();
                    return EmitirJson("bench", new
                    {
                        modo = "latmem",
                        resumo = BenchMemoria.Resumo(curva),
                        pontos = curva.Select(p => new { bytes = p.Bytes, tamanho = p.Tamanho, fase = p.Fase, ns = Math.Round(p.Ns, 2) })
                    });
                }

            case "real":
                {
                    var r = TarefaReal.Correr();
                    return EmitirJson("bench", new { modo = "real", resultado = r });
                }

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

                    int repeticoes = LerInteiro(args, "--repeticoes", 1, 1, 9);
                    var corridas = new List<BenchResult>();
                    for (int i = 0; i < repeticoes; i++)
                        corridas.Add(new BenchRenderer().Run(threads, TimeSpan.Zero, CancellationToken.None));

                    var notas = corridas.Select(c => c.Score).ToList();
                    double mediana = Historico.Mediana(notas) ?? 0;
                    double variacao = Historico.Variacao(notas);

                    if (repeticoes > 1)
                        Historico.Registar("cpu", $"{threads} threads", mediana, $"mediana de {repeticoes} corridas");

                    return EmitirJson("bench", new
                    {
                        modo = "cpu",
                        threads,
                        repeticoes,
                        notas,
                        mediana,
                        variacao_pct = Math.Round(variacao, 2),
                        aviso = repeticoes >= Historico.CorridasRanking && variacao > RankingLocal.ToleranciaPct
                            ? $"variação {variacao:0.0} % acima dos {RankingLocal.ToleranciaPct:0} % — máquina instável"
                            : null,
                        resultado = corridas[^1]
                    });
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

        RelatorioCompleto.GuardarUltimoStress(resultado);
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
        if (alvo is "latmem" or "latencia" or "latência") return BenchLatenciaCli();
        if (alvo == "real") return BenchRealCli();

        // 2.º argumento opcional: threads ("multi" ou um número)
        int threads = Environment.ProcessorCount;
        if (args.Length > 2)
            threads = args[2] switch
            {
                "single" => 1,
                "multi" => Environment.ProcessorCount,
                _ => int.TryParse(args[2], out var n) ? n : Environment.ProcessorCount
            };

        // --repeticoes N: corre o mesmo teste N vezes e reporta a mediana (o ROADMAP pede 3). Uma
        // corrida isolada pode apanhar turbo a arrefecer ou outro programa a disputar o CPU; a mediana
        // de 3 é o que se pode comparar, e a variação diz se a máquina estava estável.
        int repeticoes = LerInteiro(args, "--repeticoes", 1, 1, 9);

        Console.WriteLine($"EXPLODE BENCH — CPU com {threads} thread(s), {BenchRenderer.Width}x{BenchRenderer.Height}, {BenchRenderer.Spp} spp" +
                          (repeticoes > 1 ? $" · {repeticoes} corridas" : ""));

        var notas = new List<double>();
        BenchResult? ultimo = null;

        for (int i = 0; i < repeticoes; i++)
        {
            if (repeticoes > 1) Console.WriteLine($"  corrida {i + 1}/{repeticoes}…");
            ultimo = CorrerCpu(threads, comProgresso: repeticoes == 1);
            notas.Add(ultimo.Score);
            if (repeticoes > 1) Console.WriteLine($"    nota: {ultimo.Score:N0} pts   ({ultimo.Seconds:0.00}s)");
        }

        if (repeticoes > 1)
        {
            double mediana = Historico.Mediana(notas) ?? 0;
            double variacao = Historico.Variacao(notas);
            Console.WriteLine($"  mediana de {repeticoes}: {mediana:N0} pts   (variação {variacao:0.0} %)");
            if (variacao > RankingLocal.ToleranciaPct)
                Console.WriteLine($"  aviso: variação acima dos {RankingLocal.ToleranciaPct:0} % — a máquina não está estável; vale a mediana.");

            Historico.Registar("cpu", $"{threads} threads", mediana, $"mediana de {repeticoes} corridas");
        }
        else if (ultimo is not null)
        {
            Console.WriteLine($"\r  nota: {ultimo.Score:N0} pts   ({ultimo.Threads}T, {ultimo.Seconds:0.00}s, {ultimo.Samples:N0} amostras)");
            Historico.Registar("cpu", $"{threads} threads", ultimo.Score, ultimo.Seconds.ToString("0.00") + " s");
        }

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

    /// <summary>Uma corrida de CPU, com o contador de blocos no stdout quando há terminal para isso.</summary>
    private static BenchResult CorrerCpu(int threads, bool comProgresso)
    {
        var bench = new BenchRenderer();
        if (!comProgresso) return bench.Run(threads, TimeSpan.Zero, CancellationToken.None);

        var ultimo = 0;
        using var cts = new CancellationTokenSource();
        var progresso = new Thread(() =>
        {
            while (!cts.Token.IsCancellationRequested)
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
        return res;
    }

    /// <summary>Lê <c>--nome N</c> de uma lista de argumentos, com limites. Devolve o valor por omissão.</summary>
    private static int LerInteiro(string[] args, string nome, int porOmissao, int min, int max)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == nome && int.TryParse(args[i + 1], out int v))
                return Math.Clamp(v, min, max);
        return porOmissao;
    }

    /// <summary>Ranking local: mediana das últimas corridas por tipo/alvo, com aviso de variação.</summary>
    private static int RankingCli()
    {
        var linhas = Historico.Rankings();

        if (linhas.Count == 0)
        {
            Console.WriteLine("ranking local: ainda não há corridas guardadas — corre `--cli bench` primeiro");
            return 0;
        }

        Console.WriteLine($"RANKING LOCAL — mediana das últimas {Historico.CorridasRanking} corridas de cada alvo");
        foreach (var l in linhas)
        {
            Console.WriteLine($"  {l.Tipo,-8} {l.Alvo,-22} {l.Linha}   ({l.Corridas} corrida(s), melhor {l.Melhor:N0})");
            if (l.Aviso is not null) Console.WriteLine($"           ! {l.Aviso}");
        }
        return 0;
    }

    /// <summary>Curva de latência de memória por tamanho de bloco (4 KB → 256 MB).</summary>
    private static int BenchLatenciaCli()
    {
        Console.WriteLine("EXPLODE BENCH — latência de memória por tamanho de bloco (4 KB → 256 MB)");
        var curva = BenchMemoria.Curva(t => Console.Write($"\r  {t,-58}"));

        Console.WriteLine("\r  tamanho   fase   latência");
        foreach (var p in curva)
            Console.WriteLine($"  {p.Tamanho,7}   {p.Fase,-4}   {p.Ns,6:0.0} ns   ({p.Bytes:N0} B)");

        Console.WriteLine($"  resumo: {BenchMemoria.Resumo(curva)}");
        return 0;
    }

    /// <summary>Carga "do mundo real": descomprimir um bloco fixo e compilar C, sem rede.</summary>
    private static int BenchRealCli()
    {
        Console.WriteLine($"EXPLODE BENCH — tarefa real: descomprimir {TarefaReal.BytesPayload / (1024 * 1024)} MB e compilar {TarefaReal.FuncoesC} funções C");
        var r = TarefaReal.Correr(t => Console.Write($"\r  {t,-58}"));

        Console.WriteLine($"\r  descompressão  {r.DescompressaoMBs,8:N0} MB/s   (SHA-256 {r.Sha256[..16]}… verificado)");
        if (r.CompilacaoSegundos is double s)
            Console.WriteLine($"  compilação     {r.CompilacaoKBs,8:N0} kB/s   ({s:0.00} s, {r.Compilador})");
        else
            Console.WriteLine($"  compilação     —          {r.CompilacaoMotivo}");

        Console.WriteLine($"  total          {r.TotalSegundos:0.00} s");
        Historico.Registar("real", $"gzip {TarefaReal.BytesPayload / (1024 * 1024)} MB", r.DescompressaoMBs, "MB/s");
        if (r.CompilacaoKBs is double kbs)
            Historico.Registar("real", $"compilar {TarefaReal.FuncoesC} funções", kbs, "kB/s de fonte");
        return 0;
    }

    /// <summary>Grava uma sessão de monitorização em CSV, uma amostra por segundo.</summary>
    private static int GravarCli(string[] args)
    {
        int segundos = 10;
        for (int i = 2; i < args.Length; i++)
            if (int.TryParse(args[i], out int v)) segundos = Math.Clamp(v, 1, 3600);

        using var gravador = new GravadorSessao();
        string caminho = gravador.Iniciar();
        Console.WriteLine($"a gravar {segundos} s em {caminho}");

        for (int i = 0; i < segundos; i++)
        {
            var a = gravador.Amostrar();
            Console.Write($"\r  {i + 1}/{segundos}  CPU {(a.CpuC is double c ? $"{c:0} °C" : "—"),-7}" +
                          $"GPU {(a.GpuC is double g ? $"{g:0} °C" : "—"),-7}" +
                          $"{(a.Watts is double w ? $"{w:0.0} W" : "—")}");
            Thread.Sleep(1000);
        }

        var (amostras, resumo) = gravador.Parar();
        Console.WriteLine($"\r  {amostras.Count} amostras em {resumo.Duracao.TotalSeconds:0} s");

        static string S((double? Min, double? Max, double? Media) t, string unidade) =>
            t.Media is double m ? $"média {m:0.0} {unidade} · min {t.Min:0.0} · max {t.Max:0.0}" : "sem leitura";

        Console.WriteLine($"  CPU   {S(resumo.Cpu, "°C")}");
        Console.WriteLine($"  GPU   {S(resumo.Gpu, "°C")}");
        Console.WriteLine($"  watts {S(resumo.Watts, "W")}");
        Console.WriteLine($"  freq  {S(resumo.Freq, "MHz")}");
        if (gravador.FalhouEscrita) Console.WriteLine("  aviso: o CSV não pôde ser escrito (permissões/disco) — as amostras ficaram só em memória");
        Console.WriteLine($"  ficheiro: {caminho}");
        return 0;
    }

    /// <summary>Versão JSON da gravação: nada é impresso além do envelope.</summary>
    private static object GravarJson(string[] args)
    {
        // args[2] é o número de segundos (o --json é o envelope, não um argumento da gravação); o
        // varrimento tem de começar em 2 ou `--cli gravar 3 --json` gravava os 10 s por omissão.
        int segundos = 10;
        for (int i = 2; i < args.Length; i++)
            if (int.TryParse(args[i], out int v)) segundos = Math.Clamp(v, 1, 3600);

        using var gravador = new GravadorSessao();
        string caminho = gravador.Iniciar();

        for (int i = 0; i < segundos; i++)
        {
            gravador.Amostrar();
            Thread.Sleep(1000);
        }

        var (amostras, resumo) = gravador.Parar();
        return new
        {
            caminho,
            duracao_s = Math.Round(resumo.Duracao.TotalSeconds, 1),
            amostras = amostras.Count,
            falhou_escrita = gravador.FalhouEscrita,
            cpu = new { min = resumo.Cpu.Min, max = resumo.Cpu.Max, media = resumo.Cpu.Media },
            gpu = new { min = resumo.Gpu.Min, max = resumo.Gpu.Max, media = resumo.Gpu.Media },
            watts = new { min = resumo.Watts.Min, max = resumo.Watts.Max, media = resumo.Watts.Media },
            freq_mhz = new { min = resumo.Freq.Min, max = resumo.Freq.Max, media = resumo.Freq.Media }
        };
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

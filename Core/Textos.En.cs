namespace LinuxExplode.Core;

/// <summary>Traduções para inglês (en-GB). A chave é o texto original em pt-PT.</summary>
public static partial class Textos
{
    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        // navegação e moldura
        ["Sistema"] = "System",
        ["Sensores"] = "Sensors",
        ["Discos e saúde"] = "Disks and health",
        ["Benchmark"] = "Benchmark",
        ["Stress"] = "Stress",
        ["Rede"] = "Network",
        ["Dispositivos"] = "Devices",
        ["Relatório"] = "Report",
        ["Tema claro"] = "Light theme",
        ["Tema escuro"] = "Dark theme",
        ["Escala da interface (Ctrl+- e Ctrl++)"] = "Interface scale (Ctrl+- and Ctrl++)",
        ["a ler hardware…"] = "reading hardware…",
        ["leitura de hardware indisponível"] = "hardware read unavailable",
        ["hardware · sensores · bench"] = "hardware · sensors · bench",

        // bandeja
        ["Abrir janela"] = "Open window",
        ["Fechar para a bandeja"] = "Close to tray",
        ["Parar todo o stress"] = "Stop all stress",
        ["Parar todo o stress ({0})"] = "Stop all stress ({0})",
        ["Sair"] = "Quit",
        ["a ler sensores…"] = "reading sensors…",
        ["não havia stress a correr"] = "no stress was running",
        ["stress parado ({0} sessão/ões)"] = "stress stopped ({0} session(s))",
        ["stress {0}"] = "stress {0}",
        ["LINUX EXPLODE · sem leitura de sensores"] = "LINUX EXPLODE · no sensor readings",

        // página Sistema
        ["Informação de hardware lida diretamente de /sys, /proc e DMI"] =
            "Hardware information read directly from /sys, /proc and DMI",
        ["Falha a ler o hardware: "] = "Failed to read hardware: ",
        ["Sistema operativo"] = "Operating system",
        ["Processador"] = "Processor",
        ["Memória"] = "Memory",
        ["Placa-mãe e BIOS"] = "Motherboard and BIOS",
        ["Placa de vídeo"] = "Graphics card",
        ["Nenhuma GPU detetada em /sys/class/drm."] = "No GPU found in /sys/class/drm.",
        ["threads"] = "threads",
        ["{0} núcleos"] = "{0} cores",
        ["{0} em uso"] = "{0} in use",
        ["frequência"] = "frequency",
        ["máx {0}"] = "max {0}",
        ["ligado há"] = "up for",
        ["Distribuição"] = "Distribution",
        ["Arquitetura"] = "Architecture",
        ["Máquina"] = "Machine",
        ["Modelo"] = "Model",
        ["Núcleos"] = "Cores",
        ["Frequência"] = "Frequency",
        ["Máxima"] = "Maximum",
        ["Instalada"] = "Installed",
        ["Em uso"] = "In use",
        ["Disponível"] = "Available",
        ["sem swap"] = "no swap",
        ["{0} de {1}"] = "{0} of {1}",
        ["Sem SPD legível sem root: em DDR5 é preciso o módulo spd5118 carregado " +
         "(expõe /sys/bus/nvmem/devices) e em DDR4 o ee1004."] =
            "No readable SPD without root: DDR5 needs the spd5118 module loaded " +
            "(it exposes /sys/bus/nvmem/devices) and DDR4 needs ee1004.",
        ["Placa-mãe"] = "Motherboard",
        ["Barramento PCI"] = "PCI bus",
        ["Driver"] = "Driver",
        ["Agora"] = "Now",
        ["{0}   ·   uso {1}"] = "{0}   ·   use {1}",
        ["Estados"] = "States",
        ["Modo em memória"] = "Memory mode",
        ["Suspender"] = "Suspend",
        ["sim (sudo sem password)"] = "yes (passwordless sudo)",
        ["não — falta permissão"] = "no — permission missing",
        ["Acordar sozinho"] = "Wake by itself",
        ["sim (rtcwake)"] = "yes (rtcwake)",
        ["não — acorda-se à mão"] = "no — must be woken by hand",
        ["DORMIR"] = "SLEEP",
        ["Testar suspensão"] = "Test suspend",
        ["Simular (sem dormir)"] = "Simulate (without sleeping)",
        ["último teste: {0} · {1}"] = "last test: {0} · {1}",
        ["Suspender e retomar"] = "Suspend and resume",
        ["simulação: a ler os relógios…"] = "simulation: reading the clocks…",
        ["a suspender…"] = "suspending…",
        ["o teste falhou: {0}"] = "the test failed: {0}",
        ["relógio de parede {0} s · suspend medido {1} s · uptime confirma {2} s"] =
            "wall clock {0} s · suspend measured {1} s · uptime confirms {2} s",
        ["sensores CPU {0} → {1} · GPU {2} → {3} · potência {4} → {5}"] =
            "sensors CPU {0} → {1} · GPU {2} → {3} · power {4} → {5}",
        ["sensores e potência voltaram a responder depois do wake"] =
            "sensors and power responded again after the wake",
        ["há leituras que não voltaram depois do wake — o cartão mostra quais"] =
            "some readings did not come back after the wake — the card shows which",
        ["pronto"] = "ready",
        ["Módulo {0}"] = "Module {0}",
        ["fabrico {0}-W{1}"] = "made {0}-W{1}",

        // página Sensores
        ["Bom"] = "Good",
        ["Atencao"] = "Warning",
        ["Mau"] = "Bad",
        ["a medir…"] = "measuring…",
        ["sem alertas"] = "no alerts",
        ["sem gravação"] = "not recording",
        ["Notificações do sistema"] = "System notifications",
        ["Todos"] = "All",
        ["Temperatura"] = "Temperature",
        ["Ventoinha"] = "Fan",
        ["Potência"] = "Power",
        ["Tensão"] = "Voltage",
        ["ao vivo · 1 s"] = "live · 1 s",
        ["Nenhum sensor de frequência em /sys/class/hwmon — o detalhe por núcleo acima vem de cpufreq."] =
            "No frequency sensor in /sys/class/hwmon — the per-core detail above comes from cpufreq.",
        ["Nenhum sensor de potência em /sys/class/hwmon — o consumo acima vem do RAPL (CPU) e dos contadores das GPUs."] =
            "No power sensor in /sys/class/hwmon — the power above comes from RAPL (CPU) and the GPU counters.",
        ["Nenhum sensor disponível nesta categoria. Em muitos portáteis é preciso carregar o módulo " +
         "do kernel (ex.: sudo modprobe nct6775) ou o CPU expõe apenas k10temp."] =
            "No sensors available in this category. On many laptops you must load the kernel " +
            "module (e.g. sudo modprobe nct6775), or the CPU only exposes k10temp.",
        ["CPU — socket (RAPL)"] = "CPU — socket (RAPL)",
        ["CPU — núcleos (RAPL)"] = "CPU — cores (RAPL)",
        ["Plataforma (psys, RAPL)"] = "Platform (psys, RAPL)",
        ["Total medido"] = "Measured total",
        ["Energia desde o arranque"] = "Energy since boot",
        ["Consumo medido — CPU (socket) e GPUs"] = "Measured power — CPU (socket) and GPUs",
        ["Isto NÃO é o consumo da tomada: mede o socket da CPU (RAPL, inclui núcleos, I/O e gráfica " +
         "integrada) e as GPUs. Placa-mãe, memória, discos, ventoinhas e as perdas da fonte não têm sensor " +
         "nesta máquina — só uma tomada inteligente/UPS ou uma fonte com telemetria daria o total real."] =
            "This is NOT wall-socket power: it measures the CPU socket (RAPL, includes cores, I/O and the " +
            "integrated graphics) and the GPUs. Motherboard, memory, disks, fans and PSU losses have no sensor " +
            "on this machine — only a smart plug/UPS or a PSU with telemetry would give the real total.",
        ["sem leitura de frequência de CPU — o kernel não expõe cpufreq " +
         "(comum em ARM sem driver de escala e em máquinas virtuais)."] =
            "no CPU frequency reading — the kernel does not expose cpufreq " +
            "(common on ARM without a scaling driver and in virtual machines).",
        ["CPU — frequência por núcleo"] = "CPU — frequency per core",
        ["Núcleo {0}"] = "Core {0}",
        ["últimos 60 s · uma amostra por segundo · temperaturas em °C, potência em W, frequência em GHz"] =
            "last 60 s · one sample per second · temperatures in °C, power in W, frequency in GHz",
        ["Ao vivo"] = "Live",
        ["Throttling — frequência efectiva vs máxima"] = "Throttling — effective vs maximum frequency",
        ["tarifa (€/kWh)"] = "rate (€/kWh)",
        ["Energia e custo"] = "Energy and cost",
        ["sem sensor"] = "no sensor",
        ["sem RAPL"] = "no RAPL",
        ["o firmware não expõe contadores de throttle (normal em AMD)"] =
            "the firmware exposes no throttle counters (normal on AMD)",
        ["sem leitura de cpufreq"] = "no cpufreq reading",
        ["a média é a do tempo em que a janela esteve aberta (inclui o PC parado)"] =
            "the average covers the time the window was open (including an idle PC)",
        ["Nenhum sensor de temperatura fora da CPU e da GPU nesta máquina."] =
            "No temperature sensor besides the CPU and GPU on this machine.",
        ["Placa-mãe, chipset e memória"] = "Motherboard, chipset and memory",
        ["sensores lidos de /sys/class/hwmon; uma leitura ausente fica em branco em vez de zero."] =
            "sensors read from /sys/class/hwmon; a missing reading stays blank instead of zero.",
        ["Grava CPU, GPU, potência e frequência uma vez por segundo em " +
         "~/.local/share/linux-explode/sessoes. No fim ficam aqui o resumo e o gráfico da sessão inteira."] =
            "Records CPU, GPU, power and frequency once per second into " +
            "~/.local/share/linux-explode/sessoes. At the end the summary and the whole-session chart appear here.",
        ["Gravar sessão"] = "Record session",
        ["Sessão gravada · monitorização em CSV"] = "Recorded session · CSV monitoring",
        ["Parar e ver"] = "Stop and view",
        ["nada gravado"] = "nothing recorded",
        ["sem leitura"] = "no reading",
        ["Avisa com notify-send quando um limite é passado (no máximo um aviso por regra a cada 15 min)"] =
            "Warns with notify-send when a limit is crossed (at most one warning per rule every 15 min)",
        ["notify-send não existe nesta instalação (o Flatpak não o traz) — os alertas continuam na interface"] =
            "notify-send does not exist in this installation (the Flatpak does not ship it) — alerts still show in the window",
        ["Alertas"] = "Alerts",
        ["sem alertas — temperaturas, ventoinhas e espaço dentro dos limites"] =
            "no alerts — temperatures, fans and free space within limits",
        ["potência"] = "power",
        ["frequência média"] = "average frequency",
        ["{0} núcleos · {1} threads"] = "{0} cores · {1} threads",
        ["   |   mín {0} · média {1}"] = "   |   min {0} · average {1}",
        [" · pico {0} · turbo {1}"] = " · peak {0} · turbo {1}",
        ["{0} % do máximo ({1} de {2} MHz) · mínimo {3} MHz · carga {4} %"] =
            "{0} % of maximum ({1} of {2} MHz) · minimum {3} MHz · load {4} %",
        ["contador de firmware {0}"] = "firmware counter {0}",
        ["sem leitura de potência — não é possível estimar o custo ({0})"] =
            "no power reading — the cost cannot be estimated ({0})",
        ["nesta sessão: {0} Wh em {1} · média {2} W"] = "this session: {0} Wh in {1} · average {2} W",
        ["projecção a este ritmo: {0} €/dia · {1} €/mês"] = "projected at this rate: {0} €/day · {1} €/month",
        ["sem sensor nesta máquina: {0} — a aplicação não inventa valores."] =
            "no sensor on this machine: {0} — the application does not invent values.",
        ["a gravar mas o CSV não pôde ser escrito — amostras só em memória ({0})"] =
            "recording but the CSV could not be written — samples kept in memory only ({0})",
        ["a gravar · 0 amostras · {0}"] = "recording · 0 samples · {0}",
        ["{0} amostras em {1} s · {2}"] = "{0} samples in {1} s · {2}",
        ["CPU {0} · GPU {1}"] = "CPU {0} · GPU {1}",
        ["potência {0} · frequência {1}"] = "power {0} · frequency {1}",
        ["{0} {1} (min {2}, max {3})"] = "{0} {1} (min {2}, max {3})",
        ["a gravar · {0} amostras · {1}"] = "recording · {0} samples · {1}",
        ["a gravar (falha ao escrever o CSV) · {0} amostras em memória"] =
            "recording (CSV write failed) · {0} samples in memory",
        ["{0} alerta(s) activo(s)"] = "{0} active alert(s)",
        ["GPU {0} · {1}"] = "GPU {0} · {1}",
        [" (integrada, dentro do pacote)"] = " (integrated, inside the package)",

        // página Discos
        ["Atualizar"] = "Refresh",
        ["a ler discos…"] = "reading disks…",
        ["Falha a ler discos: "] = "Failed to read disks: ",
        ["Nenhum disco detetado."] = "No disks detected.",
        ["Atenção"] = "Warning",
        ["sem S.M.A.R.T."] = "no S.M.A.R.T.",
        ["Horas ligado"] = "Power-on hours",
        ["Ciclos de energia"] = "Power cycles",
        ["Desgaste"] = "Wear",
        ["Setores realocados"] = "Reallocated sectors",
        ["Erros I/O"] = "I/O errors",
        ["não montada"] = "not mounted",
        ["A saúde S.M.A.R.T. vem do smartctl (por sudo NOPASSWD em /etc/sudoers.d/smartctl). " +
         "Se aparecer \"sem S.M.A.R.T.\", é porque falta essa permissão de root — o cartão do disco " +
         "mostra o comando exato para a conceder. O auto-teste é executado pelo próprio controlador: " +
         "curto demora ~2 min num NVMe, longo varre a superfície toda (pode demorar horas) e o disco " +
         "continua utilizável durante o teste."] =
            "S.M.A.R.T. health comes from smartctl (via sudo NOPASSWD in /etc/sudoers.d/smartctl). " +
            "If it says \"no S.M.A.R.T.\", that root permission is missing — the disk card shows the " +
            "exact command to grant it. The self-test runs in the drive controller itself: the short " +
            "one takes ~2 min on an NVMe, the long one scans the whole surface (it can take hours) and the " +
            "disk stays usable while it runs.",
        ["a ler…"] = "reading…",
        ["Teste curto"] = "Short test",
        ["Teste longo"] = "Long test",
        ["Abortar"] = "Abort",
        ["AUTO-TESTE S.M.A.R.T."] = "S.M.A.R.T. SELF-TEST",
        ["a abortar o auto-teste…"] = "aborting the self-test…",
        ["falha a ler o auto-teste: "] = "failed to read the self-test: ",
        ["indisponível"] = "unavailable",
        ["nunca corrido"] = "never run",
        ["curto"] = "short",
        ["longo"] = "long",
        ["/dev/{0} · {1} · {2} · estado {3}"] = "/dev/{0} · {1} · {2} · state {3}",
        ["{0} h  ({1} dias)"] = "{0} h  ({1} days)",
        ["{0} livres de {1}"] = "{0} free of {1}",
        ["a pedir o auto-teste {0}…"] = "requesting the {0} self-test…",
        ["a correr · {0} % · {1}"] = "running · {0} % · {1}",
        ["a correr · {0} (o controlador não publica a percentagem)"] =
            "running · {0} (the controller does not publish a percentage)",
        ["último resultado: {0}"] = "last result: {0}",

        // página Rede
        ["pronto para testar"] = "ready to test",
        ["Teste de ~15 s: latência TCP até ao router e à internet, jitter, perda de pacotes, " +
         "tempo de DNS e velocidade de download/upload (servidores da Cloudflare)."] =
            "~15 s test: TCP latency to the router and the internet, jitter, packet loss, " +
            "DNS time and download/upload speed (Cloudflare servers).",
        ["Testar rede"] = "Test network",
        ["Diagnóstico de rede"] = "Network diagnostics",
        ["Latência, jitter, perda, velocidade e débito real"] =
            "Latency, jitter, loss, speed and real throughput",
        ["Cliente (medir até outro PC)"] = "Client (measure to another PC)",
        ["Servidor (este PC a servir)"] = "Server (this PC serving)",
        ["Iniciar"] = "Start",
        ["Parar"] = "Stop",
        ["Modo"] = "Mode",
        ["PC servidor"] = "Server PC",
        ["Porta"] = "Port",
        ["Tamanho"] = "Size",
        ["Mede o débito entre dois PC com a própria aplicação, sem servidores de terceiros: um lado " +
         "corre “Servidor” e o outro “Cliente” (ou --cli netbench). O cliente envia N MB e recebe os " +
         "mesmos N MB de volta; a latência vem de 20 idas e voltas de 8 bytes. Os dois sentidos são " +
         "cronometrados e contados em bytes — o que aparece é o que passou."] =
            "Measures throughput between two PCs with the application itself, no third-party servers: one " +
            "side runs “Server” and the other “Client” (or --cli netbench). The client sends N MB and gets the " +
            "same N MB back; latency comes from 20 round trips of 8 bytes. Both directions are " +
            "timed and counted in bytes — what you see is what went through.",
        ["Débito real — cliente/servidor"] = "Real throughput — client/server",
        ["a escutar na porta {0} — inicia o cliente no outro PC…"] =
            "listening on port {0} — start the client on the other PC…",
        ["servidor falhou: "] = "server failed: ",
        ["parado pelo utilizador"] = "stopped by the user",
        ["falhou: "] = "failed: ",
        ["Subida"] = "Upload",
        ["Descida"] = "Download",
        ["Latência"] = "Latency",
        ["a parar…"] = "stopping…",
        ["a testar…"] = "testing…",
        ["Estável"] = "Stable",
        ["Rede local"] = "Local network",
        ["Instável"] = "Unstable",
        ["Latência alta"] = "High latency",
        ["sem dados"] = "no data",
        ["{0} · {1} em cada sentido · {2} s"] = "{0} · {1} each way · {2} s",
        ["{0} ms · jitter {1} ms"] = "{0} ms · jitter {1} ms",
        ["min {0} · max {1} ms · jitter {2} ms · perda {3}%"] =
            "min {0} · max {1} ms · jitter {2} ms · loss {3}%",
        ["medição concluída às {0}"] = "measurement finished at {0}",

        // página Dispositivos
        ["Mostrar as pontes"] = "Show bridges",
        ["As pontes mostram o link que estão a servir, não um máximo comparável — ficam escondidas para a tabela respirar"] =
            "Bridges show the link they are serving, not a comparable maximum — they stay hidden so the table can breathe",
        ["PCI Express — link em uso vs máximo anunciado"] = "PCI Express — link in use vs advertised maximum",
        ["USB — o que está ligado e a que velocidade"] = "USB — what is connected and at what speed",
        ["Áudio — placas, fluxos e xruns do PipeWire"] = "Audio — cards, streams and PipeWire xruns",
        ["Sem pontes"] = "No bridges",
        ["Nenhum link PCI Express legível em /sys/bus/pci/devices."] =
            "No readable PCI Express link in /sys/bus/pci/devices.",
        ["Todos os dispositivos finais negociaram o máximo que anunciam."] =
            "Every endpoint negotiated the maximum it advertises.",
        ["pontes (mostram o link que estão a servir)"] = "bridges (show the link they are serving)",
        ["A largura e a velocidade são as negociadas agora (LnkSta do kernel). O máximo é o que o " +
         "próprio dispositivo anuncia; nas pontes o valor é o link que estão a servir, por isso não " +
         "é comparável e ficam escondidas até as pedires."] =
            "Width and speed are what was negotiated now (the kernel's LnkSta). The maximum is what the " +
            "device itself advertises; on bridges the value is the link they are serving, so it is not " +
            "comparable and they stay hidden until you ask for them.",
        ["Nenhum dispositivo USB legível em /sys/bus/usb/devices."] =
            "No readable USB device in /sys/bus/usb/devices.",
        ["A velocidade é a negociada com o kernel, não um teste de débito."] =
            "The speed is what was negotiated with the kernel, not a throughput test.",
        ["driver "] = "driver ",
        ["pede "] = "asks for ",
        ["série "] = "serial ",
        ["Nenhum fluxo PCM aberto agora — todos os dispositivos estão \"closed\" em /proc/asound."] =
            "No PCM stream open right now — every device is \"closed\" in /proc/asound.",
        ["fluxos activos"] = "active streams",
        ["xruns por nó do PipeWire"] = "xruns per PipeWire node",
        ["Xruns = coluna ERR do pw-top: contador acumulado no servidor desde que cada nó arrancou. " +
         "Reiniciar o PipeWire ou a aplicação cliente volta a zero."] =
            "Xruns = the ERR column of pw-top: a counter accumulated in the server since each node started. " +
            "Restarting PipeWire or the client application resets it to zero.",
        ["Xruns não medidos: o PipeWire não respondeu ao pw-top."] =
            "Xruns not measured: PipeWire did not answer pw-top.",
        ["Quantum de {0} ms — é o período com que o servidor acorda. Buffers maiores toleram mais jitter; "] =
            "Quantum {0} ms — the period the server wakes up with. Larger buffers tolerate more jitter; ",
        ["buffers menores dão menos latência."] = "smaller buffers give lower latency.",
        ["Quantum não lido: sem nó activo o pw-top não mostra o período e o pw-metadata não respondeu."] =
            "Quantum not read: with no active node pw-top shows no period and pw-metadata did not answer.",
        ["anuncia {0} — o link em uso é mais estreito/lento "] =
            "advertises {0} — the link in use is narrower/slower ",
        ["(slot, riser ou poupança de energia do driver)"] = "(slot, riser or driver power saving)",
        ["Mostrar as pontes ({0})"] = "Show bridges ({0})",
        ["{0} dispositivo(s) final(is) e {1} ponte(s). "] = "{0} endpoint(s) and {1} bridge(s). ",
        ["{0} dispositivo(s) abaixo do máximo anunciado (assinalados)."] =
            "{0} device(s) below the advertised maximum (marked).",
        ["{0} dispositivo(s) ligado(s) e {1} concentrador(es)/controlador(es). "] =
            "{0} device(s) connected and {1} hub(s)/controller(s). ",
        ["bus {0}, dispositivo {1}"] = "bus {0}, device {1}",
        ["{0} · {1} ({2}) — {3}"] = "{0} · {1} ({2}) — {3}",
        [", {0} Hz"] = ", {0} Hz",
        [", {0} canais"] = ", {0} channels",
        [" · período {0} ms"] = " · period {0} ms",
        [" · buffer {0} ms"] = " · buffer {0} ms",
        ["≈ {0} GB/s"] = "≈ {0} GB/s",

        // página Relatório
        ["Gera um relatório com tudo o que a aplicação mediu: sistema, CPU, memória, GPUs, discos " +
         "(S.M.A.R.T. e auto-teste), sensores, ranking de benchmarks, o último stress/ciclos/VRAM e a " +
         "última sessão gravada. O HTML é autónomo — CSS e gráficos SVG embutidos, sem rede — e abre em " +
         "qualquer browser. O JSON serve para automação."] =
            "Builds a report with everything the application measured: system, CPU, memory, GPUs, disks " +
            "(S.M.A.R.T. and self-test), sensors, benchmark ranking, the last stress/cycles/VRAM run and the " +
            "last recorded session. The HTML is self-contained — embedded CSS and SVG charts, no network — and " +
            "opens in any browser. The JSON is for automation.",
        ["Abrir"] = "Open",
        ["Gerar HTML completo"] = "Build full HTML",
        ["Gerar JSON"] = "Build JSON",
        ["Abrir pasta"] = "Open folder",
        ["Exportar"] = "Export",
        ["O que vai no relatório"] = "What the report contains",
        ["Exportar a informação do sistema e o histórico de medições"] =
            "Export the system information and the measurement history",
        ["distribuição, kernel, arquitetura, uptime, placa-mãe, BIOS"] =
            "distribution, kernel, architecture, uptime, motherboard, BIOS",
        ["modelo, núcleos, threads, frequências, cache L3"] = "model, cores, threads, frequencies, L3 cache",
        ["total, disponível, swap"] = "total, available, swap",
        ["modelo, barramento PCI, driver, VRAM, temperatura"] = "model, PCI bus, driver, VRAM, temperature",
        ["modelo, tamanho, partições, ocupação, S.M.A.R.T. e auto-teste"] =
            "model, size, partitions, usage, S.M.A.R.T. and self-test",
        ["todas as leituras de hwmon"] = "every hwmon reading",
        ["ranking local com mediana, melhor, última e variação, mais as últimas 15 corridas"] =
            "local ranking with median, best, last and spread, plus the last 15 runs",
        ["último CPU/RAM/GPU/disco, série de ciclos agregada e enchimento de VRAM"] =
            "last CPU/RAM/GPU/disk, aggregated cycle series and VRAM fill",
        ["resumo da última sessão gravada, com gráficos de CPU, GPU, potência e frequência"] =
            "summary of the last recorded session, with CPU, GPU, power and frequency charts",
        ["Documentos"] = "Documents",
        ["a recolher dados e histórico…"] = "collecting data and history…",
        ["gravado ({0} KB)"] = "saved ({0} KB)",
        [" — relatório completo"] = " — full report",
        ["não foi possível abrir: "] = "could not open: ",

        // cartão de FPS (MangoHud)
        ["Procurar sessões"] = "Find sessions",
        ["Ver a escolhida"] = "View the selected one",
        ["FPS médio"] = "Average FPS",
        ["frametime médio"] = "Average frametime",
        ["Lê as sessões que o MangoHud grava em CSV (layer Vulkan/OpenGL). Em Wayland é a única " +
         "forma honesta de ter FPS por aplicação — o compositor não expõe frames de outros processos."] =
            "Reads the sessions MangoHud writes as CSV (Vulkan/OpenGL layer). On Wayland it is the only " +
            "honest way to get per-application FPS — the compositor does not expose other processes' frames.",
        ["FPS · sessão do MangoHud"] = "FPS · MangoHud session",
        ["Sem sessões do MangoHud. Para gravar, corre o jogo/programa assim:  "] =
            "No MangoHud sessions. To record, run the game/program like this:  ",
        ["Sem sessões do MangoHud. Corre o jogo com MANGOHUD=1 (Steam: nas opções de " +
         "lançamento) e a pasta ~/mangologs recebe um CSV por sessão; o comando " +
         "`LinuxExplode --cli fps` lê o mesmo formato."] =
            "No MangoHud sessions. Run the game with MANGOHUD=1 (Steam: in the launch " +
            "options) and ~/mangologs gets one CSV per session; `LinuxExplode --cli fps` reads the same format.",
        ["Esse ficheiro não tem amostras válidas: "] = "That file has no valid samples: ",
        ["{0} s · {1} amostras"] = "{0} s · {1} samples",

        // pop do benchmark
        ["1280 × 720 (padrão)"] = "1280 × 720 (default)",
        ["Benchmark — CPU a renderizar"] = "Benchmark — CPU rendering",
        ["Benchmark — GPU a renderizar"] = "Benchmark — GPU rendering",
        ["Benchmark — ray tracing a renderizar"] = "Benchmark — ray tracing rendering",
        ["CPU · path tracer single-core a animar"] = "CPU · single-core path tracer animating",
        ["CPU · path tracer a animar · {0} threads"] = "CPU · path tracer animating · {0} threads",
        ["GPU · compute Vulkan a animar"] = "GPU · Vulkan compute animating",
        ["GPU · ray tracing (VK_KHR_ray_query) a animar"] = "GPU · ray tracing (VK_KHR_ray_query) animating",
        ["Fechar"] = "Close",
        ["CORES EM USO"] = "CORES IN USE",
        ["NOTA · AMOSTRAS/S ÷ 10.000"] = "SCORE · SAMPLES/S ÷ 10,000",
        ["a preparar o dispositivo Vulkan e a medir…"] = "preparing the Vulkan device and measuring…",
        ["falhou: não foi possível criar o dispositivo Vulkan (verifica o driver vulkan-radeon)"] =
            "failed: could not create the Vulkan device (check the vulkan-radeon driver)",
        ["indisponível: esta GPU não expõe VK_KHR_ray_query"] =
            "unavailable: this GPU does not expose VK_KHR_ray_query",
        ["a renderizar numa só thread…"] = "rendering on a single thread…",
        ["a renderizar com {0} threads…"] = "rendering with {0} threads…",
        ["{0} fotogramas/s"] = "{0} frames/s",
        ["a contar fotogramas…"] = "counting frames…",
        ["{0}×{1} · {2} amostras/pixel · {3} · câmara em órbita"] =
            "{0}×{1} · {2} samples/pixel · {3} · camera orbiting",
        ["corrida {0} · {1} thread(s) · {2} s · {3} amostras"] =
            "run {0} · {1} thread(s) · {2} s · {3} samples",
        [" — concluída; a recomeçar em {0} s"] = " — finished; restarting in {0} s",

        // página Benchmark
        ["sem resultados"] = "no results",
        ["procurar tipo, alvo ou detalhe…"] = "search type, target or detail…",
        ["sem medições"] = "no measurements",
        ["Path tracing de CPU e GPU · banda de memória · disco"] =
            "CPU and GPU path tracing · memory bandwidth · disk",
        ["nenhuma GPU Vulkan com fila de computação — verifica o driver (vulkan-radeon / mesa)"] =
            "no Vulkan GPU with a compute queue — check the driver (vulkan-radeon / mesa)",
        ["dedicada"] = "discrete",
        ["integrada"] = "integrated",
        ["virtual"] = "virtual",
        ["{0} GPU(s) Vulkan detetada(s)"] = "{0} Vulkan GPU(s) detected",
        ["O mesmo path tracer da CPU, agora em compute shader Vulkan: "] =
            "The same CPU path tracer, now as a Vulkan compute shader: ",
        ["{0} amostras por pixel, repetido até {1} s para a nota "] =
            "{0} samples per pixel, repeated until {1} s so the score ",
        ["ser estável — a resolução escolhe-se no pop-up, de 320×180 até 4K. "] =
            "stays stable — the resolution is chosen in the pop-up, from 320×180 up to 4K. ",
        ["O modo ray tracing usa VK_KHR_ray_query com 64 esferas em BVH — a nota não é comparável com a de compute."] =
            "Ray tracing mode uses VK_KHR_ray_query with 64 spheres in a BVH — its score is not comparable with compute.",
        ["Testar GPU"] = "Test GPU",
        ["Testar ray tracing"] = "Test ray tracing",
        ["NOTA GPU"] = "GPU SCORE",
        ["{0} pts · {1}T · {2}"] = "{0} pts · {1}T · {2}",
        ["primeira corrida guardada"] = "first run saved",
        ["ray tracing · "] = "ray tracing · ",
        ["compute · "] = "compute · ",
        ["primeira corrida deste alvo"] = "first run of this target",
        ["Multi-core"] = "Multi-core",
        ["Single-core"] = "Single-core",
        ["O mesmo renderizador do benchmark da versão Windows: path tracer determinístico, 960×540, 1024 amostras por pixel. "] =
            "The same renderer as the Windows benchmark: deterministic path tracer, 960×540, 1024 samples per pixel. ",
        ["A nota é amostras por segundo ÷ 10.000 — comparável entre máquinas."] =
            "The score is samples per second ÷ 10,000 — comparable across machines.",
        ["HISTÓRICO"] = "HISTORY",
        ["Banda de escrita, leitura, cópia e latência real de acesso à RAM."] =
            "Write, read and copy bandwidth plus real RAM access latency.",
        ["Testar memória"] = "Test memory",
        ["Escrita sequencial"] = "Sequential write",
        ["Leitura sequencial"] = "Sequential read",
        ["Cópia"] = "Copy",
        ["bloco de {0}"] = "{0} block",
        ["escrita {0} MB/s · leitura {1} MB/s · "] = "write {0} MB/s · read {1} MB/s · ",
        ["cópia {0} MB/s · latência {1} ns"] = "copy {0} MB/s · latency {1} ns",
        ["primeira corrida"] = "first run",
        ["Memória RAM"] = "RAM",
        ["Pointer chasing sobre blocos de 64 B (uma linha de cache), de 4 KB a 256 MB: nanossegundos por "] =
            "Pointer chasing over 64 B blocks (one cache line), from 4 KB to 256 MB: nanoseconds per ",
        ["acesso. Cada patamar é um nível — L1, L2, L3 — e o degrau final é a RAM. "] =
            "access. Each plateau is a level — L1, L2, L3 — and the last step is RAM. ",
        ["O percurso é uma permutação aleatória com semente fixa, por isso duas corridas visitam os mesmos endereços."] =
            "The walk is a random permutation with a fixed seed, so two runs visit the same addresses.",
        ["Medir latência"] = "Measure latency",
        ["Memória · latência por tamanho de bloco"] = "Memory · latency by block size",
        ["a medir 4 KB → 256 MB (alguns segundos)…"] = "measuring 4 KB → 256 MB (a few seconds)…",
        ["{0} pontos medidos"] = "{0} points measured",
        ["falhou: {0}"] = "failed: {0}",
        ["Duas tarefas fixas e repetíveis: descomprimir {0} MB "] =
            "Two fixed, repeatable tasks: decompress {0} MB ",
        ["(o resultado é conferido por SHA-256) e compilar um ficheiro C com "] =
            "(the result is checked with SHA-256) and compile a C file with ",
        ["{0} funções, com o compilador do sistema. "] = "{0} functions using the system compiler. ",
        ["Não usa a rede nem o disco além do próprio trabalho — é o que um utilizador faz a compilar ou a instalar."] =
            "No network and no disk beyond the work itself — it is what a user does when compiling or installing.",
        ["Correr tarefa real"] = "Run real-world task",
        ["Tarefa do mundo real · descomprimir + compilar"] = "Real-world task · decompress + compile",
        ["a correr (descomprimir + compilar)…"] = "running (decompress + compile)…",
        ["descompressão   {0} MB/s"] = "decompression   {0} MB/s",
        ["compilação      {0} kB/s   ({1} s, {2})"] = "compilation     {0} kB/s   ({1} s, {2})",
        ["compilação      —   {0}"] = "compilation     —   {0}",
        ["total           {0} s"] = "total           {0} s",
        ["descompressão verificada (SHA-256 {0}…); a compilação não correu"] =
            "decompression verified (SHA-256 {0}…); the compilation did not run",
        ["concluído · SHA-256 {0}… igual ao original"] = "done · SHA-256 {0}… matches the original",
        ["gzip {0} MB"] = "gzip {0} MB",
        ["compilar {0} funções"] = "compile {0} functions",
        ["kB/s de fonte"] = "kB/s of source",
        ["Mediana das últimas {0} corridas de cada alvo, com a variação entre elas. "] =
            "Median of the last {0} runs of each target, with the spread between them. ",
        ["Acima de {0} % de variação a máquina não estava estável e o número vale pouco — "] =
            "Above {0} % spread the machine was not stable and the number is worth little — ",
        ["o aviso aparece na linha. Para a mediana ser de 3, corre `--cli bench multi --repeticoes 3`."] =
            "the warning shows on the row. For a median of 3, run `--cli bench multi --repeticoes 3`.",
        ["Actualizar"] = "Refresh",
        ["Ranking local · mediana de 3 corridas"] = "Local ranking · median of 3 runs",
        ["sem corridas guardadas — corre um teste para começar o ranking"] =
            "no runs saved — run a test to start the ranking",
        ["{0} alvo(s) no histórico"] = "{0} target(s) in the history",
        ["Escrita e leitura sequencial + 4K aleatório num ficheiro temporário de 256 MB, "] =
            "Sequential write and read plus random 4K on a temporary 256 MB file, ",
        ["com O_DIRECT (sem passar pelo cache do sistema). O ficheiro é apagado no fim."] =
            "with O_DIRECT (bypassing the system cache). The file is deleted at the end.",
        ["Testar disco"] = "Test disk",
        ["Sequencial · escrita"] = "Sequential · write",
        ["Sequencial · leitura"] = "Sequential · read",
        ["4K aleatório · leitura"] = "Random 4K · read",
        ["4K aleatório · escrita"] = "Random 4K · write",
        ["ficheiro de {0} em {1}"] = "{0} file in {1}",
        ["seq escrita {0} · seq leitura {1} · "] = "seq write {0} · seq read {1} · ",
        ["4K leitura {0} · 4K escrita {1} MB/s"] = "4K read {0} · 4K write {1} MB/s",
        ["sequencial (escrita)"] = "sequential (write)",
        ["Exportar CSV"] = "Export CSV",
        ["Exportar JSON"] = "Export JSON",
        ["Importar…"] = "Import…",
        ["As notas ficam guardadas em SQLite (~/.local/share/linux-explode/historico.db), com a data e a "] =
            "Scores are stored in SQLite (~/.local/share/linux-explode/historico.db), with the date and ",
        ["versão. Cada cartão mostra o melhor resultado, a mediana e a variação da última corrida. "] =
            "version. Each card shows the best result, the median and the spread of the last run. ",
        ["Pesquisa por tipo, alvo ou detalhe e filtra por período; exporta para CSV/JSON para comparar "] =
            "Search by type, target or detail and filter by period; export to CSV/JSON to compare ",
        ["máquinas, ou importa o ficheiro de outra máquina. O resultados.json antigo é migrado sozinho "] =
            "machines, or import another machine's file. The old resultados.json is migrated automatically ",
        ["no primeiro arranque e guardado como resultados.json.migrado."] =
            "on first start and kept as resultados.json.migrado.",
        ["7 dias"] = "7 days",
        ["30 dias"] = "30 days",
        ["90 dias"] = "90 days",
        ["PROCURAR"] = "SEARCH",
        ["PERÍODO"] = "PERIOD",
        ["Histórico de resultados"] = "Results history",
        ["{0} de {1} resultado(s) no histórico"] = "{0} of {1} result(s) in the history",
        [" · desde {0}"] = " · since {0}",
        ["ainda sem resultados guardados — corre um teste"] = "no results saved yet — run a test",
        ["nada encontrado com esse filtro"] = "nothing found with that filter",
        ["últimas {0} corridas · {1} · {2} · "] = "last {0} runs · {1} · {2} · ",
        ["mín {0} · máx {1}"] = "min {0} · max {1}",
        ["gravado em {0}"] = "saved to {0}",
        ["não consegui exportar: "] = "could not export: ",
        ["Importar resultados (JSON)"] = "Import results (JSON)",
        ["Resultados JSON"] = "JSON results",
        ["{0} resultado(s) importado(s) de {1}"] = "{0} result(s) imported from {1}",
        ["o ficheiro não tinha resultados novos"] = "the file had no new results",
        ["não consegui importar: "] = "could not import: ",

        // página Stress
        ["pronto para começar"] = "ready to start",
        ["sem série registada"] = "no recorded series",
        ["sem teste de VRAM"] = "no VRAM test",
        ["Guardar relatório"] = "Save report",
        ["Aquece CPU, RAM, GPU e disco — com guarda térmica e valores medidos"] =
            "Heats up CPU, RAM, GPU and disk — with thermal guard and measured values",
        ["pronto · limites de fábrica: CPU/GPU 95 °C"] = "ready · factory limits: CPU/GPU 95 °C",
        ["nenhuma GPU Vulkan — falta o driver"] = "no Vulkan GPU — driver missing",
        ["falha a listar GPUs: "] = "failed to list GPUs: ",
        ["1 minuto"] = "1 minute",
        ["2 minutos"] = "2 minutes",
        ["5 minutos"] = "5 minutes",
        ["10 minutos"] = "10 minutes",
        ["20 minutos"] = "20 minutes",
        ["30 minutos"] = "30 minutes",
        ["FMA vetorial (SIMD)"] = "Vector FMA (SIMD)",
        ["vírgula flutuante"] = "floating point",
        ["memória"] = "memory",
        ["25 % da RAM"] = "25 % of RAM",
        ["50 % da RAM"] = "50 % of RAM",
        ["75 % da RAM"] = "75 % of RAM",
        ["Duração"] = "Duration",
        ["Threads de CPU"] = "CPU threads",
        ["Carga de CPU"] = "CPU load",
        ["Memória a encher"] = "Memory to fill",
        ["Guarda térmica"] = "Thermal guard",
        ["Disco por passagem"] = "Disk per pass",
        ["A guarda térmica lê os sensores reais (k10temp e amdgpu) quatro vezes por segundo e para tudo " +
         "assim que um limite é passado. O ficheiro de disco é apagado no fim."] =
            "The thermal guard reads the real sensors (k10temp and amdgpu) four times a second and stops everything " +
            "as soon as a limit is crossed. The disk file is deleted at the end.",
        ["Configuração"] = "Configuration",
        ["Cada teste corre durante a duração escolhida. CPU faz passagens contínuas de carga, a RAM " +
         "enche, relê e verifica cada palavra, o disco escreve e relê a verificar, e a GPU dispara o " +
         "path tracer em ciclo. “Tudo” corre os quatro em paralelo — é o teste de estabilidade a sério."] =
            "Each test runs for the chosen duration. The CPU does continuous load passes, RAM " +
            "fills, re-reads and verifies every word, the disk writes and re-reads verifying, and the GPU fires the " +
            "path tracer in a loop. “Everything” runs all four in parallel — that is the real stability test.",
        ["Testes"] = "Tests",
        ["tudo (burn-in)"] = "everything (burn-in)",
        ["3 ciclos"] = "3 cycles",
        ["5 ciclos"] = "5 cycles",
        ["10 ciclos"] = "10 cycles",
        ["20 ciclos"] = "20 cycles",
        ["Iniciar série"] = "Start series",
        ["Parar série"] = "Stop series",
        ["Alvo"] = "Target",
        ["Duração do ciclo"] = "Cycle duration",
        ["Número de ciclos"] = "Number of cycles",
        ["Pausa entre ciclos"] = "Pause between cycles",
        ["A série repete o mesmo teste N vezes, com pausa entre ciclos, e dá a mediana de cada métrica " +
         "com o mínimo e o máximo. Um teste longo não mostra se a máquina repete o resultado; N ciclos " +
         "iguais mostram — e se a variação passar os 3 % o cartão avisa. A guarda térmica continua " +
         "activa e um ciclo que a dispare termina a série."] =
            "The series repeats the same test N times, with a pause between cycles, and gives the median of each " +
            "metric with the minimum and maximum. One long test does not show whether the machine repeats the " +
            "result; N equal cycles do — and if the spread goes past 3 % the card warns. The thermal guard stays " +
            "active and a cycle that triggers it ends the series.",
        ["Ciclos agendados"] = "Scheduled cycles",
        ["a série de ciclos já está a correr"] = "the cycle series is already running",
        ["há um teste simples a correr — usa Parar antes de arrancar a série"] =
            "a simple test is running — use Stop before starting the series",
        ["o teste de VRAM está a correr — pára-o antes de arrancar a série"] =
            "the VRAM test is running — stop it before starting the series",
        ["a correr: {0} · {1} ciclo(s) de {2} s"] = "running: {0} · {1} cycle(s) of {2} s",
        ["a arrancar…"] = "starting…",
        ["série · {0} · {1} × {2} s · pausa {3} s"] = "series · {0} · {1} × {2} s · pause {3} s",
        ["a série não arrancou: "] = "the series did not start: ",
        ["pedido de paragem da série de ciclos"] = "cycle series stop requested",
        ["{0} s · {1}"] = "{0} s · {1}",
        ["{0} s · {1} · {2}"] = "{0} s · {1} · {2}",
        ["a correr · "] = "running · ",
        ["{0} · {1} s · energia {2} Wh · "] = "{0} · {1} s · energy {2} Wh · ",
        ["picos CPU {0} °C / GPU {1} °C"] = "peaks CPU {0} °C / GPU {1} °C",
        ["métrica"] = "metric",
        ["mínimo"] = "minimum",
        ["máximo"] = "maximum",
        ["variação"] = "spread",
        ["total"] = "total",
        ["em {0}"] = "in {0}",
        ["±{0} %"] = "±{0} %",
        ["amostra pequena ({0})"] = "small sample ({0})",
        ["ciclo {0}: {1} em {2} s · "] = "cycle {0}: {1} in {2} s · ",
        ["completo"] = "complete",
        ["parado"] = "stopped",
        ["série parada"] = "series stopped",
        ["série terminada"] = "series finished",
        ["fim da série · {0} · pico CPU {1} °C / GPU {2} °C"] =
            "end of series · {0} · peak CPU {1} °C / GPU {2} °C",
        ["até o driver recusar"] = "until the driver refuses",
        ["a listar GPUs…"] = "listing GPUs…",
        ["Encher VRAM"] = "Fill VRAM",
        ["Limite"] = "Limit",
        ["Bloco"] = "Block",
        ["Aloca blocos de VRAM em Vulkan, escreve um padrão em cada um e relê duas amostras. O total é o " +
         "que o driver aceitou; o pico e o uso vêm de /sys/class/drm (fonte independente da aplicação). " +
         "Encher até o driver recusar pode deixar o ambiente gráfico sem memória — nestas placas o limite " +
         "seguro é 2 GB com o desktop a correr."] =
            "Allocates VRAM blocks in Vulkan, writes a pattern into each and re-reads two samples. The total is what " +
            "the driver accepted; the peak and the usage come from /sys/class/drm (a source independent of the app). " +
            "Filling until the driver refuses can leave the desktop without memory — on these cards the safe " +
            "limit is 2 GB with the desktop running.",
        ["Memória da GPU (Vulkan)"] = "GPU memory (Vulkan)",
        ["o teste de VRAM já está a correr"] = "the VRAM test is already running",
        ["sem GPU Vulkan — não há VRAM para encher"] = "no Vulkan GPU — there is no VRAM to fill",
        ["há outro teste a correr — usa Parar antes de encher a VRAM"] =
            "another test is running — use Stop before filling VRAM",
        ["a encher até o driver recusar, blocos de {0} MB"] =
            "filling until the driver refuses, {0} MB blocks",
        ["a encher até {0} MB, blocos de {1} MB"] = "filling up to {0} MB, {1} MB blocks",
        ["a alocar blocos…"] = "allocating blocks…",
        ["VRAM · GPU {0} · limite {1} · bloco {2} MB"] = "VRAM · GPU {0} · limit {1} · {2} MB blocks",
        ["até falhar"] = "until it fails",
        ["{0} MB"] = "{0} MB",
        ["pedido de paragem do teste de VRAM"] = "VRAM test stop requested",
        ["o teste de VRAM falhou: "] = "the VRAM test failed: ",
        ["sem resultado"] = "no result",
        ["terminado com erro"] = "finished with an error",
        ["VRAM falhou: "] = "VRAM failed: ",
        ["não alocou nada — {0}"] = "allocated nothing — {0}",
        ["{0} bloco(s) · {1} em {2} s"] = "{0} block(s) · {1} in {2} s",
        [" · pico em /sys {0}"] = " · peak in /sys {0}",
        ["terminado"] = "finished",
        ["VRAM · {0} · {1} · {2} erro(s) de verificação"] =
            "VRAM · {0} · {1} · {2} verification error(s)",
        ["CPU · k10temp"] = "CPU · k10temp",
        ["GPU · amdgpu"] = "GPU · amdgpu",
        ["potência · RAPL + GPU"] = "power · RAPL + GPU",
        ["Resultado"] = "Result",
        ["Registo"] = "Log",
        ["já há um teste a correr — usa Parar primeiro"] = "a test is already running — use Stop first",
        ["a série de ciclos está a correr — pára-a antes de arrancar um teste simples"] =
            "the cycle series is running — stop it before starting a simple test",
        ["o teste de VRAM está a correr — pára-o antes de arrancar outro teste"] =
            "the VRAM test is running — stop it before starting another test",
        ["a correr: {0}"] = "running: {0}",
        [" · {0} thread(s) · carga {1}"] = " · {0} thread(s) · load {1}",
        ["início · {0} · {1}{2} · limite {3} °C"] = "start · {0} · {1}{2} · limit {3} °C",
        ["não arrancou: "] = "did not start: ",
        ["{0} · {1}"] = "{0} · {1}",
        ["concluído"] = "done",
        ["{0} — {1}"] = "{0} — {1}",
        ["· picos: CPU {0} °C · GPU {1} °C · energia {2} Wh "] =
            "· peaks: CPU {0} °C · GPU {1} °C · energy {2} Wh ",
        ["em {0} s"] = "in {0} s",
        ["fim · {0} · {1} · pico CPU {2} °C / GPU {3} °C"] =
            "end · {0} · {1} · peak CPU {2} °C / GPU {3} °C",
        ["terminado — podes repetir"] = "finished — you can repeat",
        ["relatório gravado em "] = "report saved to ",
        ["não consegui gravar o relatório: "] = "could not save the report: ",
        ["página fechada — teste parado"] = "page closed — test stopped",
        ["página fechada — série de ciclos parada"] = "page closed — cycle series stopped",
        ["página fechada — teste de VRAM parado"] = "page closed — VRAM test stopped",
        ["inteiro"] = "integer",
        ["tudo"] = "all",
        ["disco"] = "disk",
        ["todos ({0})"] = "all ({0})",
        ["Testar CPU"] = "Test CPU",
        ["Testar RAM"] = "Test RAM",
        ["Tudo (burn-in)"] = "Everything (burn-in)",
        ["/sys/bus/pci, /sys/bus/usb e /proc/asound — sem root, sem ferramentas externas"] =
            "/sys/bus/pci, /sys/bus/usb and /proc/asound — no root, no external tools",
        ["/sys/block + smartctl — ocupação, temperatura, S.M.A.R.T. e auto-teste"] =
            "/sys/block + smartctl — usage, temperature, S.M.A.R.T. and self-test",
        ["O teste suspende a máquina a sério e mede o tempo de três formas independentes: relógio de parede " +
         "menos relógio monotónico (que pára no suspend), e o uptime (CLOCK_BOOTTIME). No fim verifica se " +
         "os sensores e a potência voltaram a responder — num portátil é aqui que se vê se o wake deixou " +
         "algo partido. Sem rtcwake no sudoers a máquina só acorda quando lhe tocas; o botão Simular mede " +
         "os relógios sem dormir e prova que a conta dá zero."] =
            "The test suspends the machine for real and measures the time three independent ways: wall clock " +
            "minus monotonic clock (which stops during suspend) and uptime (CLOCK_BOOTTIME). At the end it " +
            "checks whether the sensors and power came back — on a laptop this is where you see if the wake " +
            "left something broken. Without rtcwake in sudoers the machine only wakes when you touch it; the " +
            "Simulate button measures the clocks without sleeping and proves the arithmetic comes out at zero."
    };
}

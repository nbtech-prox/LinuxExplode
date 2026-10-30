namespace LinuxExplode.Core;

/// <summary>Traduções para espanhol (es-ES). A chave é o texto original em pt-PT.</summary>
public static partial class Textos
{
    private static readonly Dictionary<string, string> Es = new(StringComparer.Ordinal)
    {
        // navegação e moldura
        ["Sistema"] = "Sistema",
        ["Sensores"] = "Sensores",
        ["Discos e saúde"] = "Discos y salud",
        ["Benchmark"] = "Benchmark",
        ["Stress"] = "Estrés",
        ["Rede"] = "Red",
        ["Dispositivos"] = "Dispositivos",
        ["Relatório"] = "Informe",
        ["Tema claro"] = "Tema claro",
        ["Tema escuro"] = "Tema oscuro",
        ["Escala da interface (Ctrl+- e Ctrl++)"] = "Escala de la interfaz (Ctrl+- y Ctrl++)",
        ["a ler hardware…"] = "leyendo hardware…",
        ["leitura de hardware indisponível"] = "lectura de hardware no disponible",
        ["hardware · sensores · bench"] = "hardware · sensores · bench",

        // bandeja
        ["Abrir janela"] = "Abrir ventana",
        ["Fechar para a bandeja"] = "Cerrar a la bandeja",
        ["Parar todo o stress"] = "Detener todo el estrés",
        ["Parar todo o stress ({0})"] = "Detener todo el estrés ({0})",
        ["Sair"] = "Salir",
        ["a ler sensores…"] = "leyendo sensores…",
        ["não havia stress a correr"] = "no había estrés en curso",
        ["stress parado ({0} sessão/ões)"] = "estrés detenido ({0} sesión/sesiones)",
        ["stress {0}"] = "estrés {0}",
        ["LINUX EXPLODE · sem leitura de sensores"] = "LINUX EXPLODE · sin lectura de sensores",

        // página Sistema
        ["Informação de hardware lida diretamente de /sys, /proc e DMI"] =
            "Información de hardware leída directamente de /sys, /proc y DMI",
        ["Falha a ler o hardware: "] = "Error al leer el hardware: ",
        ["Sistema operativo"] = "Sistema operativo",
        ["Processador"] = "Procesador",
        ["Memória"] = "Memoria",
        ["Placa-mãe e BIOS"] = "Placa base y BIOS",
        ["Placa de vídeo"] = "Tarjeta gráfica",
        ["Nenhuma GPU detetada em /sys/class/drm."] = "No se detectó ninguna GPU en /sys/class/drm.",
        ["threads"] = "hilos",
        ["{0} núcleos"] = "{0} núcleos",
        ["{0} em uso"] = "{0} en uso",
        ["frequência"] = "frecuencia",
        ["máx {0}"] = "máx. {0}",
        ["ligado há"] = "encendido desde hace",
        ["Distribuição"] = "Distribución",
        ["Arquitetura"] = "Arquitectura",
        ["Máquina"] = "Máquina",
        ["Modelo"] = "Modelo",
        ["Núcleos"] = "Núcleos",
        ["Frequência"] = "Frecuencia",
        ["Máxima"] = "Máxima",
        ["Instalada"] = "Instalada",
        ["Em uso"] = "En uso",
        ["Disponível"] = "Disponible",
        ["sem swap"] = "sin swap",
        ["{0} de {1}"] = "{0} de {1}",
        ["Sem SPD legível sem root: em DDR5 é preciso o módulo spd5118 carregado " +
         "(expõe /sys/bus/nvmem/devices) e em DDR4 o ee1004."] =
            "Sin SPD legible sin root: en DDR5 hace falta el módulo spd5118 cargado " +
            "(expone /sys/bus/nvmem/devices) y en DDR4 el ee1004.",
        ["Placa-mãe"] = "Placa base",
        ["Barramento PCI"] = "Bus PCI",
        ["Driver"] = "Controlador",
        ["Agora"] = "Ahora",
        ["{0}   ·   uso {1}"] = "{0}   ·   uso {1}",
        ["Estados"] = "Estados",
        ["Modo em memória"] = "Modo en memoria",
        ["Suspender"] = "Suspender",
        ["sim (sudo sem password)"] = "sí (sudo sin contraseña)",
        ["não — falta permissão"] = "no — falta permiso",
        ["Acordar sozinho"] = "Despertar solo",
        ["sim (rtcwake)"] = "sí (rtcwake)",
        ["não — acorda-se à mão"] = "no — hay que despertarlo a mano",
        ["DORMIR"] = "DORMIR",
        ["Testar suspensão"] = "Probar suspensión",
        ["Simular (sem dormir)"] = "Simular (sin dormir)",
        ["último teste: {0} · {1}"] = "última prueba: {0} · {1}",
        ["Suspender e retomar"] = "Suspender y reanudar",
        ["simulação: a ler os relógios…"] = "simulación: leyendo los relojes…",
        ["a suspender…"] = "suspendiendo…",
        ["o teste falhou: {0}"] = "la prueba falló: {0}",
        ["relógio de parede {0} s · suspend medido {1} s · uptime confirma {2} s"] =
            "reloj de pared {0} s · suspend medido {1} s · uptime confirma {2} s",
        ["sensores CPU {0} → {1} · GPU {2} → {3} · potência {4} → {5}"] =
            "sensores CPU {0} → {1} · GPU {2} → {3} · potencia {4} → {5}",
        ["sensores e potência voltaram a responder depois do wake"] =
            "los sensores y la potencia volvieron a responder tras el wake",
        ["há leituras que não voltaram depois do wake — o cartão mostra quais"] =
            "hay lecturas que no volvieron tras el wake — la tarjeta muestra cuáles",
        ["pronto"] = "listo",
        ["Módulo {0}"] = "Módulo {0}",
        ["fabrico {0}-W{1}"] = "fabricado {0}-W{1}",

        // página Sensores
        ["Bom"] = "Bueno",
        ["Atencao"] = "Atención",
        ["Mau"] = "Malo",
        ["a medir…"] = "midiendo…",
        ["sem alertas"] = "sin alertas",
        ["sem gravação"] = "sin grabación",
        ["Notificações do sistema"] = "Notificaciones del sistema",
        ["Todos"] = "Todos",
        ["Temperatura"] = "Temperatura",
        ["Ventoinha"] = "Ventilador",
        ["Potência"] = "Potencia",
        ["Tensão"] = "Voltaje",
        ["ao vivo · 1 s"] = "en vivo · 1 s",
        ["Nenhum sensor de frequência em /sys/class/hwmon — o detalhe por núcleo acima vem de cpufreq."] =
            "Ningún sensor de frecuencia en /sys/class/hwmon — el detalle por núcleo de arriba viene de cpufreq.",
        ["Nenhum sensor de potência em /sys/class/hwmon — o consumo acima vem do RAPL (CPU) e dos contadores das GPUs."] =
            "Ningún sensor de potencia en /sys/class/hwmon — el consumo de arriba viene del RAPL (CPU) y de los contadores de las GPUs.",
        ["Nenhum sensor disponível nesta categoria. Em muitos portáteis é preciso carregar o módulo " +
         "do kernel (ex.: sudo modprobe nct6775) ou o CPU expõe apenas k10temp."] =
            "Ningún sensor disponible en esta categoría. En muchos portátiles hay que cargar el módulo " +
            "del kernel (p. ej.: sudo modprobe nct6775) o la CPU solo expone k10temp.",
        ["CPU — socket (RAPL)"] = "CPU — socket (RAPL)",
        ["CPU — núcleos (RAPL)"] = "CPU — núcleos (RAPL)",
        ["Plataforma (psys, RAPL)"] = "Plataforma (psys, RAPL)",
        ["Total medido"] = "Total medido",
        ["Energia desde o arranque"] = "Energía desde el arranque",
        ["Consumo medido — CPU (socket) e GPUs"] = "Consumo medido — CPU (socket) y GPUs",
        ["Isto NÃO é o consumo da tomada: mede o socket da CPU (RAPL, inclui núcleos, I/O e gráfica " +
         "integrada) e as GPUs. Placa-mãe, memória, discos, ventoinhas e as perdas da fonte não têm sensor " +
         "nesta máquina — só uma tomada inteligente/UPS ou uma fonte com telemetria daria o total real."] =
            "Esto NO es el consumo de la toma: mide el socket de la CPU (RAPL, incluye núcleos, E/S y gráfica " +
            "integrada) y las GPUs. Placa base, memoria, discos, ventiladores y las pérdidas de la fuente no " +
            "tienen sensor en esta máquina — solo un enchufe inteligente/SAI o una fuente con telemetría daría el total real.",
        ["sem leitura de frequência de CPU — o kernel não expõe cpufreq " +
         "(comum em ARM sem driver de escala e em máquinas virtuais)."] =
            "sin lectura de frecuencia de CPU — el kernel no expone cpufreq " +
            "(habitual en ARM sin driver de escala y en máquinas virtuales).",
        ["CPU — frequência por núcleo"] = "CPU — frecuencia por núcleo",
        ["Núcleo {0}"] = "Núcleo {0}",
        ["últimos 60 s · uma amostra por segundo · temperaturas em °C, potência em W, frequência em GHz"] =
            "últimos 60 s · una muestra por segundo · temperaturas en °C, potencia en W, frecuencia en GHz",
        ["Ao vivo"] = "En vivo",
        ["Throttling — frequência efectiva vs máxima"] = "Throttling — frecuencia efectiva vs máxima",
        ["tarifa (€/kWh)"] = "tarifa (€/kWh)",
        ["Energia e custo"] = "Energía y coste",
        ["sem sensor"] = "sin sensor",
        ["sem RAPL"] = "sin RAPL",
        ["o firmware não expõe contadores de throttle (normal em AMD)"] =
            "el firmware no expone contadores de throttling (normal en AMD)",
        ["sem leitura de cpufreq"] = "sin lectura de cpufreq",
        ["a média é a do tempo em que a janela esteve aberta (inclui o PC parado)"] =
            "la media es la del tiempo que la ventana estuvo abierta (incluye el PC parado)",
        ["Nenhum sensor de temperatura fora da CPU e da GPU nesta máquina."] =
            "Ningún sensor de temperatura fuera de la CPU y la GPU en esta máquina.",
        ["Placa-mãe, chipset e memória"] = "Placa base, chipset y memoria",
        ["sensores lidos de /sys/class/hwmon; uma leitura ausente fica em branco em vez de zero."] =
            "sensores leídos de /sys/class/hwmon; una lectura ausente queda en blanco en vez de cero.",
        ["Grava CPU, GPU, potência e frequência uma vez por segundo em " +
         "~/.local/share/linux-explode/sessoes. No fim ficam aqui o resumo e o gráfico da sessão inteira."] =
            "Graba CPU, GPU, potencia y frecuencia una vez por segundo en " +
            "~/.local/share/linux-explode/sessoes. Al final quedan aquí el resumen y el gráfico de la sesión completa.",
        ["Gravar sessão"] = "Grabar sesión",
        ["Sessão gravada · monitorização em CSV"] = "Sesión grabada · monitorización en CSV",
        ["Parar e ver"] = "Parar y ver",
        ["nada gravado"] = "nada grabado",
        ["sem leitura"] = "sin lectura",
        ["Avisa com notify-send quando um limite é passado (no máximo um aviso por regra a cada 15 min)"] =
            "Avisa con notify-send cuando se supera un límite (como máximo un aviso por regla cada 15 min)",
        ["notify-send não existe nesta instalação (o Flatpak não o traz) — os alertas continuam na interface"] =
            "notify-send no existe en esta instalación (el Flatpak no lo trae) — las alertas siguen en la interfaz",
        ["Alertas"] = "Alertas",
        ["sem alertas — temperaturas, ventoinhas e espaço dentro dos limites"] =
            "sin alertas — temperaturas, ventiladores y espacio dentro de los límites",
        ["potência"] = "potencia",
        ["frequência média"] = "frecuencia media",
        ["{0} núcleos · {1} threads"] = "{0} núcleos · {1} hilos",
        ["   |   mín {0} · média {1}"] = "   |   mín {0} · media {1}",
        [" · pico {0} · turbo {1}"] = " · pico {0} · turbo {1}",
        ["{0} % do máximo ({1} de {2} MHz) · mínimo {3} MHz · carga {4} %"] =
            "{0} % del máximo ({1} de {2} MHz) · mínimo {3} MHz · carga {4} %",
        ["contador de firmware {0}"] = "contador de firmware {0}",
        ["sem leitura de potência — não é possível estimar o custo ({0})"] =
            "sin lectura de potencia — no es posible estimar el coste ({0})",
        ["nesta sessão: {0} Wh em {1} · média {2} W"] = "en esta sesión: {0} Wh en {1} · media {2} W",
        ["projecção a este ritmo: {0} €/dia · {1} €/mês"] = "proyección a este ritmo: {0} €/día · {1} €/mes",
        ["sem sensor nesta máquina: {0} — a aplicação não inventa valores."] =
            "sin sensor en esta máquina: {0} — la aplicación no inventa valores.",
        ["a gravar mas o CSV não pôde ser escrito — amostras só em memória ({0})"] =
            "grabando pero el CSV no se pudo escribir — muestras solo en memoria ({0})",
        ["a gravar · 0 amostras · {0}"] = "grabando · 0 muestras · {0}",
        ["{0} amostras em {1} s · {2}"] = "{0} muestras en {1} s · {2}",
        ["CPU {0} · GPU {1}"] = "CPU {0} · GPU {1}",
        ["potência {0} · frequência {1}"] = "potencia {0} · frecuencia {1}",
        ["{0} {1} (min {2}, max {3})"] = "{0} {1} (mín {2}, máx {3})",
        ["a gravar · {0} amostras · {1}"] = "grabando · {0} muestras · {1}",
        ["a gravar (falha ao escrever o CSV) · {0} amostras em memória"] =
            "grabando (fallo al escribir el CSV) · {0} muestras en memoria",
        ["{0} alerta(s) activo(s)"] = "{0} alerta(s) activo(s)",
        ["GPU {0} · {1}"] = "GPU {0} · {1}",
        [" (integrada, dentro do pacote)"] = " (integrada, dentro del paquete)",

        // página Discos
        ["Atualizar"] = "Actualizar",
        ["a ler discos…"] = "leyendo discos…",
        ["Falha a ler discos: "] = "Error al leer los discos: ",
        ["Nenhum disco detetado."] = "Ningún disco detectado.",
        ["Atenção"] = "Atención",
        ["sem S.M.A.R.T."] = "sin S.M.A.R.T.",
        ["Horas ligado"] = "Horas encendido",
        ["Ciclos de energia"] = "Ciclos de encendido",
        ["Desgaste"] = "Desgaste",
        ["Setores realocados"] = "Sectores reasignados",
        ["Erros I/O"] = "Errores de E/S",
        ["não montada"] = "no montada",
        ["A saúde S.M.A.R.T. vem do smartctl (por sudo NOPASSWD em /etc/sudoers.d/smartctl). " +
         "Se aparecer \"sem S.M.A.R.T.\", é porque falta essa permissão de root — o cartão do disco " +
         "mostra o comando exato para a conceder. O auto-teste é executado pelo próprio controlador: " +
         "curto demora ~2 min num NVMe, longo varre a superfície toda (pode demorar horas) e o disco " +
         "continua utilizável durante o teste."] =
            "La salud S.M.A.R.T. viene de smartctl (por sudo NOPASSWD en /etc/sudoers.d/smartctl). " +
            "Si aparece «sin S.M.A.R.T.», es porque falta ese permiso de root — la tarjeta del disco " +
            "muestra el comando exacto para concederlo. La autoprueba la ejecuta el propio controlador: " +
            "la corta tarda ~2 min en un NVMe, la larga recorre toda la superficie (puede tardar horas) y el disco " +
            "sigue usable durante la prueba.",
        ["a ler…"] = "leyendo…",
        ["Teste curto"] = "Prueba corta",
        ["Teste longo"] = "Prueba larga",
        ["Abortar"] = "Abortar",
        ["AUTO-TESTE S.M.A.R.T."] = "AUTOPRUEBA S.M.A.R.T.",
        ["a abortar o auto-teste…"] = "abortando la autoprueba…",
        ["falha a ler o auto-teste: "] = "fallo al leer la autoprueba: ",
        ["indisponível"] = "no disponible",
        ["nunca corrido"] = "nunca ejecutado",
        ["curto"] = "corto",
        ["longo"] = "largo",
        ["/dev/{0} · {1} · {2} · estado {3}"] = "/dev/{0} · {1} · {2} · estado {3}",
        ["{0} h  ({1} dias)"] = "{0} h  ({1} días)",
        ["{0} livres de {1}"] = "{0} libres de {1}",
        ["a pedir o auto-teste {0}…"] = "pidiendo la autoprueba {0}…",
        ["a correr · {0} % · {1}"] = "en curso · {0} % · {1}",
        ["a correr · {0} (o controlador não publica a percentagem)"] =
            "en curso · {0} (el controlador no publica el porcentaje)",
        ["último resultado: {0}"] = "último resultado: {0}",

        // página Rede
        ["pronto para testar"] = "listo para probar",
        ["Teste de ~15 s: latência TCP até ao router e à internet, jitter, perda de pacotes, " +
         "tempo de DNS e velocidade de download/upload (servidores da Cloudflare)."] =
            "Prueba de ~15 s: latencia TCP hasta el router y a internet, jitter, pérdida de paquetes, " +
            "tiempo de DNS y velocidad de bajada/subida (servidores de Cloudflare).",
        ["Testar rede"] = "Probar red",
        ["Diagnóstico de rede"] = "Diagnóstico de red",
        ["Latência, jitter, perda, velocidade e débito real"] =
            "Latencia, jitter, pérdida, velocidad y caudal real",
        ["Cliente (medir até outro PC)"] = "Cliente (medir hasta otro PC)",
        ["Servidor (este PC a servir)"] = "Servidor (este PC sirviendo)",
        ["Iniciar"] = "Iniciar",
        ["Parar"] = "Parar",
        ["Modo"] = "Modo",
        ["PC servidor"] = "PC servidor",
        ["Porta"] = "Puerto",
        ["Tamanho"] = "Tamaño",
        ["Mede o débito entre dois PC com a própria aplicação, sem servidores de terceiros: um lado " +
         "corre “Servidor” e o outro “Cliente” (ou --cli netbench). O cliente envia N MB e recebe os " +
         "mesmos N MB de volta; a latência vem de 20 idas e voltas de 8 bytes. Os dois sentidos são " +
         "cronometrados e contados em bytes — o que aparece é o que passou."] =
            "Mide el caudal entre dos PC con la propia aplicación, sin servidores de terceros: un lado " +
            "ejecuta «Servidor» y el otro «Cliente» (o --cli netbench). El cliente envía N MB y recibe los " +
            "mismos N MB de vuelta; la latencia sale de 20 idas y vueltas de 8 bytes. Los dos sentidos se " +
            "cronometran y se cuentan en bytes — lo que aparece es lo que pasó.",
        ["Débito real — cliente/servidor"] = "Caudal real — cliente/servidor",
        ["a escutar na porta {0} — inicia o cliente no outro PC…"] =
            "escuchando en el puerto {0} — inicia el cliente en el otro PC…",
        ["servidor falhou: "] = "el servidor falló: ",
        ["parado pelo utilizador"] = "detenido por el usuario",
        ["falhou: "] = "falló: ",
        ["Subida"] = "Subida",
        ["Descida"] = "Bajada",
        ["Latência"] = "Latencia",
        ["a parar…"] = "parando…",
        ["a testar…"] = "probando…",
        ["Estável"] = "Estable",
        ["Rede local"] = "Red local",
        ["Instável"] = "Inestable",
        ["Latência alta"] = "Latencia alta",
        ["sem dados"] = "sin datos",
        ["{0} · {1} em cada sentido · {2} s"] = "{0} · {1} en cada sentido · {2} s",
        ["{0} ms · jitter {1} ms"] = "{0} ms · jitter {1} ms",
        ["min {0} · max {1} ms · jitter {2} ms · perda {3}%"] =
            "mín {0} · máx {1} ms · jitter {2} ms · pérdida {3}%",
        ["medição concluída às {0}"] = "medición terminada a las {0}",

        // página Dispositivos
        ["Mostrar as pontes"] = "Mostrar los puentes",
        ["As pontes mostram o link que estão a servir, não um máximo comparável — ficam escondidas para a tabela respirar"] =
            "Los puentes muestran el enlace que están sirviendo, no un máximo comparable — quedan ocultos para que la tabla respire",
        ["PCI Express — link em uso vs máximo anunciado"] = "PCI Express — enlace en uso vs máximo anunciado",
        ["USB — o que está ligado e a que velocidade"] = "USB — qué está conectado y a qué velocidad",
        ["Áudio — placas, fluxos e xruns do PipeWire"] = "Audio — tarjetas, flujos y xruns de PipeWire",
        ["Sem pontes"] = "Sin puentes",
        ["Nenhum link PCI Express legível em /sys/bus/pci/devices."] =
            "Ningún enlace PCI Express legible en /sys/bus/pci/devices.",
        ["Todos os dispositivos finais negociaram o máximo que anunciam."] =
            "Todos los dispositivos finales negociaron el máximo que anuncian.",
        ["pontes (mostram o link que estão a servir)"] = "puentes (muestran el enlace que están sirviendo)",
        ["A largura e a velocidade são as negociadas agora (LnkSta do kernel). O máximo é o que o " +
         "próprio dispositivo anuncia; nas pontes o valor é o link que estão a servir, por isso não " +
         "é comparável e ficam escondidas até as pedires."] =
            "La anchura y la velocidad son las negociadas ahora (LnkSta del kernel). El máximo es lo que el " +
            "propio dispositivo anuncia; en los puentes el valor es el enlace que están sirviendo, por eso no " +
            "es comparable y quedan ocultos hasta que los pidas.",
        ["Nenhum dispositivo USB legível em /sys/bus/usb/devices."] =
            "Ningún dispositivo USB legible en /sys/bus/usb/devices.",
        ["A velocidade é a negociada com o kernel, não um teste de débito."] =
            "La velocidad es la negociada con el kernel, no una prueba de caudal.",
        ["driver "] = "controlador ",
        ["pede "] = "pide ",
        ["série "] = "serie ",
        ["Nenhum fluxo PCM aberto agora — todos os dispositivos estão \"closed\" em /proc/asound."] =
            "Ningún flujo PCM abierto ahora — todos los dispositivos están \"closed\" en /proc/asound.",
        ["fluxos activos"] = "flujos activos",
        ["xruns por nó do PipeWire"] = "xruns por nodo de PipeWire",
        ["Xruns = coluna ERR do pw-top: contador acumulado no servidor desde que cada nó arrancou. " +
         "Reiniciar o PipeWire ou a aplicação cliente volta a zero."] =
            "Xruns = columna ERR de pw-top: contador acumulado en el servidor desde que arrancó cada nodo. " +
            "Reiniciar PipeWire o la aplicación cliente lo devuelve a cero.",
        ["Xruns não medidos: o PipeWire não respondeu ao pw-top."] =
            "Xruns no medidos: PipeWire no respondió a pw-top.",
        ["Quantum de {0} ms — é o período com que o servidor acorda. Buffers maiores toleram mais jitter; "] =
            "Quantum de {0} ms — es el período con el que despierta el servidor. Búferes mayores toleran más jitter; ",
        ["buffers menores dão menos latência."] = "búferes menores dan menos latencia.",
        ["Quantum não lido: sem nó activo o pw-top não mostra o período e o pw-metadata não respondeu."] =
            "Quantum no leído: sin nodo activo pw-top no muestra el período y pw-metadata no respondió.",
        ["anuncia {0} — o link em uso é mais estreito/lento "] =
            "anuncia {0} — el enlace en uso es más estrecho/lento ",
        ["(slot, riser ou poupança de energia do driver)"] = "(slot, riser o ahorro de energía del controlador)",
        ["Mostrar as pontes ({0})"] = "Mostrar los puentes ({0})",
        ["{0} dispositivo(s) final(is) e {1} ponte(s). "] = "{0} dispositivo(s) final(es) y {1} puente(s). ",
        ["{0} dispositivo(s) abaixo do máximo anunciado (assinalados)."] =
            "{0} dispositivo(s) por debajo del máximo anunciado (señalados).",
        ["{0} dispositivo(s) ligado(s) e {1} concentrador(es)/controlador(es). "] =
            "{0} dispositivo(s) conectado(s) y {1} concentrador(es)/controlador(es). ",
        ["bus {0}, dispositivo {1}"] = "bus {0}, dispositivo {1}",
        ["{0} · {1} ({2}) — {3}"] = "{0} · {1} ({2}) — {3}",
        [", {0} Hz"] = ", {0} Hz",
        [", {0} canais"] = ", {0} canales",
        [" · período {0} ms"] = " · período {0} ms",
        [" · buffer {0} ms"] = " · búfer {0} ms",
        ["≈ {0} GB/s"] = "≈ {0} GB/s",

        // página Relatório
        ["Gera um relatório com tudo o que a aplicação mediu: sistema, CPU, memória, GPUs, discos " +
         "(S.M.A.R.T. e auto-teste), sensores, ranking de benchmarks, o último stress/ciclos/VRAM e a " +
         "última sessão gravada. O HTML é autónomo — CSS e gráficos SVG embutidos, sem rede — e abre em " +
         "qualquer browser. O JSON serve para automação."] =
            "Genera un informe con todo lo que midió la aplicación: sistema, CPU, memoria, GPUs, discos " +
            "(S.M.A.R.T. y autoprueba), sensores, ranking de benchmarks, el último estrés/ciclos/VRAM y la " +
            "última sesión grabada. El HTML es autónomo — CSS y gráficos SVG embutidos, sin red — y abre en " +
            "cualquier navegador. El JSON sirve para automatización.",
        ["Abrir"] = "Abrir",
        ["Gerar HTML completo"] = "Generar HTML completo",
        ["Gerar JSON"] = "Generar JSON",
        ["Abrir pasta"] = "Abrir carpeta",
        ["Exportar"] = "Exportar",
        ["O que vai no relatório"] = "Qué incluye el informe",
        ["Exportar a informação do sistema e o histórico de medições"] =
            "Exportar la información del sistema y el historial de mediciones",
        ["distribuição, kernel, arquitetura, uptime, placa-mãe, BIOS"] =
            "distribución, kernel, arquitectura, uptime, placa base, BIOS",
        ["modelo, núcleos, threads, frequências, cache L3"] = "modelo, núcleos, hilos, frecuencias, caché L3",
        ["total, disponível, swap"] = "total, disponible, swap",
        ["modelo, barramento PCI, driver, VRAM, temperatura"] =
            "modelo, bus PCI, controlador, VRAM, temperatura",
        ["modelo, tamanho, partições, ocupação, S.M.A.R.T. e auto-teste"] =
            "modelo, tamaño, particiones, ocupación, S.M.A.R.T. y autoprueba",
        ["todas as leituras de hwmon"] = "todas las lecturas de hwmon",
        ["ranking local com mediana, melhor, última e variação, mais as últimas 15 corridas"] =
            "ranking local con mediana, mejor, última y variación, más las últimas 15 pruebas",
        ["último CPU/RAM/GPU/disco, série de ciclos agregada e enchimento de VRAM"] =
            "último CPU/RAM/GPU/disco, serie de ciclos agregada y llenado de VRAM",
        ["resumo da última sessão gravada, com gráficos de CPU, GPU, potência e frequência"] =
            "resumen de la última sesión grabada, con gráficos de CPU, GPU, potencia y frecuencia",
        ["Documentos"] = "Documentos",
        ["a recolher dados e histórico…"] = "recopilando datos e historial…",
        ["gravado ({0} KB)"] = "guardado ({0} KB)",
        [" — relatório completo"] = " — informe completo",
        ["não foi possível abrir: "] = "no se pudo abrir: ",

        // cartão de FPS (MangoHud)
        ["Procurar sessões"] = "Buscar sesiones",
        ["Ver a escolhida"] = "Ver la elegida",
        ["FPS médio"] = "FPS medio",
        ["frametime médio"] = "frametime medio",
        ["Lê as sessões que o MangoHud grava em CSV (layer Vulkan/OpenGL). Em Wayland é a única " +
         "forma honesta de ter FPS por aplicação — o compositor não expõe frames de outros processos."] =
            "Lee las sesiones que MangoHud graba en CSV (capa Vulkan/OpenGL). En Wayland es la única " +
            "forma honesta de tener FPS por aplicación — el compositor no expone fotogramas de otros procesos.",
        ["FPS · sessão do MangoHud"] = "FPS · sesión de MangoHud",
        ["Sem sessões do MangoHud. Para gravar, corre o jogo/programa assim:  "] =
            "Sin sesiones de MangoHud. Para grabar, ejecuta el juego/programa así:  ",
        ["Sem sessões do MangoHud. Corre o jogo com MANGOHUD=1 (Steam: nas opções de " +
         "lançamento) e a pasta ~/mangologs recebe um CSV por sessão; o comando " +
         "`LinuxExplode --cli fps` lê o mesmo formato."] =
            "Sin sesiones de MangoHud. Ejecuta el juego con MANGOHUD=1 (Steam: en las opciones de " +
            "inicio) y la carpeta ~/mangologs recibe un CSV por sesión; el comando " +
            "`LinuxExplode --cli fps` lee el mismo formato.",
        ["Esse ficheiro não tem amostras válidas: "] = "Ese archivo no tiene muestras válidas: ",
        ["{0} s · {1} amostras"] = "{0} s · {1} muestras",

        // pop do benchmark
        ["1280 × 720 (padrão)"] = "1280 × 720 (predeterminado)",
        ["Benchmark — CPU a renderizar"] = "Benchmark — CPU renderizando",
        ["Benchmark — GPU a renderizar"] = "Benchmark — GPU renderizando",
        ["Benchmark — ray tracing a renderizar"] = "Benchmark — ray tracing renderizando",
        ["CPU · path tracer single-core a animar"] = "CPU · path tracer de un núcleo animando",
        ["CPU · path tracer a animar · {0} threads"] = "CPU · path tracer animando · {0} hilos",
        ["GPU · compute Vulkan a animar"] = "GPU · compute Vulkan animando",
        ["GPU · ray tracing (VK_KHR_ray_query) a animar"] = "GPU · ray tracing (VK_KHR_ray_query) animando",
        ["Fechar"] = "Cerrar",
        ["CORES EM USO"] = "NÚCLEOS EN USO",
        ["NOTA · AMOSTRAS/S ÷ 10.000"] = "NOTA · MUESTRAS/S ÷ 10.000",
        ["a preparar o dispositivo Vulkan e a medir…"] = "preparando el dispositivo Vulkan y midiendo…",
        ["falhou: não foi possível criar o dispositivo Vulkan (verifica o driver vulkan-radeon)"] =
            "falló: no se pudo crear el dispositivo Vulkan (comprueba el driver vulkan-radeon)",
        ["indisponível: esta GPU não expõe VK_KHR_ray_query"] =
            "no disponible: esta GPU no expone VK_KHR_ray_query",
        ["a renderizar numa só thread…"] = "renderizando en un solo hilo…",
        ["a renderizar com {0} threads…"] = "renderizando con {0} hilos…",
        ["{0} fotogramas/s"] = "{0} fotogramas/s",
        ["a contar fotogramas…"] = "contando fotogramas…",
        ["{0}×{1} · {2} amostras/pixel · {3} · câmara em órbita"] =
            "{0}×{1} · {2} muestras/píxel · {3} · cámara en órbita",
        ["corrida {0} · {1} thread(s) · {2} s · {3} amostras"] =
            "prueba {0} · {1} hilo(s) · {2} s · {3} muestras",
        [" — concluída; a recomeçar em {0} s"] = " — terminada; reiniciando en {0} s",

        // página Benchmark
        ["sem resultados"] = "sin resultados",
        ["procurar tipo, alvo ou detalhe…"] = "buscar tipo, objetivo o detalle…",
        ["sem medições"] = "sin mediciones",
        ["Path tracing de CPU e GPU · banda de memória · disco"] =
            "Path tracing de CPU y GPU · ancho de banda de memoria · disco",
        ["nenhuma GPU Vulkan com fila de computação — verifica o driver (vulkan-radeon / mesa)"] =
            "ninguna GPU Vulkan con cola de computación — revisa el driver (vulkan-radeon / mesa)",
        ["dedicada"] = "dedicada",
        ["integrada"] = "integrada",
        ["virtual"] = "virtual",
        ["{0} GPU(s) Vulkan detetada(s)"] = "{0} GPU(s) Vulkan detectada(s)",
        ["O mesmo path tracer da CPU, agora em compute shader Vulkan: "] =
            "El mismo path tracer de la CPU, ahora en compute shader Vulkan: ",
        ["{0} amostras por pixel, repetido até {1} s para a nota "] =
            "{0} muestras por píxel, repetido hasta {1} s para que la nota ",
        ["ser estável — a resolução escolhe-se no pop-up, de 320×180 até 4K. "] =
            "sea estable — la resolución se elige en la ventana, de 320×180 hasta 4K. ",
        ["O modo ray tracing usa VK_KHR_ray_query com 64 esferas em BVH — a nota não é comparável com a de compute."] =
            "El modo ray tracing usa VK_KHR_ray_query con 64 esferas en BVH — la nota no es comparable con la de compute.",
        ["Testar GPU"] = "Probar GPU",
        ["Testar ray tracing"] = "Probar ray tracing",
        ["NOTA GPU"] = "NOTA GPU",
        ["{0} pts · {1}T · {2}"] = "{0} pts · {1}T · {2}",
        ["primeira corrida guardada"] = "primera prueba guardada",
        ["ray tracing · "] = "ray tracing · ",
        ["compute · "] = "compute · ",
        ["primeira corrida deste alvo"] = "primera prueba de este objetivo",
        ["Multi-core"] = "Multinúcleo",
        ["Single-core"] = "Un núcleo",
        ["O mesmo renderizador do benchmark da versão Windows: path tracer determinístico, 960×540, 1024 amostras por pixel. "] =
            "El mismo renderizador del benchmark de la versión Windows: path tracer determinístico, 960×540, 1024 muestras por píxel. ",
        ["A nota é amostras por segundo ÷ 10.000 — comparável entre máquinas."] =
            "La nota es muestras por segundo ÷ 10.000 — comparable entre máquinas.",
        ["HISTÓRICO"] = "HISTORIAL",
        ["Banda de escrita, leitura, cópia e latência real de acesso à RAM."] =
            "Ancho de banda de escritura, lectura, copia y latencia real de acceso a la RAM.",
        ["Testar memória"] = "Probar memoria",
        ["Escrita sequencial"] = "Escritura secuencial",
        ["Leitura sequencial"] = "Lectura secuencial",
        ["Cópia"] = "Copia",
        ["bloco de {0}"] = "bloque de {0}",
        ["escrita {0} MB/s · leitura {1} MB/s · "] = "escritura {0} MB/s · lectura {1} MB/s · ",
        ["cópia {0} MB/s · latência {1} ns"] = "copia {0} MB/s · latencia {1} ns",
        ["primeira corrida"] = "primera prueba",
        ["Memória RAM"] = "Memoria RAM",
        ["Pointer chasing sobre blocos de 64 B (uma linha de cache), de 4 KB a 256 MB: nanossegundos por "] =
            "Pointer chasing sobre bloques de 64 B (una línea de caché), de 4 KB a 256 MB: nanosegundos por ",
        ["acesso. Cada patamar é um nível — L1, L2, L3 — e o degrau final é a RAM. "] =
            "acceso. Cada escalón es un nivel — L1, L2, L3 — y el último escalón es la RAM. ",
        ["O percurso é uma permutação aleatória com semente fixa, por isso duas corridas visitam os mesmos endereços."] =
            "El recorrido es una permutación aleatoria con semilla fija, por eso dos pruebas visitan las mismas direcciones.",
        ["Medir latência"] = "Medir latencia",
        ["Memória · latência por tamanho de bloco"] = "Memoria · latencia por tamaño de bloque",
        ["a medir 4 KB → 256 MB (alguns segundos)…"] = "midiendo 4 KB → 256 MB (unos segundos)…",
        ["{0} pontos medidos"] = "{0} puntos medidos",
        ["falhou: {0}"] = "falló: {0}",
        ["Duas tarefas fixas e repetíveis: descomprimir {0} MB "] =
            "Dos tareas fijas y repetibles: descomprimir {0} MB ",
        ["(o resultado é conferido por SHA-256) e compilar um ficheiro C com "] =
            "(el resultado se comprueba con SHA-256) y compilar un archivo C con ",
        ["{0} funções, com o compilador do sistema. "] = "{0} funciones, con el compilador del sistema. ",
        ["Não usa a rede nem o disco além do próprio trabalho — é o que um utilizador faz a compilar ou a instalar."] =
            "No usa la red ni el disco más allá del propio trabajo — es lo que hace un usuario al compilar o instalar.",
        ["Correr tarefa real"] = "Ejecutar tarea real",
        ["Tarefa do mundo real · descomprimir + compilar"] = "Tarea del mundo real · descomprimir + compilar",
        ["a correr (descomprimir + compilar)…"] = "ejecutando (descomprimir + compilar)…",
        ["descompressão   {0} MB/s"] = "descompresión   {0} MB/s",
        ["compilação      {0} kB/s   ({1} s, {2})"] = "compilación     {0} kB/s   ({1} s, {2})",
        ["compilação      —   {0}"] = "compilación     —   {0}",
        ["total           {0} s"] = "total           {0} s",
        ["descompressão verificada (SHA-256 {0}…); a compilação não correu"] =
            "descompresión verificada (SHA-256 {0}…); la compilación no se ejecutó",
        ["concluído · SHA-256 {0}… igual ao original"] = "terminado · SHA-256 {0}… igual al original",
        ["gzip {0} MB"] = "gzip {0} MB",
        ["compilar {0} funções"] = "compilar {0} funciones",
        ["kB/s de fonte"] = "kB/s de fuente",
        ["Mediana das últimas {0} corridas de cada alvo, com a variação entre elas. "] =
            "Mediana de las últimas {0} pruebas de cada objetivo, con la variación entre ellas. ",
        ["Acima de {0} % de variação a máquina não estava estável e o número vale pouco — "] =
            "Por encima de {0} % de variación la máquina no estaba estable y el número vale poco — ",
        ["o aviso aparece na linha. Para a mediana ser de 3, corre `--cli bench multi --repeticoes 3`."] =
            "el aviso aparece en la línea. Para que la mediana sea de 3, ejecuta `--cli bench multi --repeticoes 3`.",
        ["Actualizar"] = "Actualizar",
        ["Ranking local · mediana de 3 corridas"] = "Ranking local · mediana de 3 pruebas",
        ["sem corridas guardadas — corre um teste para começar o ranking"] =
            "sin pruebas guardadas — ejecuta una para empezar el ranking",
        ["{0} alvo(s) no histórico"] = "{0} objetivo(s) en el historial",
        ["Escrita e leitura sequencial + 4K aleatório num ficheiro temporário de 256 MB, "] =
            "Escritura y lectura secuencial + 4K aleatorio en un archivo temporal de 256 MB, ",
        ["com O_DIRECT (sem passar pelo cache do sistema). O ficheiro é apagado no fim."] =
            "con O_DIRECT (sin pasar por la caché del sistema). El archivo se borra al final.",
        ["Testar disco"] = "Probar disco",
        ["Sequencial · escrita"] = "Secuencial · escritura",
        ["Sequencial · leitura"] = "Secuencial · lectura",
        ["4K aleatório · leitura"] = "4K aleatorio · lectura",
        ["4K aleatório · escrita"] = "4K aleatorio · escritura",
        ["ficheiro de {0} em {1}"] = "archivo de {0} en {1}",
        ["seq escrita {0} · seq leitura {1} · "] = "sec escritura {0} · sec lectura {1} · ",
        ["4K leitura {0} · 4K escrita {1} MB/s"] = "4K lectura {0} · 4K escritura {1} MB/s",
        ["sequencial (escrita)"] = "secuencial (escritura)",
        ["Exportar CSV"] = "Exportar CSV",
        ["Exportar JSON"] = "Exportar JSON",
        ["Importar…"] = "Importar…",
        ["As notas ficam guardadas em SQLite (~/.local/share/linux-explode/historico.db), com a data e a "] =
            "Las notas se guardan en SQLite (~/.local/share/linux-explode/historico.db), con la fecha y la ",
        ["versão. Cada cartão mostra o melhor resultado, a mediana e a variação da última corrida. "] =
            "versión. Cada tarjeta muestra el mejor resultado, la mediana y la variación de la última prueba. ",
        ["Pesquisa por tipo, alvo ou detalhe e filtra por período; exporta para CSV/JSON para comparar "] =
            "Busca por tipo, objetivo o detalle y filtra por período; exporta a CSV/JSON para comparar ",
        ["máquinas, ou importa o ficheiro de outra máquina. O resultados.json antigo é migrado sozinho "] =
            "máquinas, o importa el archivo de otra máquina. El resultados.json antiguo se migra solo ",
        ["no primeiro arranque e guardado como resultados.json.migrado."] =
            "en el primer arranque y se guarda como resultados.json.migrado.",
        ["7 dias"] = "7 días",
        ["30 dias"] = "30 días",
        ["90 dias"] = "90 días",
        ["PROCURAR"] = "BUSCAR",
        ["PERÍODO"] = "PERÍODO",
        ["Histórico de resultados"] = "Historial de resultados",
        ["{0} de {1} resultado(s) no histórico"] = "{0} de {1} resultado(s) en el historial",
        [" · desde {0}"] = " · desde {0}",
        ["ainda sem resultados guardados — corre um teste"] = "aún sin resultados guardados — ejecuta una prueba",
        ["nada encontrado com esse filtro"] = "nada encontrado con ese filtro",
        ["últimas {0} corridas · {1} · {2} · "] = "últimas {0} pruebas · {1} · {2} · ",
        ["mín {0} · máx {1}"] = "mín {0} · máx {1}",
        ["gravado em {0}"] = "guardado en {0}",
        ["não consegui exportar: "] = "no se pudo exportar: ",
        ["Importar resultados (JSON)"] = "Importar resultados (JSON)",
        ["Resultados JSON"] = "Resultados JSON",
        ["{0} resultado(s) importado(s) de {1}"] = "{0} resultado(s) importado(s) de {1}",
        ["o ficheiro não tinha resultados novos"] = "el archivo no tenía resultados nuevos",
        ["não consegui importar: "] = "no se pudo importar: ",

        // página Stress
        ["pronto para começar"] = "listo para empezar",
        ["sem série registada"] = "sin serie registrada",
        ["sem teste de VRAM"] = "sin prueba de VRAM",
        ["Guardar relatório"] = "Guardar informe",
        ["Aquece CPU, RAM, GPU e disco — com guarda térmica e valores medidos"] =
            "Calienta CPU, RAM, GPU y disco — con protección térmica y valores medidos",
        ["pronto · limites de fábrica: CPU/GPU 95 °C"] = "listo · límites de fábrica: CPU/GPU 95 °C",
        ["nenhuma GPU Vulkan — falta o driver"] = "ninguna GPU Vulkan — falta el driver",
        ["falha a listar GPUs: "] = "fallo al listar GPUs: ",
        ["1 minuto"] = "1 minuto",
        ["2 minutos"] = "2 minutos",
        ["5 minutos"] = "5 minutos",
        ["10 minutos"] = "10 minutos",
        ["20 minutos"] = "20 minutos",
        ["30 minutos"] = "30 minutos",
        ["FMA vetorial (SIMD)"] = "FMA vectorial (SIMD)",
        ["vírgula flutuante"] = "coma flotante",
        ["memória"] = "memoria",
        ["25 % da RAM"] = "25 % de la RAM",
        ["50 % da RAM"] = "50 % de la RAM",
        ["75 % da RAM"] = "75 % de la RAM",
        ["Duração"] = "Duración",
        ["Threads de CPU"] = "Hilos de CPU",
        ["Carga de CPU"] = "Carga de CPU",
        ["Memória a encher"] = "Memoria a llenar",
        ["Guarda térmica"] = "Protección térmica",
        ["Disco por passagem"] = "Disco por pasada",
        ["A guarda térmica lê os sensores reais (k10temp e amdgpu) quatro vezes por segundo e para tudo " +
         "assim que um limite é passado. O ficheiro de disco é apagado no fim."] =
            "La protección térmica lee los sensores reales (k10temp y amdgpu) cuatro veces por segundo y lo para todo " +
            "en cuanto se supera un límite. El archivo de disco se borra al final.",
        ["Configuração"] = "Configuración",
        ["Cada teste corre durante a duração escolhida. CPU faz passagens contínuas de carga, a RAM " +
         "enche, relê e verifica cada palavra, o disco escreve e relê a verificar, e a GPU dispara o " +
         "path tracer em ciclo. “Tudo” corre os quatro em paralelo — é o teste de estabilidade a sério."] =
            "Cada prueba corre durante la duración elegida. La CPU hace pasadas continuas de carga, la RAM " +
            "se llena, se relee y se verifica cada palabra, el disco escribe y relee verificando, y la GPU lanza el " +
            "path tracer en bucle. «Todo» ejecuta los cuatro en paralelo — es la prueba de estabilidad de verdad.",
        ["Testes"] = "Pruebas",
        ["tudo (burn-in)"] = "todo (burn-in)",
        ["3 ciclos"] = "3 ciclos",
        ["5 ciclos"] = "5 ciclos",
        ["10 ciclos"] = "10 ciclos",
        ["20 ciclos"] = "20 ciclos",
        ["Iniciar série"] = "Iniciar serie",
        ["Parar série"] = "Parar serie",
        ["Alvo"] = "Objetivo",
        ["Duração do ciclo"] = "Duración del ciclo",
        ["Número de ciclos"] = "Número de ciclos",
        ["Pausa entre ciclos"] = "Pausa entre ciclos",
        ["A série repete o mesmo teste N vezes, com pausa entre ciclos, e dá a mediana de cada métrica " +
         "com o mínimo e o máximo. Um teste longo não mostra se a máquina repete o resultado; N ciclos " +
         "iguais mostram — e se a variação passar os 3 % o cartão avisa. A guarda térmica continua " +
         "activa e um ciclo que a dispare termina a série."] =
            "La serie repite la misma prueba N veces, con pausa entre ciclos, y da la mediana de cada métrica " +
            "con el mínimo y el máximo. Una prueba larga no muestra si la máquina repite el resultado; N ciclos " +
            "iguales lo muestran — y si la variación supera el 3 % la tarjeta avisa. La protección térmica sigue " +
            "activa y un ciclo que la dispare termina la serie.",
        ["Ciclos agendados"] = "Ciclos programados",
        ["a série de ciclos já está a correr"] = "la serie de ciclos ya está en marcha",
        ["há um teste simples a correr — usa Parar antes de arrancar a série"] =
            "hay una prueba simple en marcha — usa Parar antes de arrancar la serie",
        ["o teste de VRAM está a correr — pára-o antes de arrancar a série"] =
            "la prueba de VRAM está en marcha — párala antes de arrancar la serie",
        ["a correr: {0} · {1} ciclo(s) de {2} s"] = "en marcha: {0} · {1} ciclo(s) de {2} s",
        ["a arrancar…"] = "arrancando…",
        ["série · {0} · {1} × {2} s · pausa {3} s"] = "serie · {0} · {1} × {2} s · pausa {3} s",
        ["a série não arrancou: "] = "la serie no arrancou: ",
        ["pedido de paragem da série de ciclos"] = "pedido de parada de la serie de ciclos",
        ["{0} s · {1}"] = "{0} s · {1}",
        ["{0} s · {1} · {2}"] = "{0} s · {1} · {2}",
        ["a correr · "] = "en marcha · ",
        ["{0} · {1} s · energia {2} Wh · "] = "{0} · {1} s · energía {2} Wh · ",
        ["picos CPU {0} °C / GPU {1} °C"] = "picos CPU {0} °C / GPU {1} °C",
        ["métrica"] = "métrica",
        ["mínimo"] = "mínimo",
        ["máximo"] = "máximo",
        ["variação"] = "variación",
        ["total"] = "total",
        ["em {0}"] = "en {0}",
        ["±{0} %"] = "±{0} %",
        ["amostra pequena ({0})"] = "muestra pequeña ({0})",
        ["ciclo {0}: {1} em {2} s · "] = "ciclo {0}: {1} en {2} s · ",
        ["completo"] = "completo",
        ["parado"] = "parado",
        ["série parada"] = "serie parada",
        ["série terminada"] = "serie terminada",
        ["fim da série · {0} · pico CPU {1} °C / GPU {2} °C"] =
            "fin de la serie · {0} · pico CPU {1} °C / GPU {2} °C",
        ["até o driver recusar"] = "hasta que el driver lo rechace",
        ["a listar GPUs…"] = "listando GPUs…",
        ["Encher VRAM"] = "Llenar VRAM",
        ["Limite"] = "Límite",
        ["Bloco"] = "Bloque",
        ["Aloca blocos de VRAM em Vulkan, escreve um padrão em cada um e relê duas amostras. O total é o " +
         "que o driver aceitou; o pico e o uso vêm de /sys/class/drm (fonte independente da aplicação). " +
         "Encher até o driver recusar pode deixar o ambiente gráfico sem memória — nestas placas o limite " +
         "seguro é 2 GB com o desktop a correr."] =
            "Reserva bloques de VRAM en Vulkan, escribe un patrón en cada uno y relee dos muestras. El total es lo " +
            "que aceptó el driver; el pico y el uso vienen de /sys/class/drm (fuente independiente de la aplicación). " +
            "Llenar hasta que el driver rechace puede dejar el entorno gráfico sin memoria — en estas tarjetas el límite " +
            "seguro es 2 GB con el escritorio en marcha.",
        ["Memória da GPU (Vulkan)"] = "Memoria de la GPU (Vulkan)",
        ["o teste de VRAM já está a correr"] = "la prueba de VRAM ya está en marcha",
        ["sem GPU Vulkan — não há VRAM para encher"] = "sin GPU Vulkan — no hay VRAM que llenar",
        ["há outro teste a correr — usa Parar antes de encher a VRAM"] =
            "hay otra prueba en marcha — usa Parar antes de llenar la VRAM",
        ["a encher até o driver recusar, blocos de {0} MB"] =
            "llenando hasta que el driver rechace, bloques de {0} MB",
        ["a encher até {0} MB, blocos de {1} MB"] = "llenando hasta {0} MB, bloques de {1} MB",
        ["a alocar blocos…"] = "reservando bloques…",
        ["VRAM · GPU {0} · limite {1} · bloco {2} MB"] = "VRAM · GPU {0} · límite {1} · bloque {2} MB",
        ["até falhar"] = "hasta fallar",
        ["{0} MB"] = "{0} MB",
        ["o teste de VRAM falhou: "] = "la prueba de VRAM falló: ",
        ["sem resultado"] = "sin resultado",
        ["terminado com erro"] = "terminado con error",
        ["VRAM falhou: "] = "VRAM falló: ",
        ["não alocou nada — {0}"] = "no reservó nada — {0}",
        ["{0} bloco(s) · {1} em {2} s"] = "{0} bloque(s) · {1} en {2} s",
        [" · pico em /sys {0}"] = " · pico en /sys {0}",
        ["terminado"] = "terminado",
        ["VRAM · {0} · {1} · {2} erro(s) de verificação"] =
            "VRAM · {0} · {1} · {2} error(es) de verificación",
        ["CPU · k10temp"] = "CPU · k10temp",
        ["GPU · amdgpu"] = "GPU · amdgpu",
        ["potência · RAPL + GPU"] = "potencia · RAPL + GPU",
        ["Resultado"] = "Resultado",
        ["Registo"] = "Registro",
        ["já há um teste a correr — usa Parar primeiro"] = "ya hay una prueba en marcha — usa Parar primero",
        ["a série de ciclos está a correr — pára-a antes de arrancar um teste simples"] =
            "la serie de ciclos está en marcha — párala antes de arrancar una prueba simple",
        ["o teste de VRAM está a correr — pára-o antes de arrancar outro teste"] =
            "la prueba de VRAM está en marcha — párala antes de arrancar otra prueba",
        ["a correr: {0}"] = "en marcha: {0}",
        [" · {0} thread(s) · carga {1}"] = " · {0} hilo(s) · carga {1}",
        ["início · {0} · {1}{2} · limite {3} °C"] = "inicio · {0} · {1}{2} · límite {3} °C",
        ["não arrancou: "] = "no arrancó: ",
        ["pedido de paragem do teste de VRAM"] = "pedido de parada de la prueba de VRAM",
        ["{0} · {1}"] = "{0} · {1}",
        ["concluído"] = "terminado",
        ["{0} — {1}"] = "{0} — {1}",
        ["· picos: CPU {0} °C · GPU {1} °C · energia {2} Wh "] =
            "· picos: CPU {0} °C · GPU {1} °C · energía {2} Wh ",
        ["em {0} s"] = "en {0} s",
        ["fim · {0} · {1} · pico CPU {2} °C / GPU {3} °C"] =
            "fin · {0} · {1} · pico CPU {2} °C / GPU {3} °C",
        ["terminado — podes repetir"] = "terminado — puedes repetir",
        ["relatório gravado em "] = "informe guardado en ",
        ["não consegui gravar o relatório: "] = "no se pudo guardar el informe: ",
        ["página fechada — teste parado"] = "página cerrada — prueba detenida",
        ["página fechada — série de ciclos parada"] = "página cerrada — serie de ciclos detenida",
        ["página fechada — teste de VRAM parado"] = "página cerrada — prueba de VRAM detenida",
        ["inteiro"] = "entero",
        ["tudo"] = "todo",
        ["disco"] = "disco",
        ["todos ({0})"] = "todos ({0})",
        ["Testar CPU"] = "Probar CPU",
        ["Testar RAM"] = "Probar RAM",
        ["Tudo (burn-in)"] = "Todo (burn-in)",
        ["/sys/bus/pci, /sys/bus/usb e /proc/asound — sem root, sem ferramentas externas"] =
            "/sys/bus/pci, /sys/bus/usb y /proc/asound — sin root, sin herramientas externas",
        ["/sys/block + smartctl — ocupação, temperatura, S.M.A.R.T. e auto-teste"] =
            "/sys/block + smartctl — ocupación, temperatura, S.M.A.R.T. y autoprueba",
        ["O teste suspende a máquina a sério e mede o tempo de três formas independentes: relógio de parede " +
         "menos relógio monotónico (que pára no suspend), e o uptime (CLOCK_BOOTTIME). No fim verifica se " +
         "os sensores e a potência voltaram a responder — num portátil é aqui que se vê se o wake deixou " +
         "algo partido. Sem rtcwake no sudoers a máquina só acorda quando lhe tocas; o botão Simular mede " +
         "os relógios sem dormir e prova que a conta dá zero."] =
            "La prueba suspende la máquina de verdad y mide el tiempo de tres formas independientes: reloj de " +
            "pared menos reloj monotónico (que se detiene en el suspend) y el uptime (CLOCK_BOOTTIME). Al final " +
            "comprueba si los sensores y la potencia volvieron a responder — en un portátil es aquí donde se ve " +
            "si el wake dejó algo roto. Sin rtcwake en sudoers la máquina solo despierta cuando la tocas; el " +
            "botón Simular mide los relojes sin dormir y demuestra que la cuenta da cero."
    };
}

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

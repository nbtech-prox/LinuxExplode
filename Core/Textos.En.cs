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

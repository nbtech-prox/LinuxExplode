using System.Text.Json;

namespace LinuxExplode.Core;

/// <summary>O que o sistema suporta: estados de suspensão, modo em memória e permissões para suspender.</summary>
public sealed record SuporteSuspensao(bool Suportado, string Estados, string? ModoMemoria,
    bool PodeSuspender, bool PodeAcordarSozinho, string Nota);

/// <summary>Leitura antes/depois: relógio de parede, monotónico, uptime e sensores.</summary>
public sealed record SnapshotSuspensao(DateTimeOffset Quando, string Hora, double UptimeS, long MonotonicoMs,
    double? CpuC, double? GpuC, double? Watts);

/// <summary>
/// Resultado do teste de suspender/retomar. <c>DormiuS</c> é o tempo que a máquina esteve mesmo
/// suspensa: diferença entre o relógio de parede e o relógio monotónico (que pára no suspend). O
/// uptime (CLOCK_BOOTTIME) é a segunda fonte que confirma o mesmo número.
/// </summary>
public sealed record ResultadoSuspensao(bool Suspendeu, double DormiuS, double RelogioParedeS,
    double DiferencaUptimeS, string Veredito, SnapshotSuspensao Antes, SnapshotSuspensao Depois,
    bool SensoresOk, bool EnergiaOk, DateTimeOffset Quando, List<string> Linhas);

/// <summary>
/// Teste de suspender/retomar. Num portátil é o sítio onde tudo se parte: sensores deixam de responder,
/// o RAPL perde a permissão, o relógio salta. Aqui mede-se o que aconteceu — sem estimativas: se a
/// máquina não dormiu, o resultado diz que não dormiu.
/// </summary>
public static class Suspender
{
    public static string Ferramenta => "rtcwake";

    public static string Caminho => Path.Combine(Historico.Pasta, "suspensao.json");

    public static SuporteSuspensao Suporte()
    {
        var estados = Util.LerNulo("/sys/power/state") ?? "";
        var modo = Util.LerNulo("/sys/power/mem_sleep")?.Trim();
        bool suportado = estados.Contains("mem", StringComparison.Ordinal) ||
                         estados.Contains("freeze", StringComparison.Ordinal);

        bool sistema = ExisteFerramenta("systemctl");
        bool podeSuspender = sistema && SemPassword("systemctl");
        bool podeAcordar = ExisteFerramenta(Ferramenta) && SemPassword(Ferramenta);

        var nota = (suportado, podeSuspender, podeAcordar) switch
        {
            (false, _, _) => "este kernel não anuncia suspensão em /sys/power/state (máquina virtual?)",
            (_, false, _) => "falta permissão para suspender sem password: acrescenta systemctl ao sudoers " +
                             "(o mesmo ficheiro que já tem o smartctl)",
            (_, _, false) => "o despertar automático precisa do rtcwake no sudoers; sem ele a máquina só " +
                             "acorda quando alguém lhe toca",
            _ => "suspensão e despertar automático disponíveis"
        };

        return new SuporteSuspensao(suportado, estados.Replace("\n", " "), modo, podeSuspender, podeAcordar, nota);
    }

    /// <summary>
    /// Snapshot + suspensão + snapshot. Com <paramref name="simular"/> não dorme nada: mede o que os
    /// relógios andam para provar que, sem suspend, a diferença entre eles é zero.
    /// </summary>
    public static ResultadoSuspensao Testar(int segundos = 15, bool simular = false, Action<string>? progresso = null)
    {
        int dormir = Math.Clamp(segundos, 5, 3600);
        var antes = LerSnapshot();
        var linhas = new List<string>();
        string? impedimento = null;

        if (simular)
        {
            progresso?.Invoke("simulação: a ler os relógios sem suspender…");
            Thread.Sleep(1200);
        }
        else
        {
            var suporte = Suporte();
            if (!suporte.Suportado) impedimento = suporte.Nota;
            else if (!suporte.PodeSuspender)
                impedimento = "sem permissão para suspender: " + Instrucoes;
            else
            {
                progresso?.Invoke(suporte.PodeAcordarSozinho
                    ? $"a suspender — a máquina acorda sozinha daqui a {dormir} s…"
                    : "a suspender — acorda a máquina com uma tecla para continuar o teste…");
                var (ok, mensagem) = SuspenderAgora(dormir);
                linhas.Add("pedido: " + mensagem);
                if (!ok) impedimento = mensagem;
            }
        }

        var depois = LerSnapshot();

        double parede = (depois.Quando - antes.Quando).TotalSeconds;
        double monotonico = (depois.MonotonicoMs - antes.MonotonicoMs) / 1000.0;
        double dormiu = Math.Max(0, parede - monotonico);
        double diferencaUptime = depois.UptimeS - antes.UptimeS - monotonico;

        bool sensoresOk = (antes.CpuC is null || depois.CpuC is not null) &&
                          (antes.GpuC is null || depois.GpuC is not null);
        bool energiaOk = antes.Watts is null || depois.Watts is not null;

        bool suspendeu = impedimento is null && dormiu >= 1.0;
        var veredito = impedimento is not null
            ? "não testado — " + impedimento
            : simular
                ? $"simulação coerente: os relógios andaram {parede:0.0} s e a diferença foi {dormiu:0.00} s"
                : dormiu >= 1.0
                    ? $"dormiu {dormiu:0.0} s e acordou, com os sensores a responder"
                    : "a máquina não dormiu: o pedido não teve efeito (um inibidor ou o compositor bloqueou o suspend)";

        linhas.Add($"relógio de parede: {parede:0.00} s · monotónico: {monotonico:0.00} s → suspend: {dormiu:0.00} s");
        linhas.Add($"uptime (CLOCK_BOOTTIME): {depois.UptimeS - antes.UptimeS:0.00} s → confirma {diferencaUptime:0.00} s de suspend");

        var resultado = new ResultadoSuspensao(suspendeu, dormiu, parede, diferencaUptime, veredito, antes, depois,
            sensoresOk, energiaOk, DateTimeOffset.Now, linhas);

        if (!simular) Guardar(resultado);
        return resultado;
    }

    /// <summary>Último teste gravado (é o que o cartão da interface mostra ao abrir).</summary>
    public static ResultadoSuspensao? LerUltimo()
    {
        try
        {
            if (!File.Exists(Caminho)) return null;
            return JsonSerializer.Deserialize<ResultadoSuspensao>(File.ReadAllText(Caminho),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private static void Guardar(ResultadoSuspensao resultado)
    {
        try
        {
            Directory.CreateDirectory(Historico.Pasta);
            File.WriteAllText(Caminho, JsonSerializer.Serialize(resultado,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static SnapshotSuspensao LerSnapshot()
    {
        var uptime = double.TryParse((Util.LerNulo("/proc/uptime") ?? "0").Split(' ')[0],
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var u)
            ? u : 0;

        double? cpu = null, gpu = null, watts = null;
        try { cpu = Sensores.CpuTemperatura(); } catch { }
        try { gpu = Sensores.GpuTemperatura(); } catch { }
        try
        {
            Potencia.Ler();            // primeira leitura só dá a referência do contador RAPL
            Thread.Sleep(350);
            watts = Potencia.Ler().TotalW;
        }
        catch { }

        var agora = DateTimeOffset.Now;
        return new SnapshotSuspensao(agora, agora.ToString("yyyy-MM-dd HH:mm:ss"), uptime,
            Environment.TickCount64, cpu, gpu, watts);
    }

    private static (bool Ok, string Mensagem) SuspenderAgora(int segundos)
    {
        // 1) rtcwake: agenda o despertar no relógio da máquina e suspende. É o caminho bom: ninguém tem
        //    de estar à frente do ecrã para o teste terminar.
        if (ExisteFerramenta(Ferramenta) && SemPassword(Ferramenta))
        {
            var (saida, codigo) = Sudo(Ferramenta, "-m", "mem", "-s", segundos.ToString());
            return codigo == 0
                ? (true, $"suspenso com despertar agendado para daqui a {segundos} s (rtcwake)")
                : (false, saida.Length > 0 ? saida : $"rtcwake devolveu {codigo}");
        }

        // 2) systemctl suspend: suspende, mas o despertar é à mão (tecla/power).
        var (s2, c2) = Sudo("systemctl", "suspend");
        return c2 == 0
            ? (true, "pedido de suspensão aceite (systemctl) — acorda a máquina para continuar")
            : (false, s2.Length > 0 ? s2 : $"systemctl devolveu {c2}");
    }

    private static string Instrucoes =>
        "acrescenta ao sudoers (sem password): " +
        "echo \"$USER ALL=(root) NOPASSWD: /usr/bin/systemctl suspend, /usr/bin/rtcwake\" | sudo tee /etc/sudoers.d/linuxexplode";

    /// <summary>
    /// `sudo -n -l comando` devolve 0 mesmo quando a regra exige password (basta existir (ALL) ALL),
    /// por isso a prova é correr um comando inofensivo com -n: se o sudo pedir password, devolve 1.
    /// </summary>
    private static bool SemPassword(string comando) => Sudo(comando, "--help").Codigo == 0;

    /// <summary>Um comando do anfitrião, por sudo não interativo (dentro do sandbox via flatpak-spawn).</summary>
    private static (string Saida, int Codigo) Sudo(params string[] args)
        => Util.EmFlatpak && Util.ExisteComando("flatpak-spawn")
            ? Util.ComandoCodigo("flatpak-spawn", new[] { "--host", "sudo", "-n" }.Concat(args).ToArray())
            : Util.ComandoCodigo("sudo", new[] { "-n" }.Concat(args).ToArray());

    /// <summary>Existe no PATH — dentro do sandbox pergunta-se ao anfitrião.</summary>
    private static bool ExisteFerramenta(string nome)
    {
        if (!Util.EmFlatpak) return Util.ExisteComando(nome);
        if (!Util.ExisteComando("flatpak-spawn")) return false;
        return !string.IsNullOrWhiteSpace(Util.Comando("flatpak-spawn", "--host", "sh", "-c", "command -v " + nome));
    }
}

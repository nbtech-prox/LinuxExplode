using System.Globalization;
using System.Text.Json;

namespace LinuxExplode.Core;

public sealed record SmartInfo(bool Disponivel, string Veredito, string? Temperatura, long? HorasLigado,
    long? Ciclos, double? Desgaste, long? SetoresRealocados, long? ErrosLeitura, string? Nota);

public sealed record AutoTesteHistorico(string Tipo, string Resultado, bool? Passou, long? HorasLigado);

/// <summary>Duração do auto-teste: curto (~2 min) ou longo (varre a superfície toda).</summary>
public enum TipoAutoTeste { Curto, Longo }

/// <summary>
/// Estado do auto-teste S.M.A.R.T. do disco: a operação a correr (com percentagem), o último resultado
/// e a tabela de testes anteriores. Lido do mesmo JSON do smartctl que a saúde — num só `smartctl -a`.
/// </summary>
public sealed record AutoTesteInfo(bool Suportado, bool EmCurso, int? Percentagem, int? RestantePercent,
    string Estado, string? TipoCorrida, List<AutoTesteHistorico> Historico, string? Nota)
{
    public static AutoTesteInfo Indisponivel(string nota) =>
        new(false, false, null, null, "indisponível", null, new(), nota);
}

public sealed record DiscoInfo(string Dev, string Modelo, long Tamanho, bool Ssd, string Estado,
    List<Particao> Particoes, SmartInfo Smart, AutoTesteInfo AutoTeste);

public sealed record Particao(string Nome, string Montagem, string Fs, long Total, long Livre);

/// <summary>Discos e saúde: /sys/block + smartctl (quando presente).</summary>
public static class Discos
{
    public static List<DiscoInfo> Ler()
    {
        var lista = new List<DiscoInfo>();
        var montagens = Util.Montagens();
        var espaco = Util.Espaco();
        if (!Directory.Exists("/sys/block")) return lista;

        foreach (var dir in Directory.GetDirectories("/sys/block").OrderBy(x => x))
        {
            var nome = Path.GetFileName(dir);
            if (nome.StartsWith("loop") || nome.StartsWith("ram") || nome.StartsWith("zram") ||
                nome.StartsWith("sr") || nome.StartsWith("fd") || nome.StartsWith("md")) continue;

            long setores = Util.LerLong(dir + "/size");
            long bytes = setores * 512;
            if (bytes <= 0) continue;

            var modelo = Util.Ler(dir + "/device/model");
            if (modelo.Length == 0) modelo = Util.Ler(dir + "/device/name");
            if (modelo.Length == 0) modelo = nome;
            var vendor = Util.Ler(dir + "/device/vendor");
            if (vendor.Length > 0 && !modelo.StartsWith(vendor, StringComparison.OrdinalIgnoreCase))
                modelo = vendor + " " + modelo;

            bool ssd = Util.LerLong(dir + "/queue/rotational") == 0;
            var estado = Util.Ler(dir + "/device/state", "—");

            var partes = new List<Particao>();
            foreach (var p in Directory.GetDirectories(dir).OrderBy(x => x))
            {
                var pnome = Path.GetFileName(p);
                if (!pnome.StartsWith(nome)) continue;
                var devPath = "/dev/" + pnome;
                montagens.TryGetValue(devPath, out var m);
                long total = 0, livre = 0;
                if (!string.IsNullOrEmpty(m.Montagem))
                {
                    if (espaco.TryGetValue(m.Montagem, out var e)) { total = e.Total; livre = e.Livre; }
                    else
                    {
                        try
                        {
                            var di = new DriveInfo(m.Montagem);
                            total = di.TotalSize; livre = di.AvailableFreeSpace;
                        }
                        catch { }
                    }
                }
                var fs = m.Fs ?? Util.Ler(p + "/../device/fstype");
                partes.Add(new Particao(pnome, m.Montagem ?? "—", string.IsNullOrEmpty(fs) ? "—" : fs, total, livre));
            }

            var (smart, autoTeste) = LerSaude(nome, ssd);
            lista.Add(new DiscoInfo(nome, modelo, bytes, ssd, estado, partes, smart, autoTeste));
        }
        return lista;
    }

    private static readonly bool EmFlatpak = File.Exists("/.flatpak-info");

    private static string SemPermissao
    {
        get
        {
            // o caminho muda de distro para distro (/usr/bin no Arch, /usr/sbin no Debian/Fedora);
            // dentro do sandbox pergunta-se ao anfitrião
            string caminho;
            if (EmFlatpak && Util.ExisteComando("flatpak-spawn"))
            {
                var noHost = Util.Comando("flatpak-spawn", "--host", "sh", "-c", "command -v smartctl");
                caminho = string.IsNullOrWhiteSpace(noHost) ? "/usr/bin/smartctl" : noHost.Trim();
            }
            else
            {
                caminho = Util.CaminhoDe("smartctl") ?? "/usr/bin/smartctl";
            }
            return "S.M.A.R.T. precisa de root. Ativa uma vez (só para o smartctl): " +
                   $"echo \"$USER ALL=(root) NOPASSWD: {caminho}\" | sudo tee /etc/sudoers.d/smartctl && " +
                   "sudo chmod 440 /etc/sudoers.d/smartctl";
        }
    }

    /// <summary>As quatro formas de chamar o smartctl: direto, por sudo, e as duas dentro do sandbox.</summary>
    private static IEnumerable<(string Cmd, string[] Args)> Variantes(params string[] baseArgs)
    {
        if (EmFlatpak && Util.ExisteComando("flatpak-spawn"))
        {
            yield return ("flatpak-spawn", new[] { "--host", "smartctl" }.Concat(baseArgs).ToArray());
            yield return ("flatpak-spawn", new[] { "--host", "sudo", "-n", "smartctl" }.Concat(baseArgs).ToArray());
        }
        else
        {
            yield return ("smartctl", baseArgs);
            yield return ("sudo", new[] { "-n", "smartctl" }.Concat(baseArgs).ToArray());
        }
    }

    /// <summary>
    /// Corre o smartctl com os argumentos pedidos e devolve a primeira resposta útil. Sem root o smartctl
    /// também imprime JSON — com <c>exit_status</c> 2 e a queixa em <c>messages</c> —, por isso a resposta
    /// só conta quando traz dados (leituras) ou quando o código não acusa falha de abertura (ações).
    /// É o que faz a segunda tentativa (por sudo) acontecer.
    /// </summary>
    private static (string Json, int Codigo) Smartctl(bool exigirDados, params string[] baseArgs)
    {
        var ultimo = ("", -1);
        foreach (var (cmd, args) in Variantes(baseArgs))
        {
            var (saida, codigo) = Util.ComandoCodigo(cmd, args);
            if (saida.Length == 0) continue;
            ultimo = (saida, codigo);
            if (!saida.Contains("\"json_format_version\"")) continue;

            if (exigirDados)
            {
                if (TemDadosJson(saida)) return ultimo;
            }
            else if ((CodigoSmartctl(saida, codigo) & 0b11) == 0)
            {
                return ultimo;
            }
        }
        return ultimo;
    }

    private static bool TemDadosJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            return TemDadosSmart(raiz) || raiz.TryGetProperty("nvme_self_test_log", out _) ||
                   raiz.TryGetProperty("ata_smart_data", out _);
        }
        catch { return false; }
    }

    private static string SmartctlJson(string dev)
        => Smartctl(true, "-j", "-a", "/dev/" + dev).Json;

    /// <summary>
    /// Auto-teste: <c>curto</c> (2 min num NVMe, 1–2 min num HDD) ou <c>longo</c> (varre a superfície
    /// inteira). Arranca num processo à parte — o smartctl devolve logo o controlo e o disco continua.
    /// </summary>
    public static (bool Ok, string Mensagem) IniciarAutoTeste(string dev, TipoAutoTeste tipo)
    {
        var (json, codigoProcesso) = Smartctl(false, "-j", "-t", tipo == TipoAutoTeste.Curto ? "short" : "long", "/dev/" + dev);
        if (json.Length == 0)
            return (false, "smartctl sem resposta. " + SemPermissao);

        int estado = CodigoSmartctl(json, codigoProcesso);
        bool ok = (estado & 0b11) == 0;   // 1 = linha inválida · 2 = não abriu o dispositivo
        var msgs = MensagensSmartctl(json);
        var texto = msgs.Length > 0
            ? msgs
            : ok ? $"auto-teste {(tipo == TipoAutoTeste.Curto ? "curto" : "longo")} iniciado" : $"smartctl devolveu o código {estado}";
        return (ok, texto);
    }

    /// <summary>Aborta o auto-teste em curso (smartctl -X).</summary>
    public static (bool Ok, string Mensagem) PararAutoTeste(string dev)
    {
        var (json, codigoProcesso) = Smartctl(false, "-j", "-X", "/dev/" + dev);
        if (json.Length == 0) return (false, "smartctl sem resposta. " + SemPermissao);

        int estado = CodigoSmartctl(json, codigoProcesso);
        bool ok = (estado & 0b11) == 0;
        var msgs = MensagensSmartctl(json);
        return (ok, msgs.Length > 0 ? msgs : ok ? "auto-teste abortado" : $"smartctl devolveu o código {estado}");
    }

    /// <summary>Estado do auto-teste de um disco (uma leitura própria do smartctl).</summary>
    public static AutoTesteInfo LerAutoTeste(string dev)
    {
        if (!Util.ExisteComando("smartctl") && !EmFlatpak)
            return AutoTesteInfo.Indisponivel("smartctl não instalado (pacote smartmontools)");

        var json = SmartctlJson(dev);
        if (json.Length == 0) return AutoTesteInfo.Indisponivel("smartctl sem resposta. " + SemPermissao);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (!TemDadosSmart(raiz))
                return AutoTesteInfo.Indisponivel(NotaDeFalha(raiz));
            return InterpretarAutoTeste(raiz);
        }
        catch (Exception ex)
        {
            return AutoTesteInfo.Indisponivel("falha a ler smartctl: " + ex.Message);
        }
    }

    /// <summary>
    /// Interpreta o JSON do smartctl como se viesse do disco. Está exposto para os testes poderem
    /// correr o parser sobre as duas famílias de log (NVMe e ATA) sem depender do hardware.
    /// </summary>
    public static AutoTesteInfo InterpretarAutoTeste(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return AutoTesteInfo.Indisponivel("JSON vazio");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (!TemDadosSmart(raiz)) return AutoTesteInfo.Indisponivel(NotaDeFalha(raiz));
            return InterpretarAutoTeste(raiz);
        }
        catch (Exception ex)
        {
            return AutoTesteInfo.Indisponivel("JSON inválido: " + ex.Message);
        }
    }

    private static (SmartInfo Smart, AutoTesteInfo AutoTeste) LerSaude(string dev, bool ssd)
    {
        var temp = Temperatura(dev);

        if (!Util.ExisteComando("smartctl") && !EmFlatpak)
            return (new SmartInfo(false, "—", temp, null, null, null, null, null,
                    "smartctl não instalado — saúde S.M.A.R.T. indisponível " +
                    "(pacote smartmontools: pacman -S / apt install / dnf install)"),
                AutoTesteInfo.Indisponivel("smartctl não instalado (pacote smartmontools)"));

        var json = SmartctlJson(dev);
        if (json.Length == 0)
            return (new SmartInfo(false, "—", temp, null, null, null, null, null, "smartctl sem resposta"),
                AutoTesteInfo.Indisponivel("smartctl sem resposta"));

        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            if (!TemDadosSmart(raiz))
            {
                var nota = NotaDeFalha(raiz);
                return (new SmartInfo(false, "—", temp, null, null, null, null, null, nota),
                    AutoTesteInfo.Indisponivel(nota));
            }

            return (InterpretarSmart(raiz, temp), InterpretarAutoTeste(raiz));
        }
        catch (Exception ex)
        {
            var nota = "falha a ler smartctl: " + ex.Message;
            return (new SmartInfo(false, "—", temp, null, null, null, null, null, nota),
                AutoTesteInfo.Indisponivel(nota));
        }
    }

    /// <summary>O smartctl abriu o dispositivo e devolveu saúde, atributos ou contadores NVMe?</summary>
    private static bool TemDadosSmart(JsonElement raiz) =>
        raiz.TryGetProperty("smart_status", out _) ||
        raiz.TryGetProperty("ata_smart_attributes", out _) ||
        raiz.TryGetProperty("nvme_smart_health_information_log", out _);

    /// <summary>Porque é que não há dados: permissão (código 1/2) ou a mensagem do próprio smartctl.</summary>
    private static string NotaDeFalha(JsonElement raiz)
    {
        int estado = 0;
        if (raiz.TryGetProperty("smartctl", out var sc) && sc.TryGetProperty("exit_status", out var es))
            estado = es.GetInt32();

        if (estado is 2 or 1) return SemPermissao;

        var msg = "sem dados S.M.A.R.T.";
        if (raiz.TryGetProperty("smartctl", out var sc2) && sc2.TryGetProperty("messages", out var msgs) &&
            msgs.ValueKind == JsonValueKind.Array)
            foreach (var m in msgs.EnumerateArray())
                if (m.TryGetProperty("string", out var s2)) { msg = s2.GetString() ?? msg; break; }

        return msg.Replace("Smartctl open device:", "smartctl:").Replace("/dev/", "");
    }

    private static SmartInfo InterpretarSmart(JsonElement raiz, string? temp)
    {
        bool passou = true;
        if (raiz.TryGetProperty("smart_status", out var st) && st.TryGetProperty("passed", out var p))
            passou = p.GetBoolean();

        long? horas = null, ciclos = null, realocados = null, errosLeitura = null;
        double? desgaste = null;

        if (raiz.TryGetProperty("power_on_time", out var pot) && pot.TryGetProperty("hours", out var h))
            horas = h.GetInt64();
        if (raiz.TryGetProperty("power_cycle_count", out var pc))
            ciclos = pc.GetInt64();

        if (raiz.TryGetProperty("ata_smart_attributes", out var attrs) &&
            attrs.TryGetProperty("table", out var tabela))
        {
            foreach (var a in tabela.EnumerateArray())
            {
                var id = a.TryGetProperty("id", out var i) ? i.GetInt32() : 0;
                long bruto = 0;
                var brutoTxt = "";
                if (a.TryGetProperty("raw", out var raw))
                {
                    if (raw.TryGetProperty("value", out var rv)) bruto = rv.GetInt64();
                    if (raw.TryGetProperty("string", out var rs)) brutoTxt = rs.GetString() ?? "";
                }
                switch (id)
                {
                    case 5: realocados = bruto; break;
                    case 187: errosLeitura ??= bruto; break;
                    case 197: errosLeitura ??= bruto; break;
                    case 202: desgaste ??= bruto; break;
                    case 231: desgaste ??= bruto; break;
                    case 190:
                    case 194:
                        // temperatura: "35 (Min/Max 20/45)" ou o byte baixo do valor bruto
                        var m = System.Text.RegularExpressions.Regex.Match(brutoTxt, @"^\s*(\d{1,3})");
                        long t = m.Success && long.TryParse(m.Groups[1].Value, out var tv) ? tv : (bruto & 0xFF);
                        if (t is > 0 and < 120) temp ??= $"{t} °C";
                        break;
                }
            }
        }

        if (raiz.TryGetProperty("nvme_smart_health_information_log", out var nv))
        {
            if (nv.TryGetProperty("percentage_used", out var pu)) desgaste = pu.GetInt32();
            if (nv.TryGetProperty("media_errors", out var me)) errosLeitura = me.GetInt64();
            if (nv.TryGetProperty("temperature", out var t) && temp is null) temp = $"{t.GetInt32()} °C";
        }

        // Veredito
        int pontos = 0;
        var notas = new List<string>();
        if (!passou) { pontos += 3; notas.Add("S.M.A.R.T. reprovado"); }
        if (realocados > 0) { pontos += realocados > 8 ? 3 : 1; notas.Add($"{realocados} setores realocados"); }
        if (errosLeitura > 0) { pontos += errosLeitura > 50 ? 2 : 1; notas.Add($"{errosLeitura} erros"); }
        if (desgaste is double d && d >= 90) { pontos += 2; notas.Add($"desgaste {d:0}%"); }
        else if (desgaste is double d2 && d2 >= 70) { pontos += 1; notas.Add($"desgaste {d2:0}%"); }
        if (temp is not null && double.TryParse(temp.Replace(" °C", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var tc) && tc >= 60)
        { pontos += 1; notas.Add($"temperatura {tc:0} °C"); }

        var veredito = pontos >= 3 ? "Mau" : pontos >= 1 ? "Atenção" : "Bom";
        return new SmartInfo(true, veredito, temp, horas, ciclos, desgaste, realocados, errosLeitura,
            notas.Count > 0 ? string.Join(" · ", notas) : "sem problemas registados");
    }

    /// <summary>
    /// Lê o auto-teste das duas famílias de log: NVMe (<c>nvme_self_test_log</c>, com percentagem
    /// em curso) e ATA (<c>ata_smart_data.self_test</c> + tabela de resultados anteriores).
    /// </summary>
    private static AutoTesteInfo InterpretarAutoTeste(JsonElement raiz)
    {
        var historico = new List<AutoTesteHistorico>();
        bool suportado = false, emCurso = false;
        int? percentagem = null, restante = null;
        string? tipoCorrida = null;

        if (raiz.TryGetProperty("nvme_self_test_log", out var nv) && nv.ValueKind == JsonValueKind.Object)
        {
            suportado = true;

            if (nv.TryGetProperty("current_self_test_operation", out var op))
            {
                var txt = Texto(op, "string");
                if (!string.IsNullOrEmpty(txt) &&
                    !txt.StartsWith("No self-test", StringComparison.OrdinalIgnoreCase))
                {
                    emCurso = true;
                    tipoCorrida = txt;
                }
            }

            if (nv.TryGetProperty("current_self_test_completion_percent", out var pc) && pc.TryGetInt32(out var p))
            {
                percentagem = Math.Clamp(p, 0, 100);
                restante = 100 - percentagem;
            }

            if (nv.TryGetProperty("table", out var tabela) && tabela.ValueKind == JsonValueKind.Array)
                foreach (var t in tabela.EnumerateArray())
                    historico.Add(new AutoTesteHistorico(
                        TraduzTipo(Texto(t, "self_test_code", "string")),
                        TraduzResultado(Texto(t, "self_test_result", "string")),
                        Passou(Texto(t, "self_test_result", "string")),
                        Numero(t, "power_on_hours")));
        }

        if (raiz.TryGetProperty("ata_smart_data", out var ad))
        {
            suportado = true;
            if (ad.TryGetProperty("self_test", out var st) && st.TryGetProperty("status", out var s))
            {
                var txt = Texto(s, "string");
                var valor = Numero(s, "value");
                restante = (int?)Numero(s, "remaining_percent");
                emCurso = (txt?.Contains("in progress", StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (restante is > 0 && restante < 100);
                if (emCurso && restante is int r)
                {
                    percentagem = Math.Clamp(100 - r, 0, 100);
                    tipoCorrida = "em curso";
                }
                else if (txt is not null && valor is 0)
                {
                    tipoCorrida = null;
                }
            }
        }

        if (raiz.TryGetProperty("ata_smart_self_test_log", out var log) && log.ValueKind == JsonValueKind.Object)
        {
            suportado = true;
            foreach (var bloco in log.EnumerateObject())
            {
                if (bloco.Value.ValueKind != JsonValueKind.Object) continue;
                if (!bloco.Value.TryGetProperty("table", out var tabela) || tabela.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var t in tabela.EnumerateArray())
                {
                    var tipo = TraduzTipo(Texto(t, "type", "string"));
                    var resultado = TraduzResultado(Texto(t, "status", "string"));
                    // a linha "in progress" não é histórico: é o teste a correr
                    if (resultado == "a correr") { emCurso = true; tipoCorrida ??= tipo; continue; }
                    var horas = Numero(t, "lifetime_hours");
                    var passou = t.TryGetProperty("status", out var st2) && st2.TryGetProperty("passed", out var pp)
                        ? pp.GetBoolean()
                        : (bool?)null;
                    if (!historico.Any(x => x.Tipo == tipo && x.Resultado == resultado && x.HorasLigado == horas))
                        historico.Add(new AutoTesteHistorico(tipo, resultado, passou, horas));
                }
            }
        }

        var estado = emCurso
            ? percentagem is int pct ? $"a correr · {pct} %" : "a correr"
            : !suportado ? "indisponível"
            : historico.Count > 0 ? historico[0].Resultado
            : "nunca corrido";

        string? nota = null;
        if (!suportado) nota = "este disco não expõe log de auto-teste (o smartctl não o reporta)";
        else if (emCurso && percentagem is null) nota = "o controlador não publica a percentagem deste teste";

        return new AutoTesteInfo(suportado, emCurso, percentagem, restante, estado, tipoCorrida, historico, nota);
    }

    private static string? Texto(JsonElement e, params string[] caminho)
    {
        foreach (var c in caminho)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(c, out e)) return null;
        }
        return e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.ToString(),
            _ => null
        };
    }

    private static long? Numero(JsonElement e, params string[] caminho)
    {
        foreach (var c in caminho)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(c, out e)) return null;
        }
        return e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var v) ? v : null;
    }

    private static bool? Passou(string? resultado) => resultado switch
    {
        null => null,
        "Completed without error" => true,
        "concluído sem erros" => true,
        _ when resultado.Contains("failure", StringComparison.OrdinalIgnoreCase) => false,
        _ => null
    };

    private static string TraduzTipo(string? tipo) => tipo switch
    {
        null => "—",
        "Short" => "curto",
        "Extended" => "longo",
        "Conveyance" => "conveyance",
        "Short offline" => "curto",
        "Extended offline" => "longo",
        "Conveyance offline" => "conveyance",
        _ => tipo
    };

    private static string TraduzResultado(string? s) => s switch
    {
        null => "—",
        "Completed without error" => "concluído sem erros",
        "Self-test routine in progress" => "a correr",
        "Aborted by host" => "abortado pelo utilizador",
        "Interrupted (host reset)" => "interrompido (reset do anfitrião)",
        "Fatal or unknown error" => "erro fatal",
        "Completed: read failure" => "concluído com falha de leitura",
        "Completed: electrical failure" => "concluído com falha eléctrica",
        "Completed: servo/seek failure" => "concluído com falha de servo",
        "Completed: handling damage" => "concluído com danos",
        "Unknown" => "desconhecido",
        _ when s.StartsWith("Aborted", StringComparison.OrdinalIgnoreCase) => "abortado",
        _ => s
    };

    /// <summary>Código de saída do smartctl: o do JSON quando existe, senão o do processo.</summary>
    private static int CodigoSmartctl(string json, int codigoProcesso)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("smartctl", out var sc) &&
                sc.TryGetProperty("exit_status", out var es))
                return es.GetInt32();
        }
        catch { }
        return codigoProcesso;
    }

    private static string MensagensSmartctl(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("smartctl", out var sc) &&
                sc.TryGetProperty("messages", out var ms) && ms.ValueKind == JsonValueKind.Array)
            {
                var lista = new List<string>();
                foreach (var m in ms.EnumerateArray())
                    if (m.TryGetProperty("string", out var s) && s.GetString() is { Length: > 0 } txt)
                        lista.Add(txt.Replace("Smartctl open device:", "smartctl:").Replace("/dev/", ""));
                return string.Join(" · ", lista);
            }
        }
        catch { }
        return "";
    }

    private static string? Temperatura(string dev)
    {
        // Controlador NVMe: nvme0n1 → nvme0
        var controlador = dev.StartsWith("nvme") && dev.Contains('n')
            ? dev[..dev.LastIndexOf('n')]
            : dev;

        try
        {
            // 1) hwmon do próprio controlador (o caminho em /sys/class/nvme aponta para outro sítio)
            if (dev.StartsWith("nvme") && Directory.Exists("/sys/class/hwmon"))
            {
                foreach (var h in Directory.GetDirectories("/sys/class/hwmon"))
                {
                    var nome = Util.Ler(h + "/name");
                    if (nome != "nvme") continue;
                    string real;
                    try { real = Directory.ResolveLinkTarget(h, true)?.FullName ?? h; } catch { real = h; }
                    if (!real.Contains("/" + controlador + "/")) continue;
                    var t = Util.LerLong(h + "/temp1_input");
                    if (t != 0) return $"{t / 1000.0:0} °C";
                }
            }

            // 2) hwmon ligado ao dispositivo de bloco (drivetemp, USB, etc.)
            foreach (var h in Directory.GetDirectories($"/sys/block/{dev}/device/hwmon"))
            {
                var t = Util.LerLong(h + "/temp1_input");
                if (t != 0) return $"{t / 1000.0:0} °C";
            }
            var direto = Util.LerLong($"/sys/block/{dev}/device/temp1_input");
            if (direto != 0) return $"{direto / 1000.0:0} °C";
        }
        catch { }
        return null;
    }
}

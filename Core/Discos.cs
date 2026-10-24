using System.Globalization;
using System.Text.Json;

namespace LinuxExplode.Core;

public sealed record SmartInfo(bool Disponivel, string Veredito, string? Temperatura, long? HorasLigado,
    long? Ciclos, double? Desgaste, long? SetoresRealocados, long? ErrosLeitura, string? Nota);

public sealed record DiscoInfo(string Dev, string Modelo, long Tamanho, bool Ssd, string Estado,
    List<Particao> Particoes, SmartInfo Smart);

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

            lista.Add(new DiscoInfo(nome, modelo, bytes, ssd, estado, partes, LerSmart(nome, ssd)));
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

    /// <summary>
    /// Tenta ler o JSON do smartctl: primeiro sem privilégios, depois com sudo não interativo.
    /// Dentro do Flatpak corre no anfitrião via flatpak-spawn (o sandbox não vê /dev).
    /// </summary>
    private static string SmartctlJson(string dev)
    {
        var alvo = "/dev/" + dev;
        var baseArgs = new[] { "-j", "-H", "-A", "-i", alvo };
        var tentativas = new List<(string Cmd, string[] Args)>();

        if (EmFlatpak && Util.ExisteComando("flatpak-spawn"))
        {
            tentativas.Add(("flatpak-spawn", new[] { "--host", "smartctl" }.Concat(baseArgs).ToArray()));
            tentativas.Add(("flatpak-spawn", new[] { "--host", "sudo", "-n", "smartctl" }.Concat(baseArgs).ToArray()));
        }
        else
        {
            tentativas.Add(("smartctl", baseArgs));
            tentativas.Add(("sudo", new[] { "-n", "smartctl" }.Concat(baseArgs).ToArray()));
        }

        var ultimo = "";
        foreach (var (cmd, args) in tentativas)
        {
            var saida = Util.Comando(cmd, args);
            if (saida.Length == 0) continue;
            ultimo = saida;
            if (saida.Contains("\"smart_status\"") ||
                saida.Contains("\"nvme_smart_health_information_log\"") ||
                saida.Contains("\"ata_smart_attributes\""))
                return saida;
        }
        return ultimo;
    }

    private static SmartInfo LerSmart(string dev, bool ssd)
    {
        var temp = Temperatura(dev);

        if (!Util.ExisteComando("smartctl") && !EmFlatpak)
            return new SmartInfo(false, "—", temp, null, null, null, null, null,
                "smartctl não instalado — saúde S.M.A.R.T. indisponível " +
                "(pacote smartmontools: pacman -S / apt install / dnf install)");

        var json = SmartctlJson(dev);
        if (json.Length == 0)
            return new SmartInfo(false, "—", temp, null, null, null, null, null, "smartctl sem resposta");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            bool passou = true;
            if (raiz.TryGetProperty("smart_status", out var st) && st.TryGetProperty("passed", out var p))
                passou = p.GetBoolean();

            // sem smart_status nem tabela de atributos → o smartctl não conseguiu abrir o dispositivo
            bool temDados = raiz.TryGetProperty("smart_status", out _) ||
                            raiz.TryGetProperty("ata_smart_attributes", out _) ||
                            raiz.TryGetProperty("nvme_smart_health_information_log", out _);
            if (!temDados)
            {
                int estado = 0;
                if (raiz.TryGetProperty("smartctl", out var sc) && sc.TryGetProperty("exit_status", out var es))
                    estado = es.GetInt32();

                if (estado is 2 or 1)
                    return new SmartInfo(false, "—", temp, null, null, null, null, null,
                        SemPermissao);

                var msg = "sem dados S.M.A.R.T.";
                if (raiz.TryGetProperty("smartctl", out var sc2) && sc2.TryGetProperty("messages", out var msgs))
                    foreach (var m in msgs.EnumerateArray())
                        if (m.TryGetProperty("string", out var s2)) { msg = s2.GetString() ?? msg; break; }
                return new SmartInfo(false, "—", temp, null, null, null, null, null,
                    msg.Replace("Smartctl open device:", "smartctl:").Replace("/dev/", ""));
            }

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
        catch (Exception ex)
        {
            return new SmartInfo(false, "—", temp, null, null, null, null, null, "falha a ler smartctl: " + ex.Message);
        }
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

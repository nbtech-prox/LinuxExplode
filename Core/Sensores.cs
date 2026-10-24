using System.Globalization;

namespace LinuxExplode.Core;

public enum TipoSensor { Temperatura, Ventoinha, Energia, Tensao, Corrente, Frequencia, Outro }

public sealed record Sensor(string Chip, string Nome, TipoSensor Tipo, double Valor, string Unidade)
{
    public string ValorTexto => Tipo switch
    {
        TipoSensor.Temperatura => $"{Valor:0.0} °C",
        TipoSensor.Ventoinha => $"{Valor:0} rpm",
        TipoSensor.Energia => Valor >= 1_000_000 ? $"{Valor / 1_000_000:0.00} W" : $"{Valor / 1000:0.0} mW",
        TipoSensor.Tensao => $"{Valor / 1000:0.000} V",
        TipoSensor.Corrente => $"{Valor / 1000:0.000} A",
        TipoSensor.Frequencia => Valor >= 1e9 ? $"{Valor / 1e9:0.00} GHz" : $"{Valor / 1e6:0} MHz",
        _ => $"{Valor:0.##} {Unidade}"
    };
}

/// <summary>Sensores de hardware via /sys/class/hwmon — o equivalente Linux ao LibreHardwareMonitor.</summary>
public static class Sensores
{
    public static List<Sensor> Ler()
    {
        var lista = new List<Sensor>();
        var _vistos = new Dictionary<string, int>(StringComparer.Ordinal);
        string raiz = "/sys/class/hwmon";
        if (!Directory.Exists(raiz)) return lista;

        foreach (var dir in Directory.GetDirectories(raiz).OrderBy(x => x))
        {
            var chip = Util.Ler(dir + "/name");
            if (chip.Length == 0) chip = Path.GetFileName(dir);
            chip = NomeAmigavel(chip);
            // Junta o endereço PCI (ou o nome do dispositivo) para distinguir duas GPUs iguais
            var pci = EnderecoPci(dir);
            if (pci is not null) chip = $"{chip} · {pci}";
            if (_vistos.TryGetValue(chip, out var n)) _vistos[chip] = ++n; else _vistos[chip] = 1;
            if (_vistos[chip] > 1) chip = $"{chip} #{_vistos[chip]}";

            // Recolhe o chip inteiro primeiro para poder ordenar e desambiguar nomes repetidos
            var doChip = new List<(Sensor S, int Canal)>();

            foreach (var f in Directory.GetFiles(dir))
            {
                var nome = Path.GetFileName(f);
                if (nome is null) continue;

                // temperatura
                if (nome.StartsWith("temp") && nome.EndsWith("_input"))
                {
                    var v = Util.LerLong(f);
                    if (v <= 0) continue;
                    doChip.Add((new(chip, Etiqueta(dir, nome, "temp"), TipoSensor.Temperatura, v / 1000.0, "°C"), Canal(nome, "temp")));
                }
                // ventoinha
                else if (nome.StartsWith("fan") && nome.EndsWith("_input"))
                {
                    var v = Util.LerLong(f);
                    if (v <= 0) continue;
                    doChip.Add((new(chip, Etiqueta(dir, nome, "fan"), TipoSensor.Ventoinha, v, "rpm"), Canal(nome, "fan")));
                }
                // energia
                else if (nome.StartsWith("power") && (nome.EndsWith("_average") || nome.EndsWith("_input")))
                {
                    var v = Util.LerLong(f);
                    if (v <= 0) continue;
                    // evita duplicar average+input
                    if (nome.EndsWith("_input") && File.Exists(f.Replace("_input", "_average"))) continue;
                    doChip.Add((new(chip, Etiqueta(dir, nome, "power"), TipoSensor.Energia, v, "W"), Canal(nome, "power")));
                }
                // tensão
                else if (nome.StartsWith("in") && nome.EndsWith("_input"))
                {
                    var v = Util.LerLong(f);
                    if (v <= 0) continue;
                    doChip.Add((new(chip, Etiqueta(dir, nome, "in"), TipoSensor.Tensao, v, "V"), Canal(nome, "in")));
                }
                // corrente
                else if (nome.StartsWith("curr") && nome.EndsWith("_input"))
                {
                    var v = Util.LerLong(f);
                    if (v == 0) continue;
                    doChip.Add((new(chip, Etiqueta(dir, nome, "curr"), TipoSensor.Corrente, v, "A"), Canal(nome, "curr")));
                }
                // frequência (amdgpu/hwmon)
                else if (nome.StartsWith("freq") && nome.EndsWith("_input"))
                {
                    var v = Util.LerLong(f);
                    if (v <= 0) continue;
                    doChip.Add((new(chip, Etiqueta(dir, nome, "freq"), TipoSensor.Frequencia, v, "Hz"), Canal(nome, "freq")));
                }
            }

            // O nct6687 dá o mesmo rótulo a canais diferentes ("Thermistor 15" em temp3 e temp6);
            // acrescenta o canal quando o nome se repete dentro do mesmo chip.
            var repetidos = doChip.GroupBy(x => x.S.Nome, StringComparer.Ordinal)
                                  .Where(g => g.Count() > 1)
                                  .Select(g => g.Key)
                                  .ToHashSet(StringComparer.Ordinal);

            foreach (var (s, canal) in doChip)
                lista.Add(repetidos.Contains(s.Nome) ? s with { Nome = $"{s.Nome} · {Prefixo(s.Tipo)}{canal}" } : s);
        }

        // ordem estável: chip, depois tipo (temperaturas primeiro) e por fim o canal
        return lista
            .OrderBy(s => s.Chip, StringComparer.Ordinal)
            .ThenBy(s => OrdemTipo(s.Tipo))
            .ThenBy(s => CanalDoNome(s.Nome))
            .ThenBy(s => s.Nome, StringComparer.Ordinal)
            .ToList();
    }

    private static int OrdemTipo(TipoSensor t) => t switch
    {
        TipoSensor.Temperatura => 0,
        TipoSensor.Ventoinha => 1,
        TipoSensor.Energia => 2,
        TipoSensor.Tensao => 3,
        TipoSensor.Corrente => 4,
        TipoSensor.Frequencia => 5,
        _ => 6
    };

    private static int Canal(string ficheiro, string prefixo)
    {
        var i = ficheiro.IndexOf('_');
        if (i <= prefixo.Length) return 0;
        return int.TryParse(ficheiro[prefixo.Length..i], out var n) ? n : 0;
    }

    /// <summary>Número do canal no fim do nome ("Ventoinha 12", "... · temp6"), para ordenar.</summary>
    private static int CanalDoNome(string nome)
    {
        int i = nome.Length - 1;
        while (i >= 0 && char.IsDigit(nome[i])) i--;
        if (i == nome.Length - 1) return int.MaxValue;
        return int.TryParse(nome[(i + 1)..], out var n) ? n : int.MaxValue;
    }

    private static string Prefixo(TipoSensor t) => t switch
    {
        TipoSensor.Temperatura => "temp",
        TipoSensor.Ventoinha => "fan",
        TipoSensor.Energia => "power",
        TipoSensor.Tensao => "in",
        TipoSensor.Corrente => "curr",
        TipoSensor.Frequencia => "freq",
        _ => "canal"
    };

    private static string Etiqueta(string dir, string ficheiro, string prefixo)
    {
        var i = ficheiro.IndexOf('_');
        var num = i > 0 ? ficheiro[prefixo.Length..i] : "";
        var lbl = Util.Ler($"{dir}/{prefixo}{num}_label");
        if (lbl.Length > 0) return lbl;
        return prefixo switch
        {
            "temp" => $"Temp {num}",
            "fan" => $"Ventoinha {num}",
            "power" => $"Potência {num}",
            "in" => $"Tensão {num}",
            "curr" => $"Corrente {num}",
            _ => $"Sensor {num}"
        };
    }

    /// <summary>Endereço PCI do dispositivo dono do hwmon (ex.: 03:00.0), para distinguir GPUs iguais.</summary>
    private static string? EnderecoPci(string dirHwmon)
    {
        try
        {
            var alvo = new DirectoryInfo(dirHwmon + "/device").LinkTarget;
            if (string.IsNullOrEmpty(alvo)) return null;
            var partes = alvo.TrimEnd('/').Split('/');
            for (int i = partes.Length - 1; i >= 0; i--)
            {
                var p = partes[i];
                if (p.Length >= 7 && p[4] == ':' && char.IsDigit(p[0])) return p[5..];
            }
        }
        catch { }
        return null;
    }

    private static string NomeAmigavel(string n) => n.ToLowerInvariant() switch
    {
        "k10temp" or "zenpower" => "CPU (k10temp)",
        "coretemp" => "CPU (coretemp)",
        "amdgpu" => "GPU (amdgpu)",
        "amdgpu_hwmon" => "GPU (amdgpu)",
        "nvme" => "NVMe",
        "nvidia" or "nouveau" => "GPU (NVIDIA)",
        "dell_smm" or "thinkpad" or "asus" or "asus_wmi_sensors" => "Portátil",
        "iwlwifi_1" => "Wi-Fi",
        "acpitz" => "Chassis (ACPI)",
        "it87" => "Placa-mãe (IT87)",
        "nct6775" or "nct6798" => "Placa-mãe (NCT6775)",
        "nct6683" => "Placa-mãe (NCT6683D)",
        "nct6686" => "Placa-mãe (NCT6686D)",
        "nct6687" => "Placa-mãe (NCT6687D)",
        _ => n
    };

    /// <summary>Temperatura da CPU, escolhendo o canal Tctl/CPU quando existir.</summary>
    public static double? CpuTemperatura(List<Sensor>? sensores = null)
    {
        sensores ??= Ler();
        var cpu = sensores.Where(s => s.Tipo == TipoSensor.Temperatura &&
                                      (s.Chip.Contains("k10temp", StringComparison.OrdinalIgnoreCase) ||
                                       s.Chip.Contains("coretemp", StringComparison.OrdinalIgnoreCase) ||
                                       s.Chip.Contains("CPU", StringComparison.OrdinalIgnoreCase))).ToList();
        if (cpu.Count == 0) return null;
        // Tctl costuma ser o canal mais alto; devolve-o.
        return cpu.Max(s => s.Valor);
    }

    public static double? GpuTemperatura(List<Sensor>? sensores = null)
    {
        sensores ??= Sensores.Ler();
        var gpu = sensores.Where(s => s.Tipo == TipoSensor.Temperatura &&
                                      s.Chip.Contains("GPU", StringComparison.OrdinalIgnoreCase)).ToList();
        return gpu.Count > 0 ? gpu.Max(s => s.Valor) : null;
    }
}

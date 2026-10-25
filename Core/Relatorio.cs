using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinuxExplode.Core;

/// <summary>Exporta a informação do sistema em HTML (com a marca EXPLODE) ou JSON.</summary>
public static class Relatorio
{
    public const string Versao = "0.20.7";

    public static string GerarJson(Sistema s, List<Sensor> sensores, List<DiscoInfo> discos)
    {
        var obj = new
        {
            app = "LINUX EXPLODE",
            versao = Versao,
            gerado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            sistema = new
            {
                so = s.So,
                kernel = s.Kernel,
                arquitetura = s.Arquitetura,
                hostname = s.Hostname,
                uptime = s.Uptime,
                maquina = s.Maquina,
                placa_mae = s.PlacaMae,
                bios = s.Bios
            },
            cpu = new
            {
                modelo = s.CpuModelo,
                nucleos = s.NucleosFisicos,
                threads = s.Threads,
                frequencia_atual = s.CpuFreqAtual,
                frequencia_max = s.CpuFreqMax,      // mantido: contrato anterior do relatório
                frequencia_min = s.CpuFreqMinAtual,
                frequencia_pico = s.CpuFreqPico,
                frequencia_turbo = s.CpuFreqMax,
                frequencia_minima = s.CpuFreqMinimo,
                cache_l3 = s.CpuCache,
                threads_frequencia = s.Freq.Threads.Select(t => new
                {
                    cpu = t.Cpu,
                    nucleo = t.Nucleo,
                    pacote = t.Pacote,
                    atual = FrequenciaCpu.Ghz(t.AtualKHz),
                    maximo = FrequenciaCpu.Ghz(t.MaxKHz)
                })
            },
            memoria = new
            {
                total = Util.Bytes(s.RamTotal),
                disponivel = Util.Bytes(s.RamLivre),
                em_uso = Util.Bytes(s.RamTotal - s.RamLivre),
                swap_total = Util.Bytes(s.SwapTotal),
                swap_usada = Util.Bytes(s.SwapUsada)
            },
            gpus = s.Gpus.Select(g => new
            {
                nome = g.Nome, pci = g.Pci, driver = g.Driver,
                temperatura = g.Temp, uso = g.Uso,
                vram_total = g.VramTotal is null ? null : Util.Bytes(g.VramTotal.Value),
                vram_usada = g.VramUsada is null ? null : Util.Bytes(g.VramUsada.Value)
            }),
            frequencias_gpu = FrequenciaGpu.Ler(s.Gpus).Select(g => new
            {
                nome = g.Nome,
                pci = g.Pci,
                nucleo = FrequenciaGpu.Mhz(g.NucleoHz),
                nucleo_maximo = FrequenciaGpu.Mhz(g.NucleoMaxHz),
                memoria = g.MemoriaMaxHz > 0 ? FrequenciaGpu.Mhz(g.MemoriaHz) : null,
                memoria_maxima = g.MemoriaMaxHz > 0 ? FrequenciaGpu.Mhz(g.MemoriaMaxHz) : null
            }),
            sensores = sensores.Select(x => new { chip = x.Chip, nome = x.Nome, tipo = x.Tipo.ToString(), valor = x.ValorTexto }),
            discos = discos.Select(d => new
            {
                dispositivo = d.Dev, modelo = d.Modelo, tamanho = Util.Bytes(d.Tamanho),
                tipo = d.Ssd ? "SSD" : "HDD", estado = d.Estado,
                veredito = d.Smart.Veredito, temperatura = d.Smart.Temperatura,
                horas_ligado = d.Smart.HorasLigado, desgaste = d.Smart.Desgaste,
                setores_realocados = d.Smart.SetoresRealocados,
                particoes = d.Particoes.Select(p => new { nome = p.Nome, montagem = p.Montagem, fs = p.Fs, total = Util.Bytes(p.Total), livre = Util.Bytes(p.Livre) })
            })
        };
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    public static string GerarHtml(Sistema s, List<Sensor> sensores, List<DiscoInfo> discos)
    {
        var sb = new StringBuilder();
        string E(string? t) => System.Net.WebUtility.HtmlEncode(t ?? "—");

        sb.Append("""
<!doctype html>
<html lang="pt-PT">
<head>
<meta charset="utf-8">
<title>LINUX EXPLODE — Relatório</title>
<style>
  :root { color-scheme: dark; }
  * { box-sizing: border-box; }
  body { margin:0; padding:40px; background:#0A0A0A; color:#F2F2F2;
         font-family: Inter, "Noto Sans", -apple-system, sans-serif; }
  h1 { font-size:22px; letter-spacing:4px; margin:0 0 4px; }
  h2 { font-size:12px; letter-spacing:2px; color:#8C8C8C; margin:30px 0 10px;
       text-transform:uppercase; font-weight:600; }
  .sub { color:#8C8C8C; font-size:13px; margin-bottom:26px; }
  .grelha { display:grid; grid-template-columns:repeat(auto-fit,minmax(300px,1fr)); gap:14px; }
  .cartao { background:#111; border:1px solid #202020; border-radius:12px; padding:16px 18px; }
  table { width:100%; border-collapse:collapse; font-size:13px; }
  td { padding:7px 0; border-bottom:1px solid #1B1B1B; vertical-align:top; }
  td:first-child { color:#8C8C8C; width:44%; }
  td:last-child { font-family:"JetBrains Mono", ui-monospace, monospace; }
  tr:last-child td { border-bottom:none; }
  .pe { color:#4A4A4A; font-size:11px; margin-top:36px; }
</style>
</head>
<body>
""");
        sb.Append($"<h1>LINUX EXPLODE</h1><div class=\"sub\">Relatório do sistema · {E(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))} · v{Versao}</div>");

        sb.Append("<h2>Sistema</h2><div class=\"grelha\"><div class=\"cartao\"><table>");
        sb.Append($"<tr><td>Distribuição</td><td>{E(s.So)}</td></tr>");
        sb.Append($"<tr><td>Kernel</td><td>{E(s.Kernel)}</td></tr>");
        sb.Append($"<tr><td>Arquitetura</td><td>{E(s.Arquitetura)}</td></tr>");
        sb.Append($"<tr><td>Máquina</td><td>{E(s.Maquina)}</td></tr>");
        sb.Append($"<tr><td>Placa-mãe</td><td>{E(s.PlacaMae)}</td></tr>");
        sb.Append($"<tr><td>BIOS</td><td>{E(s.Bios)}</td></tr>");
        sb.Append($"<tr><td>Ligado há</td><td>{E(s.Uptime)}</td></tr>");
        sb.Append("</table></div><div class=\"cartao\"><table>");
        sb.Append($"<tr><td>CPU</td><td>{E(s.CpuModelo)}</td></tr>");
        sb.Append($"<tr><td>Núcleos / threads</td><td>{s.NucleosFisicos} / {s.Threads}</td></tr>");
        sb.Append($"<tr><td>Frequência</td><td>{E(s.CpuFreqMinAtual)} – {E(s.CpuFreqPico)} agora · média {E(s.CpuFreqAtual)} · turbo {E(s.CpuFreqMax)}</td></tr>");
        sb.Append($"<tr><td>Cache L3</td><td>{E(s.CpuCache)}</td></tr>");
        sb.Append($"<tr><td>RAM</td><td>{E(Util.Bytes(s.RamTotal))}</td></tr>");
        sb.Append($"<tr><td>RAM disponível</td><td>{E(Util.Bytes(s.RamLivre))}</td></tr>");
        sb.Append($"<tr><td>Swap</td><td>{E(Util.Bytes(s.SwapUsada))} de {E(Util.Bytes(s.SwapTotal))}</td></tr>");
        sb.Append("</table></div></div>");

        if (s.Gpus.Count > 0)
        {
            var freqGpu = FrequenciaGpu.Ler(s.Gpus).ToDictionary(x => x.Pci, StringComparer.Ordinal);
            sb.Append("<h2>Placas de vídeo</h2><div class=\"grelha\">");
            foreach (var g in s.Gpus)
            {
                sb.Append("<div class=\"cartao\"><table>");
                sb.Append($"<tr><td>Modelo</td><td>{E(g.Nome)}</td></tr>");
                sb.Append($"<tr><td>PCI</td><td>{E(g.Pci)}</td></tr>");
                sb.Append($"<tr><td>Driver</td><td>{E(g.Driver)}</td></tr>");
                if (freqGpu.TryGetValue(g.Pci, out var fg))
                    sb.Append($"<tr><td>Relógio</td><td>núcleo {E(FrequenciaGpu.Mhz(fg.NucleoHz))} (máx {E(FrequenciaGpu.Mhz(fg.NucleoMaxHz))})" +
                              (fg.MemoriaMaxHz > 0
                                  ? $" · memória {E(FrequenciaGpu.Mhz(fg.MemoriaHz))} (máx {E(FrequenciaGpu.Mhz(fg.MemoriaMaxHz))})"
                                  : "") + "</td></tr>");
                if (g.VramTotal is long vt)
                    sb.Append($"<tr><td>VRAM</td><td>{E(Util.Bytes(g.VramUsada ?? 0))} de {E(Util.Bytes(vt))}</td></tr>");
                if (g.Temp is not null) sb.Append($"<tr><td>Temperatura</td><td>{E(g.Temp)}</td></tr>");
                sb.Append("</table></div>");
            }
            sb.Append("</div>");
        }

        if (discos.Count > 0)
        {
            sb.Append("<h2>Discos</h2><div class=\"grelha\">");
            foreach (var d in discos)
            {
                sb.Append("<div class=\"cartao\"><table>");
                sb.Append($"<tr><td>Dispositivo</td><td>/dev/{E(d.Dev)}</td></tr>");
                sb.Append($"<tr><td>Modelo</td><td>{E(d.Modelo)}</td></tr>");
                sb.Append($"<tr><td>Tamanho</td><td>{E(Util.Bytes(d.Tamanho))}</td></tr>");
                sb.Append($"<tr><td>Tipo</td><td>{(d.Ssd ? "SSD" : "HDD")}</td></tr>");
                sb.Append($"<tr><td>Saúde</td><td>{E(d.Smart.Veredito)}{(d.Smart.Temperatura is null ? "" : " · " + E(d.Smart.Temperatura))}</td></tr>");
                if (d.Smart.Nota is not null) sb.Append($"<tr><td>Nota</td><td>{E(d.Smart.Nota)}</td></tr>");
                foreach (var p in d.Particoes)
                    sb.Append($"<tr><td>{E(p.Nome)}</td><td>{E(p.Montagem)} · {E(p.Fs)} · {E(Util.Bytes(p.Livre))} livres de {E(Util.Bytes(p.Total))}</td></tr>");
                sb.Append("</table></div>");
            }
            sb.Append("</div>");
        }

        if (sensores.Count > 0)
        {
            sb.Append("<h2>Sensores</h2><div class=\"grelha\">");
            foreach (var grupo in sensores.GroupBy(x => x.Chip))
            {
                sb.Append("<div class=\"cartao\"><table>");
                sb.Append($"<tr><td colspan=\"2\" style=\"color:#F2F2F2\">{E(grupo.Key)}</td></tr>");
                foreach (var x in grupo)
                    sb.Append($"<tr><td>{E(x.Nome)}</td><td>{E(x.ValorTexto)}</td></tr>");
                sb.Append("</table></div>");
            }
            sb.Append("</div>");
        }

        sb.Append($"<div class=\"pe\">Gerado por LINUX EXPLODE v{Versao} — hardware lido de /sys, /proc e DMI.</div>");
        sb.Append("</body></html>");
        return sb.ToString();
    }
}

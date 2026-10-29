using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LinuxExplode.Core;

/// <summary>Um stress guardado com a data: o relatório mostra quando foi medido, não só o número.</summary>
public sealed record StressGuardado(DateTime Quando, ResultadoStress Resultado);

/// <summary>Uma série de ciclos guardada com a data.</summary>
public sealed record CiclosGuardado(DateTime Quando, ResultadoCiclos Resultado);

/// <summary>Um enchimento de VRAM guardado com a data.</summary>
public sealed record VramGuardado(DateTime Quando, ResultadoVram Resultado);

/// <summary>
/// Tudo o que o relatório completo junta: o sistema (lido à hora), o histórico de benchmarks, o último
/// stress/ciclos/VRAM medidos e a última sessão gravada. Os dados entram por parâmetro para os testes
/// poderem compor cenários sem depender do disco desta máquina.
/// </summary>
public sealed record DadosRelatorio(
    DateTime Quando,
    IReadOnlyList<RankingLocal> Ranking,
    IReadOnlyList<RegistoBench> Historico,
    StressGuardado? Stress,
    CiclosGuardado? Ciclos,
    VramGuardado? Vram,
    IReadOnlyList<AmostraSessao> Sessao,
    ResumoSessao? ResumoSessao,
    string? SessaoCaminho);

/// <summary>
/// Relatório único em HTML: sistema, sensores, discos, benchmarks, stress, ciclos, VRAM e a última
/// sessão gravada, num ficheiro autónomo (CSS e gráficos SVG embutidos, sem rede). Os gráficos são
/// desenhados a partir das amostras medidas — não há dados de exemplo.
/// </summary>
public static class RelatorioCompleto
{
    // ---------------------------------------------------------------- persistência do último stress

    public static string CaminhoStress => Path.Combine(Historico.Pasta, "ultimo-stress.json");
    public static string CaminhoCiclos => Path.Combine(Historico.Pasta, "ultimo-ciclos.json");
    public static string CaminhoVram => Path.Combine(Historico.Pasta, "ultimo-vram.json");

    public static void GuardarUltimoStress(ResultadoStress r) => Gravar(CaminhoStress, new StressGuardado(DateTime.Now, r));
    public static void GuardarUltimosCiclos(ResultadoCiclos r) => Gravar(CaminhoCiclos, new CiclosGuardado(DateTime.Now, r));
    public static void GuardarUltimoVram(ResultadoVram r) => Gravar(CaminhoVram, new VramGuardado(DateTime.Now, r));

    public static StressGuardado? LerUltimoStress() => Ler<StressGuardado>(CaminhoStress);
    public static CiclosGuardado? LerUltimosCiclos() => Ler<CiclosGuardado>(CaminhoCiclos);
    public static VramGuardado? LerUltimoVram() => Ler<VramGuardado>(CaminhoVram);

    private static void Gravar<T>(string caminho, T valor)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
            File.WriteAllText(caminho, JsonSerializer.Serialize(valor, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static T? Ler<T>(string caminho) where T : class
    {
        try
        {
            if (!File.Exists(caminho)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(caminho));
        }
        catch { return null; }
    }

    // ---------------------------------------------------------------- sessões gravadas

    /// <summary>Lê a sessão gravada mais recente de <c>sessoes/</c>; sem ficheiros devolve vazio.</summary>
    public static (IReadOnlyList<AmostraSessao> Amostras, ResumoSessao? Resumo, string? Caminho) LerUltimaSessao()
    {
        try
        {
            var pasta = Path.Combine(Historico.Pasta, "sessoes");
            if (!Directory.Exists(pasta)) return (Array.Empty<AmostraSessao>(), null, null);

            var ficheiro = Directory.GetFiles(pasta, "sessao-*.csv")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .FirstOrDefault();
            if (ficheiro is null) return (Array.Empty<AmostraSessao>(), null, null);

            return LerSessao(ficheiro);
        }
        catch { return (Array.Empty<AmostraSessao>(), null, null); }
    }

    /// <summary>
    /// Lê um CSV de sessão (separador `;`, ponto decimal, célula vazia = leitura inexistente). A mesma
    /// regra do gravador: o que falta fica null, nunca zero.
    /// </summary>
    public static (IReadOnlyList<AmostraSessao> Amostras, ResumoSessao? Resumo, string? Caminho) LerSessao(string caminho)
    {
        try
        {
            if (!File.Exists(caminho)) return (Array.Empty<AmostraSessao>(), null, caminho);

            var amostras = new List<AmostraSessao>();
            foreach (var linha in File.ReadAllLines(caminho).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;
                var c = linha.Split(';');
                if (c.Length < 5) continue;

                if (!DateTime.TryParseExact(c[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var quando))
                    continue;

                amostras.Add(new AmostraSessao(quando, N(c[1]), N(c[2]), N(c[3]), N(c[4])));
            }

            if (amostras.Count == 0) return (amostras, null, caminho);

            var duracao = amostras.Count > 1 ? amostras[^1].Quando - amostras[0].Quando : TimeSpan.Zero;
            var resumo = new ResumoSessao(
                duracao, amostras.Count,
                ResumoSessao.Calcular(amostras.Select(a => a.CpuC)),
                ResumoSessao.Calcular(amostras.Select(a => a.GpuC)),
                ResumoSessao.Calcular(amostras.Select(a => a.Watts)),
                ResumoSessao.Calcular(amostras.Select(a => a.FreqMhz)));

            return (amostras, resumo, caminho);

            static double? N(string s) =>
                double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        catch { return (Array.Empty<AmostraSessao>(), null, caminho); }
    }

    /// <summary>Recolhe tudo o que o relatório precisa do estado real da aplicação.</summary>
    public static DadosRelatorio Recolher()
    {
        var (amostras, resumo, caminho) = LerUltimaSessao();
        return new DadosRelatorio(DateTime.Now, Historico.Rankings(), Historico.Todos(),
            LerUltimoStress(), LerUltimosCiclos(), LerUltimoVram(),
            amostras, resumo, caminho);
    }

    // ---------------------------------------------------------------- HTML

    public static string GerarHtml(Sistema s, List<Sensor> sensores, List<DiscoInfo> discos, DadosRelatorio d)
    {
        static string E(string? t) => Esc(t);
        var sb = new StringBuilder();

        sb.Append("""
<!doctype html>
<html lang="pt-PT">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>LINUX EXPLODE — Relatório completo</title>
<style>
  :root { color-scheme: dark; }
  * { box-sizing: border-box; }
  body { margin:0; padding:40px; background:#0A0A0A; color:#F2F2F2;
         font-family: Inter, "Noto Sans", -apple-system, sans-serif; }
  h1 { font-size:22px; letter-spacing:4px; margin:0 0 4px; }
  h2 { font-size:12px; letter-spacing:2px; color:#8C8C8C; margin:34px 0 10px;
       text-transform:uppercase; font-weight:600; }
  .sub { color:#8C8C8C; font-size:13px; margin-bottom:26px; }
  .grelha { display:grid; grid-template-columns:repeat(auto-fit,minmax(300px,1fr)); gap:14px; }
  .cartao { background:#111; border:1px solid #202020; border-radius:12px; padding:16px 18px; }
  .destaque { background:#111; border:1px solid #202020; border-radius:12px; padding:14px 16px; }
  .destaque .num { font-size:20px; font-weight:600; }
  .destaque .rot { color:#8C8C8C; font-size:11px; letter-spacing:1.5px; text-transform:uppercase; }
  table { width:100%; border-collapse:collapse; font-size:13px; }
  td, th { padding:7px 0; border-bottom:1px solid #1B1B1B; vertical-align:top; text-align:left; }
  th { padding-right:14px; color:#8C8C8C; font-weight:600; }
  td:first-child { color:#8C8C8C; width:44%; }
  td:last-child, .num { font-family:"JetBrains Mono", ui-monospace, monospace; }
  tr:last-child td { border-bottom:none; }
  .largo td:first-child { width:auto; }
  .mono { font-family:"JetBrains Mono", ui-monospace, monospace; font-size:12px; }
  .nota { color:#8C8C8C; font-size:12px; margin-top:8px; }
  .aviso { color:#E8B84A; }
  .barra { background:#1B1B1B; border-radius:4px; height:8px; overflow:hidden; margin:4px 0 10px; }
  .barra > span { display:block; height:100%; background:#9BE8A8; }
  svg { display:block; width:100%; height:auto; margin-top:6px; }
  .pe { color:#4A4A4A; font-size:11px; margin-top:36px; }
</style>
</head>
<body>
""");

        sb.Append($"<h1>LINUX EXPLODE</h1><div class=\"sub\">Relatório completo · " +
                  $"{E(d.Quando.ToString("dd/MM/yyyy HH:mm"))} · {E(s.Hostname)} · v{E(Relatorio.Versao)}" +
                  $" · gerado a partir de /sys, /proc, DMI e das medições guardadas</div>");

        // ---------------------------------------------------------------- destaques
        long usada = s.RamTotal - s.RamLivre;
        var gpu = s.Gpus.FirstOrDefault();
        int saudaveis = discos.Count(x => x.Smart.Veredito == "Bom");
        var ultimoStress = d.Stress?.Resultado;

        sb.Append("<div class=\"grelha\">");
        sb.Append($"<div class=\"destaque\"><div class=\"rot\">Processador</div><div class=\"num\">{E(s.CpuModelo)}</div>" +
                  $"<div class=\"nota\">{s.NucleosFisicos} núcleos · {s.Threads} threads · {E(s.CpuFreqAtual)}</div></div>");
        sb.Append($"<div class=\"destaque\"><div class=\"rot\">Memória</div><div class=\"num\">{E(Util.Bytes(s.RamTotal))}</div>" +
                  $"<div class=\"nota\">{E(Util.Bytes(usada))} em uso · swap {E(Util.Bytes(s.SwapUsada))} de {E(Util.Bytes(s.SwapTotal))}</div></div>");
        if (gpu is not null)
            sb.Append($"<div class=\"destaque\"><div class=\"rot\">Placa de vídeo</div><div class=\"num\">{E(Curto(gpu.Nome))}</div>" +
                      $"<div class=\"nota\">{E(gpu.Temp ?? "—")} · {(gpu.VramTotal is long vt ? E($"{Util.Bytes(gpu.VramUsada ?? 0)} de {Util.Bytes(vt)}") : "VRAM —")}</div></div>");
        sb.Append($"<div class=\"destaque\"><div class=\"rot\">Discos</div><div class=\"num\">{discos.Count}</div>" +
                  $"<div class=\"nota\">{saudaveis} com veredito Bom · {discos.Count - saudaveis} a ver</div></div>");
        sb.Append($"<div class=\"destaque\"><div class=\"rot\">Benchmarks no histórico</div><div class=\"num\">{d.Historico.Count}</div>" +
                  $"<div class=\"nota\">{d.Ranking.Count} alvo(s) no ranking</div></div>");
        sb.Append($"<div class=\"destaque\"><div class=\"rot\">Último stress</div><div class=\"num\">" +
                  $"{(ultimoStress is null ? "sem registo" : E(ultimoStress.Resumo))}</div>" +
                  $"<div class=\"nota\">{(ultimoStress is null ? "corre a aba STRESS" : E($"{d.Stress!.Quando:dd/MM/yyyy HH:mm} · pico CPU {ultimoStress.PicoCpuC:0} °C"))}</div></div>");
        sb.Append("</div>");

        // ---------------------------------------------------------------- sistema
        sb.Append("<h2>Sistema</h2><div class=\"grelha\"><div class=\"cartao\"><table>");
        Linha(sb, "Distribuição", s.So);
        Linha(sb, "Kernel", s.Kernel);
        Linha(sb, "Arquitetura", s.Arquitetura);
        Linha(sb, "Máquina", s.Maquina);
        Linha(sb, "Placa-mãe", s.PlacaMae);
        Linha(sb, "BIOS", s.Bios);
        Linha(sb, "Ligado há", s.Uptime);
        sb.Append("</table></div><div class=\"cartao\"><table>");
        Linha(sb, "CPU", s.CpuModelo);
        Linha(sb, "Núcleos / threads", $"{s.NucleosFisicos} / {s.Threads}");
        Linha(sb, "Frequência", $"{s.CpuFreqMinAtual} – {s.CpuFreqPico} agora · média {s.CpuFreqAtual} · turbo {s.CpuFreqMax}");
        Linha(sb, "Cache L3", s.CpuCache);
        Linha(sb, "RAM", $"{Util.Bytes(s.RamTotal)} total · {Util.Bytes(s.RamLivre)} disponível");
        Linha(sb, "Swap", s.SwapTotal > 0 ? $"{Util.Bytes(s.SwapUsada)} de {Util.Bytes(s.SwapTotal)}" : "sem swap");
        sb.Append("</table></div></div>");

        // ---------------------------------------------------------------- GPUs
        if (s.Gpus.Count > 0)
        {
            var freqGpu = FrequenciaGpu.Ler(s.Gpus).ToDictionary(x => x.Pci, StringComparer.Ordinal);
            sb.Append("<h2>Placas de vídeo</h2><div class=\"grelha\">");
            foreach (var g in s.Gpus)
            {
                sb.Append("<div class=\"cartao\"><table>");
                Linha(sb, "Modelo", g.Nome);
                Linha(sb, "PCI", g.Pci);
                Linha(sb, "Driver", g.Driver);
                if (freqGpu.TryGetValue(g.Pci, out var fg))
                    Linha(sb, "Relógio", $"núcleo {FrequenciaGpu.Mhz(fg.NucleoHz)} (máx {FrequenciaGpu.Mhz(fg.NucleoMaxHz)})" +
                                         (fg.MemoriaMaxHz > 0 ? $" · memória {FrequenciaGpu.Mhz(fg.MemoriaHz)} (máx {FrequenciaGpu.Mhz(fg.MemoriaMaxHz)})" : ""));
                if (g.VramTotal is long vt)
                    Linha(sb, "VRAM", $"{Util.Bytes(g.VramUsada ?? 0)} de {Util.Bytes(vt)}");
                if (g.Temp is not null) Linha(sb, "Temperatura", g.Temp);
                sb.Append("</table></div>");
            }
            sb.Append("</div>");
        }

        // ---------------------------------------------------------------- discos
        if (discos.Count > 0)
        {
            sb.Append("<h2>Discos e saúde</h2><div class=\"grelha\">");
            foreach (var disco in discos)
            {
                sb.Append("<div class=\"cartao\"><table>");
                Linha(sb, "Dispositivo", $"/dev/{disco.Dev}");
                Linha(sb, "Modelo", disco.Modelo);
                Linha(sb, "Tamanho", $"{Util.Bytes(disco.Tamanho)} · {(disco.Ssd ? "SSD" : "HDD")}");
                Linha(sb, "Saúde", disco.Smart.Veredito + (disco.Smart.Temperatura is null ? "" : " · " + disco.Smart.Temperatura));
                if (disco.Smart.HorasLigado is long h) Linha(sb, "Horas ligado", $"{h:N0} h ({h / 24.0:N0} dias)");
                if (disco.Smart.Desgaste is double desgaste) Linha(sb, "Desgaste", $"{desgaste:0} %");
                if (disco.Smart.SetoresRealocados is long sr) Linha(sb, "Setores realocados", $"{sr:N0}");
                Linha(sb, "Auto-teste", disco.AutoTeste.Estado +
                                          (disco.AutoTeste.Percentagem is int pct ? $" ({pct} %)" : ""));
                foreach (var t in disco.AutoTeste.Historico.Take(3))
                    Linha(sb, "  " + t.Tipo, t.Resultado + (t.HorasLigado is long ht ? $" · {ht:N0} h" : ""));
                foreach (var p in disco.Particoes)
                    Linha(sb, p.Nome, $"{p.Montagem} · {p.Fs} · {Util.Bytes(p.Livre)} livres de {Util.Bytes(p.Total)}");
                sb.Append("</table></div>");
            }
            sb.Append("</div>");
        }

        // ---------------------------------------------------------------- sensores
        if (sensores.Count > 0)
        {
            sb.Append("<h2>Sensores</h2><div class=\"grelha\">");
            foreach (var grupo in sensores.GroupBy(x => x.Chip))
            {
                sb.Append("<div class=\"cartao\"><table>");
                sb.Append($"<tr><td colspan=\"2\" style=\"color:#F2F2F2\">{E(grupo.Key)}</td></tr>");
                foreach (var x in grupo) Linha(sb, x.Nome, x.ValorTexto);
                sb.Append("</table></div>");
            }
            sb.Append("</div>");
        }

        // ---------------------------------------------------------------- benchmarks
        sb.Append("<h2>Benchmarks — ranking local</h2>");
        if (d.Ranking.Count == 0)
        {
            sb.Append("<div class=\"cartao nota\">Sem corridas registadas. O ranking aparece depois do primeiro " +
                      "benchmark (aba EXPLODE BENCH ou <span class=\"mono\">--cli bench …</span>).</div>");
        }
        else
        {
            sb.Append("<div class=\"cartao\"><table class=\"largo\">");
            sb.Append("<tr><th>Tipo</th><th>Alvo</th><th>Mediana</th><th>Melhor</th><th>Última</th><th>Variação</th><th>Corridas</th></tr>");
            foreach (var r in d.Ranking)
                sb.Append($"<tr><td>{E(r.Tipo)}</td><td>{E(r.Alvo)}</td><td>{r.Mediana:N0}</td><td>{r.Melhor:N0}</td>" +
                          $"<td>{r.Ultima:N0}</td><td>{(r.Instavel ? $"<span class=\"aviso\">{r.VariacaoPct:0.0} %</span>" : $"{r.VariacaoPct:0.0} %")}</td>" +
                          $"<td>{r.Corridas}</td></tr>");
            sb.Append("</table><div class=\"nota\">Mediana das últimas " + Historico.CorridasRanking +
                      " corridas; aviso acima de " + RankingLocal.ToleranciaPct.ToString("0") + " % de variação.</div></div>");

            // barras por tipo (cada tipo tem a sua escala; comparar tipos diferentes não faria sentido)
            sb.Append("<div class=\"cartao\"><div class=\"rot\" style=\"color:#8C8C8C;font-size:11px;letter-spacing:1.5px\">" +
                      "MEDIANAS, RELATIVAS AO MELHOR DE CADA TIPO</div>");
            foreach (var tipo in d.Ranking.GroupBy(r => r.Tipo).OrderBy(g => g.Key))
            {
                double max = tipo.Max(r => r.Mediana);
                sb.Append($"<div class=\"nota\" style=\"margin-top:12px\">{E(tipo.Key)}</div>");
                foreach (var r in tipo)
                {
                    int largura = LarguraBarra(r.Mediana, max);
                    sb.Append($"<div class=\"mono\">{E(r.Alvo)} — {r.Mediana:N0}</div>" +
                              $"<div class=\"barra\"><span style=\"width:{largura}%\"></span></div>");
                }
            }
            sb.Append("</div>");

            sb.Append("<div class=\"cartao\"><table class=\"largo\">");
            sb.Append("<tr><th>Quando</th><th>Tipo</th><th>Alvo</th><th>Nota</th><th>Detalhe</th></tr>");
            foreach (var r in d.Historico.OrderByDescending(x => x.Quando).Take(15))
                sb.Append($"<tr><td class=\"mono\">{E(r.Quando.ToString("dd/MM/yyyy HH:mm"))}</td><td>{E(r.Tipo)}</td>" +
                          $"<td>{E(r.Alvo)}</td><td>{r.Nota:N0}</td><td class=\"nota\">{E(r.Detalhe)}</td></tr>");
            sb.Append("</table><div class=\"nota\">Últimas 15 corridas de " + d.Historico.Count + " registadas.</div></div>");
        }

        // ---------------------------------------------------------------- stress
        sb.Append("<h2>Stress, ciclos e VRAM</h2>");
        if (d.Stress is null && d.Ciclos is null && d.Vram is null)
        {
            sb.Append("<div class=\"cartao nota\">Sem stress registado. Corre um teste na aba STRESS — o último " +
                      "resultado de cada tipo fica aqui para o relatório.</div>");
        }

        if (d.Stress is { } es)
        {
            var r = es.Resultado;
            sb.Append("<div class=\"cartao\"><table>");
            Linha(sb, "Quando", es.Quando.ToString("dd/MM/yyyy HH:mm"));
            Linha(sb, "Alvo", NomeAlvo(r.Alvo));
            Linha(sb, "Resultado", r.Resumo);
            Linha(sb, "Picos", $"CPU {r.PicoCpuC:0} °C · GPU {r.PicoGpuC:0} °C");
            Linha(sb, "Energia · duração", $"{r.EnergiaKwh * 1000:0.0} Wh em {r.Segundos:0} s");
            sb.Append("</table>");
            foreach (var l in r.Linhas) sb.Append($"<div class=\"mono nota\">· {E(l)}</div>");
            sb.Append("</div>");
        }

        if (d.Ciclos is { } ec)
        {
            var r = ec.Resultado;
            sb.Append("<div class=\"cartao\"><table>");
            Linha(sb, "Ciclos", ec.Quando.ToString("dd/MM/yyyy HH:mm"));
            Linha(sb, "Resultado", r.Resumo);
            Linha(sb, "Picos", $"CPU {r.PicoCpuC:0} °C · GPU {r.PicoGpuC:0} °C · {r.EnergiaKwh * 1000:0.0} Wh");
            sb.Append("</table>");
            if (r.Agregado.Count > 0)
            {
                sb.Append("<table class=\"largo\" style=\"margin-top:8px\">" +
                          "<tr><th>Métrica</th><th>Mediana</th><th>Mínimo</th><th>Máximo</th><th>Variação</th><th>Amostras</th></tr>");
                foreach (var m in r.Agregado)
                    sb.Append($"<tr><td>{E(m.Rotulo)}</td><td>{m.Mediana:N1}</td><td>{m.Minimo:N1}</td><td>{m.Maximo:N1}</td>" +
                              $"<td>{(m.Contador ? "total" : m.Amostras >= 3 ? $"{m.VariacaoPct:0.0} %" : "amostra pequena")}</td><td>{m.Amostras}</td></tr>");
                sb.Append("</table>");
            }
            sb.Append("</div>");
        }

        if (d.Vram is { } ev)
        {
            var r = ev.Resultado;
            sb.Append("<div class=\"cartao\"><table>");
            Linha(sb, "VRAM", ev.Quando.ToString("dd/MM/yyyy HH:mm"));
            Linha(sb, "GPU", r.Gpu);
            Linha(sb, "Alocado", $"{Util.Bytes(r.TotalAlocado)} em {r.Blocos} bloco(s) de {Util.Bytes(r.BlocoBytes)}");
            Linha(sb, "Verificação", $"{r.BlocosVerificados} bloco(s) relidos · {r.ErrosVerificacao} diferença(s)");
            if (r.SysfsPico is long pico) Linha(sb, "Pico em /sys", Util.Bytes(pico));
            sb.Append("</table>");
            foreach (var l in r.Linhas) sb.Append($"<div class=\"mono nota\">· {E(l)}</div>");
            sb.Append("</div>");
        }

        // ---------------------------------------------------------------- sessão gravada
        sb.Append("<h2>Sessão gravada</h2>");
        if (d.ResumoSessao is null || d.Sessao.Count == 0)
        {
            sb.Append("<div class=\"cartao nota\">Nenhuma sessão gravada. Grava uma com " +
                      "<span class=\"mono\">--cli gravar 30</span> ou no cartão <em>Sessão</em> da aba Sensores.</div>");
        }
        else
        {
            var r = d.ResumoSessao;
            sb.Append("<div class=\"cartao\"><table>");
            Linha(sb, "Ficheiro", d.SessaoCaminho ?? "—");
            Linha(sb, "Duração · amostras", $"{r.Duracao.TotalSeconds:0} s · {r.Amostras} amostra(s)");
            Linha(sb, "CPU", Intervalo(r.Cpu, "°C"));
            Linha(sb, "GPU", Intervalo(r.Gpu, "°C"));
            Linha(sb, "Potência", Intervalo(r.Watts, "W"));
            Linha(sb, "Frequência", Intervalo(r.Freq, "MHz"));
            sb.Append("</table>");
            sb.Append("<div class=\"grelha\" style=\"margin-top:12px\">");
            sb.Append(Grafico("CPU", d.Sessao.Select(a => a.CpuC), "°C"));
            sb.Append(Grafico("GPU", d.Sessao.Select(a => a.GpuC), "°C"));
            sb.Append(Grafico("Potência", d.Sessao.Select(a => a.Watts), "W"));
            sb.Append(Grafico("Frequência", d.Sessao.Select(a => a.FreqMhz), "MHz"));
            sb.Append("</div></div>");
        }

        sb.Append($"<div class=\"pe\">Gerado por LINUX EXPLODE v{E(Relatorio.Versao)} — " +
                  "hardware lido de /sys, /proc e DMI; benchmarks e sessões são medições guardadas nesta máquina.</div>");
        sb.Append("</body></html>");
        return sb.ToString();

        void Linha(StringBuilder alvo, string chave, string valor)
            => alvo.Append($"<tr><td>{E(chave)}</td><td>{E(valor)}</td></tr>");
    }

    /// <summary>O nome do alvo como a interface o escreve (o enum ToString daria "Cpu").</summary>
    public static string NomeAlvo(AlvoStress alvo) => alvo switch
    {
        AlvoStress.Cpu => "CPU",
        AlvoStress.Ram => "RAM",
        AlvoStress.Gpu => "GPU",
        AlvoStress.Disco => "disco",
        _ => "tudo (burn-in)"
    };

    /// <summary>
    /// Nome para o cartão de destaque. O pci.ids traz o fabricante por extenso e a lista completa de
    /// modelos entre parênteses rectos ("Advanced Micro Devices, Inc. [AMD/ATI] Navi 22 [Radeon RX
    /// 6700/6700 XT/…]"); fica o fabricante curto e o modelo, e a lista completa continua na secção
    /// das placas. Nada é inventado — só se escolhe o que cabe.
    /// </summary>
    public static string Curto(string? nome, int maximo = 46)
    {
        var t = (nome ?? "").Trim()
            .Replace("Advanced Micro Devices, Inc. [AMD/ATI] ", "AMD ")
            .Replace("Advanced Micro Devices, Inc. [AMD] ", "AMD ")
            .Replace("Intel Corporation ", "Intel ")
            .Replace("NVIDIA Corporation ", "NVIDIA ");

        if (t.Length <= maximo) return t;

        int bracket = t.IndexOf('[');
        if (bracket > 6) return t[..bracket].Trim();

        return t[..maximo].TrimEnd() + "…";
    }

    /// <summary>
    /// Escapa só o que pode partir o HTML (&lt; &gt; &amp; " '). O documento é UTF-8, por isso os acentos
    /// ficam como estão — com <c>WebUtility.HtmlEncode</c> "concluído" virava "conclu&#237;do" e o texto
    /// deixava de ser encontrável.
    /// </summary>
    public static string Esc(string? texto) => (texto ?? "—")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;").Replace("'", "&#39;");

    /// <summary>Percentagem da barra: o valor sobre o máximo, limitada a 100.</summary>
    public static int LarguraBarra(double valor, double maximo)
        => maximo <= 0 ? 0 : (int)Math.Clamp(Math.Round(valor / maximo * 100), 0, 100);

    private static string Intervalo((double? Min, double? Max, double? Media) s, string unidade)
        => s.Min is null || s.Max is null
            ? "sem leitura nesta sessão"
            : $"mín {s.Min:0.0} · máx {s.Max:0.0} · média {s.Media:0.0} {unidade}";

    /// <summary>
    /// Pequeno gráfico SVG de uma série. Sem bibliotecas e sem rede: os pontos são os medidos no CSV.
    /// Uma série sem leituras devolve um cartão que o diz, em vez de uma linha inventada.
    /// </summary>
    public static string Grafico(string rotulo, IEnumerable<double?> serie, string unidade, int largura = 520, int altura = 90)
    {
        var v = serie.Where(x => x is not null).Select(x => x!.Value).ToList();
        if (v.Count == 0)
            return $"<div class=\"cartao\"><div class=\"nota\">{System.Net.WebUtility.HtmlEncode(rotulo)}: sem leituras nesta sessão</div></div>";

        // muitas amostras: fica uma amostra representativa por coluna (o extremo de cada janela)
        const int maximoPontos = 240;
        if (v.Count > maximoPontos)
        {
            int passo = (int)Math.Ceiling(v.Count / (double)maximoPontos);
            var reduzida = new List<double>();
            for (int i = 0; i < v.Count; i += passo)
            {
                var janela = v.Skip(i).Take(passo).ToList();
                reduzida.Add(janela.Max());
            }
            v = reduzida;
        }

        double min = v.Min(), max = v.Max();
        double intervalo = Math.Max(1e-9, max - min);
        const int margem = 8;

        var pontos = new StringBuilder();
        for (int i = 0; i < v.Count; i++)
        {
            double x = margem + (largura - 2.0 * margem) * (v.Count == 1 ? 0 : i / (double)(v.Count - 1));
            double y = altura - margem - (altura - 2.0 * margem) * ((v[i] - min) / intervalo);
            pontos.Append($"{x.ToString("0.0", CultureInfo.InvariantCulture)},{y.ToString("0.0", CultureInfo.InvariantCulture)} ");
        }

        string E(string? t) => Esc(t);
        return $"""
<div class="cartao">
  <div class="nota">{E(rotulo)}</div>
  <svg viewBox="0 0 {largura} {altura}" preserveAspectRatio="none" role="img" aria-label="{E(rotulo)}">
    <polyline points="{pontos.ToString().Trim()}" fill="none" stroke="#9BE8A8" stroke-width="1.6" />
  </svg>
  <div class="mono nota">{min:0.0} – {max:0.0} {E(unidade)} · média {v.Average():0.0} {E(unidade)}</div>
</div>
""";
    }
}

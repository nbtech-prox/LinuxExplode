using System.Globalization;
using LinuxExplode.Core;

namespace LinuxExplode.Tests;

/// <summary>
/// Fase 6 (#19): o relatório completo junta sistema, sensores, discos, benchmarks, stress e a sessão
/// gravada num HTML autónomo. Estes testes travam o que o torna utilizável: as secções existem, o texto
/// do hardware é escapado, não há rede nem script, e o CSV da sessão é lido com a mesma regra do
/// gravador (célula vazia = leitura inexistente).
/// </summary>
public sealed class RelatorioCompletoTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "linuxexplode-relatorio-" + Guid.NewGuid().ToString("N"));
    private readonly string? _dadosAntes = Environment.GetEnvironmentVariable("LINUXEXPLODE_DADOS");

    public RelatorioCompletoTests()
    {
        Directory.CreateDirectory(_pasta);
        Environment.SetEnvironmentVariable("LINUXEXPLODE_DADOS", _pasta);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LINUXEXPLODE_DADOS", _dadosAntes);
        try { Directory.Delete(_pasta, true); } catch { }
    }

    private static DadosRelatorio Vazio() =>
        new(DateTime.Now, Array.Empty<RankingLocal>(), Array.Empty<RegistoBench>(),
            null, null, null, Array.Empty<AmostraSessao>(), null, null);

    [Fact]
    public void Html_tem_as_seccoes_e_nao_depende_da_rede_nem_de_script()
    {
        var html = Relatorio.GerarHtml(Sistema.Recolher(), new List<Sensor>(), new List<DiscoInfo>());

        foreach (var secao in new[] { "Sistema", "Benchmarks", "Stress, ciclos e VRAM", "Sessão gravada" })
            Assert.Contains(secao, html);

        Assert.StartsWith("<!doctype html>", html.TrimStart());
        Assert.EndsWith("</html>", html.TrimEnd());
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public void Texto_do_hardware_e_escapado()
    {
        var sensores = new List<Sensor> { new("<b>chip</b>", "<i>temp</i>", TipoSensor.Temperatura, 50, "°C") };
        var discos = new List<DiscoInfo>
        {
            new("sdz", "<script>alert('x')</script>", 1000, true, "running",
                new List<Particao>(), new SmartInfo(false, "—", null, null, null, null, null, null, null),
                AutoTesteInfo.Indisponivel("sem dados"))
        };

        var html = RelatorioCompleto.GerarHtml(Sistema.Recolher(), sensores, discos, Vazio());

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;alert", html);
        Assert.Contains("&lt;b&gt;chip&lt;/b&gt;", html);
        Assert.DoesNotContain("<i>temp</i>", html);
    }

    [Fact]
    public void Sessao_csv_le_as_amostras_e_mantem_o_vazio_como_null()
    {
        var caminho = Path.Combine(_pasta, "sessao-teste.csv");
        File.WriteAllText(caminho,
            "quando;cpu_c;gpu_c;watts;freq_mhz\n" +
            "2026-10-29 10:00:00;50.5;40;100.2;3200\n" +
            "2026-10-29 10:00:01;52;;101.5;\n" +
            "2026-10-29 10:00:02;;41;102;3300\n");

        var (amostras, resumo, caminhoLido) = RelatorioCompleto.LerSessao(caminho);

        Assert.Equal(3, amostras.Count);
        Assert.Equal(caminho, caminhoLido);
        Assert.Equal(50.5, amostras[0].CpuC);
        Assert.Equal(52.0, amostras[1].CpuC);      // presente na 2.ª amostra
        Assert.Null(amostras[1].GpuC);             // célula vazia = leitura inexistente
        Assert.Null(amostras[2].CpuC);
        Assert.Null(amostras[1].FreqMhz);

        Assert.NotNull(resumo);
        Assert.Equal(2, resumo!.Duracao.TotalSeconds);
        Assert.Equal(50.5, resumo.Cpu.Min);
        Assert.Equal(52.0, resumo.Cpu.Max);
        Assert.Equal(51.25, resumo.Cpu.Media!.Value, 3);
        Assert.Equal(40.0, resumo.Gpu.Min);
        Assert.Equal(41.0, resumo.Gpu.Max);
        Assert.Equal(40.5, resumo.Gpu.Media!.Value, 3);
        Assert.Equal(100.2, resumo.Watts.Min);
        Assert.Equal(102.0, resumo.Watts.Max);
        Assert.Equal(101.2333, resumo.Watts.Media!.Value, 3);
        Assert.Equal(3200.0, resumo.Freq.Min);
        Assert.Equal(3300.0, resumo.Freq.Max);
        Assert.Equal(3250.0, resumo.Freq.Media!.Value, 3);
    }

    [Fact]
    public void Ultimo_stress_guarda_e_volta_a_ler_com_as_metricas()
    {
        var resultado = new ResultadoStress(AlvoStress.Cpu, true, "concluído em 5 s",
            new List<string> { "CPU: 5 000 M/s" }, 61.5, 45.0, 0.0123, 5.0,
            new Dictionary<string, double> { ["cpu_milhoes_s"] = 5000, ["cpu_gflops"] = 80 });

        RelatorioCompleto.GuardarUltimoStress(resultado);
        var lido = RelatorioCompleto.LerUltimoStress();

        Assert.NotNull(lido);
        Assert.Equal("concluído em 5 s", lido!.Resultado.Resumo);
        Assert.Equal(61.5, lido.Resultado.PicoCpuC);
        Assert.Equal(5000, lido.Resultado.Metricas!["cpu_milhoes_s"]);
        Assert.True(File.Exists(Path.Combine(_pasta, "ultimo-stress.json")));
    }

    [Fact]
    public void Html_inclui_o_stress_e_a_serie_de_ciclos_quando_existem()
    {
        var stress = new StressGuardado(DateTime.Now, new ResultadoStress(AlvoStress.Cpu, true,
            "concluído em 5 s", new List<string> { "CPU: 5 000 M/s" }, 61.5, 45, 0.01, 5,
            new Dictionary<string, double> { ["cpu_gflops"] = 80 }));

        var ciclos = new CiclosGuardado(DateTime.Now, new ResultadoCiclos(AlvoStress.Cpu, 3, 3, 3, 0, false, null,
            62, 46, 0.02, 12, new List<ResultadoStress>(),
            new List<MetricaCiclos> { new("cpu_gflops", "CPU (GFLOP/s)", 80, 79, 81, 2.5, 3, false) },
            new List<string>()));

        var dados = Vazio() with { Stress = stress, Ciclos = ciclos };
        var html = RelatorioCompleto.GerarHtml(Sistema.Recolher(), new List<Sensor>(), new List<DiscoInfo>(), dados);

        Assert.Contains("3/3 ciclos completos", html);
        Assert.Contains("CPU (GFLOP/s)", html);
        Assert.Contains("concluído em 5 s", html);
        Assert.Contains("2,5 %", html);      // variação dos ciclos, em pt-PT
    }

    [Fact]
    public void Largura_da_barra_escala_para_o_maximo_e_nao_passa_dos_100()
    {
        Assert.Equal(100, RelatorioCompleto.LarguraBarra(80, 80));
        Assert.Equal(50, RelatorioCompleto.LarguraBarra(40, 80));
        Assert.Equal(0, RelatorioCompleto.LarguraBarra(0, 80));
        Assert.Equal(0, RelatorioCompleto.LarguraBarra(5, 0));
        Assert.Equal(100, RelatorioCompleto.LarguraBarra(120, 80));
    }

    [Fact]
    public void Grafico_svg_tem_os_pontos_e_uma_serie_vazia_e_dita()
    {
        var comDados = RelatorioCompleto.Grafico("CPU", new double?[] { 40, 50, 60 }, "°C");
        Assert.Contains("<polyline", comDados);
        Assert.Contains("40,0 – 60,0 °C", comDados);

        var semDados = RelatorioCompleto.Grafico("GPU", new double?[] { null, null }, "°C");
        Assert.Contains("sem leituras", semDados);
        Assert.DoesNotContain("<svg", semDados);
    }

    [Fact]
    public void Alvos_do_stress_tem_os_nomes_da_interface()
    {
        Assert.Equal("CPU", RelatorioCompleto.NomeAlvo(AlvoStress.Cpu));
        Assert.Equal("RAM", RelatorioCompleto.NomeAlvo(AlvoStress.Ram));
        Assert.Equal("GPU", RelatorioCompleto.NomeAlvo(AlvoStress.Gpu));
        Assert.Equal("disco", RelatorioCompleto.NomeAlvo(AlvoStress.Disco));
        Assert.Equal("tudo (burn-in)", RelatorioCompleto.NomeAlvo(AlvoStress.Tudo));
    }

    [Fact]
    public void Nome_comprido_da_gpu_e_cortado()
    {
        var nome = "Advanced Micro Devices, Inc. [AMD/ATI] Navi 22 [Radeon RX 6700/6700 XT/6750 XT / 6800M/6850M XT]";
        var curto = RelatorioCompleto.Curto(nome, 46);

        Assert.Equal("AMD Navi 22", curto);          // fabricante curto + modelo, sem a lista de modelos
        Assert.True(curto.Length <= 47);
        Assert.Equal("curto", RelatorioCompleto.Curto("curto"));
        Assert.Equal("AMD Navi 22", RelatorioCompleto.Curto("Advanced Micro Devices, Inc. [AMD/ATI] Navi 22"));
    }
}

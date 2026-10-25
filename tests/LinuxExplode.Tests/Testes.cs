using LinuxExplode.Core;

namespace LinuxExplode.Tests;

/// <summary>
/// As regras que a aplicação promete: o histórico não engorda com repetições, a mediana e a variação
/// estão certas, a exportação/importação não perde nem duplica, e a configuração sobrevive a um
/// reinício.
/// </summary>
public sealed class HistoricoTests : IDisposable
{
    private readonly string _pasta;

    public HistoricoTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "linuxexplode-testes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
        Environment.SetEnvironmentVariable("LINUXEXPLODE_DADOS", _pasta);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LINUXEXPLODE_DADOS", null);
        try { Directory.Delete(_pasta, true); } catch { }
    }

    [Fact]
    public void Uma_nota_igual_a_anterior_nao_e_gravada()
    {
        Historico.Registar("cpu", "16 threads", 12_000, "detalhe");
        Historico.Registar("cpu", "16 threads", 12_000, "detalhe");

        Assert.Single(Historico.Todos());
    }

    [Fact]
    public void Notas_diferentes_ficam_todas_guardadas()
    {
        Historico.Registar("cpu", "16 threads", 12_000, "a");
        Historico.Registar("cpu", "16 threads", 12_500, "b");
        Historico.Registar("cpu", "16 threads", 11_800, "c");

        Assert.Equal(3, Historico.Todos().Count);
    }

    [Fact]
    public void Estatisticas_dao_melhor_mediana_e_contagem()
    {
        foreach (double nota in new double[] { 10, 20, 30 }) Historico.Registar("gpu", "RX", nota, "d");

        var (melhor, mediana, ultima, n) = Historico.Estatisticas("gpu", "RX");

        Assert.Equal(30, melhor);
        Assert.Equal(20, mediana);
        Assert.Equal(30, ultima);
        Assert.Equal(3, n);
    }

    [Fact]
    public void Comparacao_usa_a_mediana_e_a_percentagem()
    {
        foreach (double nota in new double[] { 100, 200 }) Historico.Registar("cpu", "8 threads", nota, "d");

        var texto = Historico.Comparacao("cpu", "8 threads", 250);

        Assert.NotNull(texto);
        Assert.Contains("mediana", texto);
        Assert.Contains("+66,7 %", texto);
    }

    [Fact]
    public void Sem_historico_a_comparacao_e_nula()
    {
        Assert.Null(Historico.Comparacao("cpu", "2 threads", 100));
    }

    [Fact]
    public void Exportar_csv_e_importar_json_preservam_os_resultados()
    {
        Historico.Registar("ram", "512 MB", 15_000, "escrita");

        string csv = Path.Combine(_pasta, "saida.csv");
        Historico.ExportarCsv(csv);
        Assert.True(File.Exists(csv));
        Assert.StartsWith("quando;tipo;alvo;nota", File.ReadAllLines(csv)[0]);

        string json = Path.Combine(_pasta, "saida.json");
        Historico.ExportarJson(json);

        // apaga o histórico e importa de novo: uma entrada deve voltar
        File.Delete(Historico.Caminho);
        int novos = Historico.Importar(json);

        Assert.Equal(1, novos);
        Assert.Single(Historico.Todos());
    }

    [Fact]
    public void Importar_nao_duplica_o_que_ja_existe()
    {
        Historico.Registar("disco", "seq", 1_000, "d");
        string json = Path.Combine(_pasta, "dup.json");
        Historico.ExportarJson(json);

        Assert.Equal(0, Historico.Importar(json));
    }
}

public sealed class ConfigTests : IDisposable
{
    private readonly string _pasta;

    public ConfigTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "linuxexplode-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
        Environment.SetEnvironmentVariable("LINUXEXPLODE_CONFIG", _pasta);
        Config.Recarregar();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LINUXEXPLODE_CONFIG", null);
        Config.Recarregar();
        try { Directory.Delete(_pasta, true); } catch { }
    }

    [Fact]
    public void Valores_por_omissao_sao_razoaveis()
    {
        Assert.Equal(0.20, Config.Atual.TarifaEurKwh);
        Assert.Equal(90, Config.Atual.LimiteCpuAviso);
        Assert.Equal(10, Config.Atual.EspacoMinimoGb);
        Assert.False(Config.Atual.Notificacoes);
    }

    [Fact]
    public void O_que_e_gravado_e_lido_de_novo()
    {
        Config.Atual.TarifaEurKwh = 0.31;
        Config.Atual.Tema = "claro";
        Config.Atual.Escala = 1.15;
        Config.Atual.Notificacoes = true;
        Config.Atual.Guardar();

        Config.Recarregar();

        Assert.Equal(0.31, Config.Atual.TarifaEurKwh);
        Assert.Equal("claro", Config.Atual.Tema);
        Assert.Equal(1.15, Config.Atual.Escala);
        Assert.True(Config.Atual.Notificacoes);
    }
}

public sealed class FormatacaoTests
{
    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    public void Bytes_usa_unidades_legiveis(long valor, string esperado)
    {
        Assert.Equal(esperado, Util.Bytes(valor));
    }

    [Fact]
    public void Gb_arredonda_a_uma_casa()
    {
        Assert.Equal("1,5 GB", Util.Gb(1610612736));
    }
}

public sealed class EnergiaTests
{
    [Fact]
    public void Medidor_integra_potencia_no_tempo()
    {
        var medidor = new MedidorEnergia();

        medidor.Amostrar(100);            // primeira amostra: só referência
        Thread.Sleep(220);
        medidor.Amostrar(100);
        Thread.Sleep(220);
        medidor.Amostrar(100);

        var custo = medidor.Calcular(0.20);

        Assert.True(custo.Wh > 0, "a energia acumulada devia ser positiva");
        Assert.InRange(custo.MediaW, 80, 120);
        Assert.NotNull(custo.EurDia);
    }
}

public sealed class AlertasTests : IDisposable
{
    private readonly string _pasta;

    public AlertasTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "linuxexplode-alertas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
        Environment.SetEnvironmentVariable("LINUXEXPLODE_CONFIG", _pasta);
        Config.Recarregar();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LINUXEXPLODE_CONFIG", null);
        Config.Recarregar();
        try { Directory.Delete(_pasta, true); } catch { }
    }

    [Fact]
    public void Limite_de_espaco_absurdo_gera_alerta_na_particao_raiz()
    {
        Config.Atual.EspacoMinimoGb = 100_000;

        var alertas = Alertas.Avaliar();

        Assert.Contains(alertas, a => a.Chave.StartsWith("disco-"));
    }

    [Fact]
    public void Sem_limites_apertados_nao_ha_alertas_de_disco()
    {
        Config.Atual.EspacoMinimoGb = 0.001;
        Config.Atual.LimiteCpuAviso = 110;
        Config.Atual.LimiteGpuAviso = 110;

        var alertas = Alertas.Avaliar();

        Assert.DoesNotContain(alertas, a => a.Chave.StartsWith("disco-"));
    }
}

public sealed class StressTests
{
    [Fact]
    public void Carga_de_cpu_conta_unidades_e_respeita_o_cancelamento()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var r = StressCpu.Correr(2, CargaCpu.Inteiro, cts.Token);

        Assert.True(r.Unidades > 0, "devia contar trabalho feito");
        Assert.True(r.Segundos > 0.05);
        Assert.Contains("misturas", r.Unidade);
    }

    [Fact]
    public void Carga_vetorial_calcula_gflops()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var r = StressCpu.Correr(1, CargaCpu.Vector, cts.Token);

        Assert.True(r.Flops > 0);
    }
}

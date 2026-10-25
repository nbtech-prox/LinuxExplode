using LinuxExplode.Core;

namespace LinuxExplode.Tests;

/// <summary>
/// Ranking local (item 9): a mediana das últimas corridas é o número que vale, e a variação entre elas
/// é o que diz se a máquina estava estável. Abaixo de três corridas não se grita "instável" — não há
/// como distinguir ruído de um problema.
/// </summary>
public sealed class RankingTests : IDisposable
{
    private readonly string _pasta;

    public RankingTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "linuxexplode-ranking-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
        Environment.SetEnvironmentVariable("LINUXEXPLODE_DADOS", _pasta);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LINUXEXPLODE_DADOS", null);
        try { Directory.Delete(_pasta, true); } catch { }
    }

    [Theory]
    [InlineData(new[] { 10.0, 20.0, 30.0 }, 20.0)]      // ímpar: o do meio
    [InlineData(new[] { 30.0, 10.0, 20.0 }, 20.0)]      // a ordem de entrada não conta
    [InlineData(new[] { 10.0, 20.0, 30.0, 40.0 }, 25.0)] // par: média dos dois do meio
    [InlineData(new[] { 42.0 }, 42.0)]
    public void Mediana_esta_certa(double[] valores, double esperado)
    {
        Assert.Equal(esperado, Historico.Mediana(valores)!.Value, 6);
    }

    [Fact]
    public void Mediana_sem_valores_e_nula()
    {
        Assert.Null(Historico.Mediana(Array.Empty<double>()));
        Assert.Null(Historico.Mediana(new List<double>()));
    }

    [Fact]
    public void Variacao_e_a_amplitude_em_percentagem_do_maior()
    {
        // 100 e 103 → 3/103 = 2,91 %
        Assert.Equal(2.913, Historico.Variacao(new[] { 100.0, 103.0 }), 3);

        // duas corridas iguais → 0 %
        Assert.Equal(0, Historico.Variacao(new[] { 100.0, 100.0 }), 6);

        // uma só corrida não tem variação
        Assert.Equal(0, Historico.Variacao(new[] { 100.0 }), 6);
        Assert.Equal(0, Historico.Variacao(Array.Empty<double>()), 6);
    }

    [Fact]
    public void Rankings_usa_as_ultimas_tres_e_avisa_acima_dos_tres_por_cento()
    {
        // quatro corridas: as três últimas são 100, 101 e 105 → variação 4,76 % (acima dos 3 %)
        foreach (double nota in new[] { 90.0, 100.0, 101.0, 105.0 })
            Historico.Registar("cpu", "16 threads", nota, "corrida");

        var linha = Assert.Single(Historico.Rankings());

        Assert.Equal("cpu", linha.Tipo);
        Assert.Equal("16 threads", linha.Alvo);
        Assert.Equal(105, linha.Melhor);
        Assert.Equal(101, linha.Mediana);          // mediana de 100, 101, 105
        Assert.Equal(105, linha.Ultima);
        Assert.Equal(4, linha.Corridas);
        Assert.True(linha.Instavel);
        Assert.NotNull(linha.Aviso);
        Assert.Contains("4,8 %", linha.Aviso!);
    }

    [Fact]
    public void Duas_corridas_instaveis_nao_levantam_aviso()
    {
        // com duas corridas não há como saber se é ruído; o aviso só existe a partir de três
        Historico.Registar("gpu", "RX 6700 XT", 100, "a");
        Historico.Registar("gpu", "RX 6700 XT", 140, "b");

        var linha = Assert.Single(Historico.Rankings());

        Assert.Equal(2, linha.Corridas);
        Assert.False(linha.Instavel);
        Assert.Null(linha.Aviso);
        Assert.Contains("mediana", linha.Linha);
    }

    [Fact]
    public void Corridas_estaveis_nao_levantam_aviso()
    {
        foreach (double nota in new[] { 100.0, 101.0, 102.5 })
            Historico.Registar("rt", "RX 6700 XT", nota, "corrida");

        var linha = Assert.Single(Historico.Rankings());

        Assert.False(linha.Instavel);            // 2,44 % < 3 %
        Assert.Null(linha.Aviso);
        Assert.Equal(101, linha.Mediana);
    }

    [Fact]
    public void Sem_historico_o_ranking_vem_vazio()
    {
        Assert.Empty(Historico.Rankings());
    }

    [Fact]
    public void Ultimas_devolve_a_serie_por_ordem_de_chegada()
    {
        foreach (double nota in new[] { 1.0, 2.0, 3.0 })
            Historico.Registar("disco", "sda", nota, "x");

        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, Historico.Ultimas("disco", "sda", 3));
        Assert.Equal(new[] { 2.0, 3.0 }, Historico.Ultimas("disco", "sda", 2));
    }
}

/// <summary>
/// Latência por blocos (item 11): a curva tem de subir com o tamanho do conjunto de trabalho — se não
/// subir, está a medir outra coisa (pré-busca, cronómetro, ou nada).
/// </summary>
public sealed class LatenciaMemoriaTests
{
    [Theory]
    [InlineData(4 * 1024, "L1")]
    [InlineData(16 * 1024, "L1")]
    [InlineData(1024 * 1024, "L2")]
    [InlineData(4L * 1024 * 1024, "L3")]
    [InlineData(256L * 1024 * 1024, "RAM")]
    public void Tamanho_cai_na_fase_certa(long bytes, string fase)
    {
        Assert.Equal(fase, new LatenciaBloco(bytes, 1).Fase);
    }

    [Fact]
    public void Tamanho_em_texto_legivel()
    {
        Assert.Equal("4 KB", new LatenciaBloco(4 * 1024, 1).Tamanho);
        Assert.Equal("256 MB", new LatenciaBloco(256L * 1024 * 1024, 1).Tamanho);
    }

    [Fact]
    public void A_curva_declara_os_nove_tamanhos_do_roadmap()
    {
        Assert.Equal(9, BenchMemoria.Tamanhos.Count);
        Assert.Equal(4 * 1024, BenchMemoria.Tamanhos[0]);
        Assert.Equal(256L * 1024 * 1024, BenchMemoria.Tamanhos[^1]);
    }

    [Fact]
    public void A_latencia_cresce_com_o_conjunto_de_trabalho()
    {
        // 4 KB cabe todo em L1 e 64 MB não cabe em cache nenhuma: a diferença tem de ser grande e no
        // sentido certo. Três medições para não decidir por um valor com ruído.
        double pequeno = Media(BenchMemoria.Medir(4 * 1024), BenchMemoria.Medir(4 * 1024), BenchMemoria.Medir(4 * 1024));
        double grande = Media(BenchMemoria.Medir(64L * 1024 * 1024), BenchMemoria.Medir(64L * 1024 * 1024));

        Assert.True(pequeno > 0, "a latência de L1 tem de ser positiva");
        Assert.True(grande > pequeno * 4, $"esperava RAM bem mais lenta que L1 (L1 {pequeno:0.0} ns, RAM {grande:0.0} ns)");

        static double Media(params double[] v) => v.Average();
    }

    [Fact]
    public void Resumo_agrupa_por_fase()
    {
        var pontos = new List<LatenciaBloco>
        {
            new(4 * 1024, 1.2), new(16 * 1024, 1.3),
            new(1024 * 1024, 3.1),
            new(64L * 1024 * 1024, 80.0)
        };

        string resumo = BenchMemoria.Resumo(pontos);

        Assert.Contains("L1 1,2 ns", resumo);
        Assert.Contains("L2 3,1 ns", resumo);
        Assert.Contains("RAM 80,0 ns", resumo);
        Assert.Equal("sem medições", BenchMemoria.Resumo(Array.Empty<LatenciaBloco>()));
    }
}

/// <summary>
/// Tarefa "do mundo real" (item 12): a descompressão é verificada por SHA-256, por isso um resultado
/// só é aceite se os dados voltarem intactos; e a tarefa de compilação tem de ser determinística.
/// </summary>
public sealed class TarefaRealTests
{
    [Fact]
    public void A_fonte_C_gerada_e_deterministica_e_tem_as_funcoes_pedidas()
    {
        string a = TarefaReal.GerarFonteC();
        string b = TarefaReal.GerarFonteC();

        Assert.Equal(a, b);
        Assert.Contains("int main(void)", a);
        Assert.Contains($"int f{TarefaReal.FuncoesC - 1}(int x)", a);
        Assert.DoesNotContain($"int f{TarefaReal.FuncoesC}(int x)", a);
    }

    [Fact]
    public void Descomprimir_devolve_os_dados_originais_e_mede_a_velocidade()
    {
        var r = TarefaReal.Correr();

        Assert.Equal(TarefaReal.BytesPayload, r.BytesDescomprimidos);
        Assert.Equal(64, r.Sha256.Length);
        Assert.True(r.DescompressaoMBs > 5, $"descompressão improvável: {r.DescompressaoMBs:0.0} MB/s");
        Assert.True(r.TotalSegundos > 0);

        // Com compilador, a nota existe; sem ele, o motivo é obrigatório (nunca um número inventado).
        if (r.CompilacaoSegundos is null)
        {
            Assert.NotNull(r.CompilacaoMotivo);
            Assert.Null(r.CompilacaoKBs);
        }
        else
        {
            Assert.True(r.CompilacaoKBs > 0);
            Assert.NotNull(r.Compilador);
            Assert.Null(r.CompilacaoMotivo);
        }
    }
}

/// <summary>
/// Sessão gravada (item 18): o CSV tem de sair com os valores medidos e as células vazias onde não houve
/// leitura, nunca zeros inventados; o resumo calcula mínimo, máximo e média só do que existe.
/// </summary>
public sealed class SessaoTests : IDisposable
{
    private readonly string _pasta;

    public SessaoTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "linuxexplode-sessao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
    }

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch { }
    }

    [Fact]
    public void Linha_do_csv_usa_ponto_decimal_e_deixa_vazio_o_que_falta()
    {
        var completa = new AmostraSessao(new DateTime(2026, 10, 25, 14, 30, 5), 47.5, 39.0, 19.63, 1744.1);
        Assert.Equal("2026-10-25 14:30:05;47.5;39;19.63;1744.1", GravadorSessao.Linha(completa));

        var semPotencia = new AmostraSessao(new DateTime(2026, 10, 25, 14, 30, 6), 48.0, 39.5, null, 1800.0);
        Assert.Equal("2026-10-25 14:30:06;48;39.5;;1800", GravadorSessao.Linha(semPotencia));
    }

    [Fact]
    public void Grava_o_csv_com_cabecalho_e_uma_linha_por_amostra()
    {
        var amostras = new List<AmostraSessao>
        {
            new(new DateTime(2026, 10, 25, 14, 30, 0), 45, 38, null, 1600),
            new(new DateTime(2026, 10, 25, 14, 30, 1), 46, 38.5, 21.4, 1650)
        };

        string caminho = GravadorSessao.EscreverCsv(amostras, Path.Combine(_pasta, "sessao.csv"));
        var linhas = File.ReadAllLines(caminho);

        Assert.Equal(GravadorSessao.Cabecalho, linhas[0]);
        Assert.Equal(3, linhas.Length);
        Assert.StartsWith("2026-10-25 14:30:00;45;38;;1600", linhas[1]);
    }

    [Fact]
    public void Resumo_ignora_as_series_sem_leitura()
    {
        var (min, max, media) = ResumoSessao.Calcular(new double?[] { null, null });

        Assert.Null(min);
        Assert.Null(max);
        Assert.Null(media);

        var (min2, max2, media2) = ResumoSessao.Calcular(new double?[] { 40, null, 44 });
        Assert.Equal(40, min2!.Value);
        Assert.Equal(44, max2!.Value);
        Assert.Equal(42, media2!.Value);
    }

    [Fact]
    public void Uma_sessao_gravada_fica_no_ficheiro_e_no_resumo()
    {
        using var gravador = new GravadorSessao(_pasta);
        string caminho = gravador.Iniciar();

        Assert.True(gravador.A_Gravar);
        Assert.True(File.Exists(caminho));

        for (int i = 0; i < 3; i++) gravador.Amostrar();

        var (amostras, resumo) = gravador.Parar();

        Assert.Equal(3, amostras.Count);
        Assert.Equal(3, resumo.Amostras);
        Assert.False(gravador.A_Gravar);

        var linhas = File.ReadAllLines(caminho);
        Assert.Equal(1 + 3, linhas.Length);       // cabeçalho + 3 amostras

        // nesta máquina há temperaturas de CPU/GPU; se alguma faltasse, o resumo diria "sem leitura"
        if (resumo.Cpu.Media is double media) Assert.InRange(media, 0, 120);
    }
}

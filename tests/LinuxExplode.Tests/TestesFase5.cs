using LinuxExplode.Core;

namespace LinuxExplode.Tests;

/// <summary>
/// Fase 5 (0.20.11): a série de ciclos agrega como o ranking (mediana, não média), os contadores
/// somam-se, a variação só se afirma a partir de 3 ciclos, e o parser do auto-teste S.M.A.R.T. lê as
/// duas famílias de log (NVMe e ATA) — incluindo o teste a correr, que não é histórico.
/// </summary>
public sealed class CiclosTests
{
    [Fact]
    public void Agregado_usa_a_mediana_e_nao_a_media()
    {
        var porCiclo = new List<List<(string Chave, double Valor)>>
        {
            new() { ("cpu_gflops", 100.0) },
            new() { ("cpu_gflops", 110.0) },
            new() { ("cpu_gflops", 400.0) }
        };

        var cpu = SessaoCiclos.Agregar(porCiclo).Single(m => m.Chave == "cpu_gflops");

        Assert.Equal(110.0, cpu.Mediana);          // a média seria 203,3
        Assert.Equal(100.0, cpu.Minimo);
        Assert.Equal(400.0, cpu.Maximo);
        Assert.Equal(3, cpu.Amostras);
        Assert.False(cpu.Contador);
    }

    [Fact]
    public void Contadores_somam_se_em_vez_de_ter_mediana()
    {
        var porCiclo = new List<List<(string Chave, double Valor)>>
        {
            new() { ("ram_erros", 0.0) },
            new() { ("ram_erros", 2.0) },
            new() { ("ram_erros", 1.0) }
        };

        var erros = SessaoCiclos.Agregar(porCiclo).Single(m => m.Chave == "ram_erros");

        Assert.True(erros.Contador);
        Assert.Equal(3.0, erros.Mediana);          // o campo Mediana transporta o total
        Assert.Equal(0.0, erros.Minimo);
        Assert.Equal(2.0, erros.Maximo);
    }

    [Fact]
    public void Com_menos_de_tres_ciclos_a_variacao_nao_e_afirmada()
    {
        var dois = new List<List<(string Chave, double Valor)>>
        {
            new() { ("cpu_gflops", 100.0) },
            new() { ("cpu_gflops", 150.0) }
        };

        var m = SessaoCiclos.Agregar(dois).Single();

        Assert.Equal(0, m.VariacaoPct);
        Assert.Equal(2, m.Amostras);
    }

    [Fact]
    public void Metricas_que_so_aparecem_nuns_ciclos_contam_as_amostras_reais()
    {
        var porCiclo = new List<List<(string Chave, double Valor)>>
        {
            new() { ("cpu_gflops", 100.0), ("disco_iops4k", 5000.0) },
            new() { ("cpu_gflops", 102.0) },
            new() { ("cpu_gflops", 104.0) }
        };

        var agregado = SessaoCiclos.Agregar(porCiclo);

        Assert.Equal(3, agregado.Single(m => m.Chave == "cpu_gflops").Amostras);
        Assert.Equal(1, agregado.Single(m => m.Chave == "disco_iops4k").Amostras);
    }
}

public sealed class AutoTesteSmartTests
{
    // JSON reduzido do smartctl -a de um NVMe com dois testes no histórico.
    private const string NvmeParado = """
    {
      "smartctl": { "exit_status": 0 },
      "smart_status": { "passed": true },
      "nvme_self_test_log": {
        "current_self_test_operation": { "string": "No self-test in progress" },
        "current_self_test_completion_percent": 0,
        "table": [
          { "self_test_code": { "string": "Short" }, "self_test_result": { "string": "Completed without error" }, "power_on_hours": 9642 },
          { "self_test_code": { "string": "Short" }, "self_test_result": { "string": "Aborted by host" }, "power_on_hours": 9642 }
        ]
      }
    }
    """;

    private const string NvmeACorrer = """
    {
      "smart_status": { "passed": true },
      "nvme_self_test_log": {
        "current_self_test_operation": { "string": "Self-test in progress" },
        "current_self_test_completion_percent": 42,
        "table": []
      }
    }
    """;

    private const string AtaACorrer = """
    {
      "smart_status": { "passed": true },
      "ata_smart_data": {
        "self_test": { "status": { "string": "Self-test routine in progress", "value": 240, "remaining_percent": 30 } }
      },
      "ata_smart_self_test_log": {
        "standard": {
          "table": [
            { "type": { "string": "Short offline" }, "status": { "string": "Completed without error", "passed": true }, "lifetime_hours": 369 },
            { "type": { "string": "Extended offline" }, "status": { "string": "Self-test routine in progress" }, "lifetime_hours": 370 }
          ]
        }
      }
    }
    """;

    [Fact]
    public void Nvme_parado_le_o_historico_e_traduz_o_resultado()
    {
        var info = Discos.InterpretarAutoTeste(NvmeParado);

        Assert.True(info.Suportado);
        Assert.False(info.EmCurso);
        Assert.Equal(2, info.Historico.Count);
        Assert.Equal("curto", info.Historico[0].Tipo);
        Assert.Equal("concluído sem erros", info.Historico[0].Resultado);
        Assert.True(info.Historico[0].Passou);
        Assert.Equal(9642, info.Historico[0].HorasLigado);
        Assert.Equal("abortado pelo utilizador", info.Historico[1].Resultado);
        Assert.Equal("concluído sem erros", info.Estado);
    }

    [Fact]
    public void Nvme_a_correr_publica_a_percentagem()
    {
        var info = Discos.InterpretarAutoTeste(NvmeACorrer);

        Assert.True(info.EmCurso);
        Assert.Equal(42, info.Percentagem);
        Assert.Equal(58, info.RestantePercent);
        Assert.Contains("a correr", info.Estado);
    }

    [Fact]
    public void Ata_a_correr_converte_o_restante_em_percentagem_e_nao_conta_a_linha_como_historico()
    {
        var info = Discos.InterpretarAutoTeste(AtaACorrer);

        Assert.True(info.Suportado);
        Assert.True(info.EmCurso);
        Assert.Equal(70, info.Percentagem);
        Assert.Single(info.Historico);
        Assert.Equal("curto", info.Historico[0].Tipo);
    }

    [Fact]
    public void Json_invalido_ou_sem_dados_nao_rebenta()
    {
        var invalido = Discos.InterpretarAutoTeste("{ isto não é json");
        Assert.False(invalido.Suportado);
        Assert.Contains("JSON inválido", invalido.Nota);

        var vazio = Discos.InterpretarAutoTeste("""{ "smartctl": { "exit_status": 2 } }""");
        Assert.False(vazio.Suportado);
        Assert.Contains("S.M.A.R.T. precisa de root", vazio.Nota);
    }
}

public sealed class SuspensaoTests
{
    [Fact]
    public void Simulacao_mede_os_relogios_sem_dormir()
    {
        var r = Suspender.Testar(5, simular: true);

        // sem suspend, o relógio de parede e o monotónico andam juntos: a diferença tem de ser ~0
        Assert.False(r.Suspendeu);
        Assert.True(r.DormiuS < 1.0, $"dormiu {r.DormiuS:0.00} s numa simulação");
        Assert.True(r.RelogioParedeS >= 1.0, $"a simulação andou {r.RelogioParedeS:0.00} s");
        Assert.Contains("simulação", r.Veredito);
        Assert.Contains(r.Linhas, l => l.Contains("monotónico", StringComparison.Ordinal));
    }
}

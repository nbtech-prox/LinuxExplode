using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinuxExplode.Core;

/// <summary>Exporta a informação do sistema em HTML (com a marca EXPLODE) ou JSON.</summary>
public static class Relatorio
{
    public const string Versao = "0.20.15";

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

    /// <summary>
    /// Relatório HTML completo: sistema, sensores, discos, benchmarks, último stress/ciclos/VRAM e a
    /// última sessão gravada. Mantém a assinatura antiga para o <c>--cli relatorio html</c> e a página
    /// Relatório não mudarem; o conteúdo vem de <see cref="RelatorioCompleto"/>.
    /// </summary>
    public static string GerarHtml(Sistema s, List<Sensor> sensores, List<DiscoInfo> discos)
        => RelatorioCompleto.GerarHtml(s, sensores, discos, RelatorioCompleto.Recolher());
}

using LinuxExplode.Core;

namespace LinuxExplode.Tests;

/// <summary>
/// Regras da página Dispositivos: a geração e o débito do link PCIe saem da velocidade negociada
/// (não de uma tabela inventada) e as temperaturas vão para o componente certo — o chipset não é CPU.
/// </summary>
public sealed class PlacaTests
{
    [Theory]
    [InlineData("16.0 GT/s PCIe", 16.0)]
    [InlineData("2.5 GT/s PCIe", 2.5)]
    [InlineData("8.0 GT/s PCIe", 8.0)]
    [InlineData("", 0)]
    [InlineData("desconhecido", 0)]
    public void Gts_le_o_numero_do_sysfs(string texto, double esperado)
    {
        Assert.Equal(esperado, Placa.Gts(texto));
    }

    [Theory]
    [InlineData(2.5, "1.0")]
    [InlineData(5.0, "2.0")]
    [InlineData(8.0, "3.0")]
    [InlineData(16.0, "4.0")]
    [InlineData(32.0, "5.0")]
    [InlineData(64.0, "6.0")]
    [InlineData(1.0, "")]
    public void Geracao_segue_a_taxa_de_transferencia(double gts, string esperado)
    {
        Assert.Equal(esperado, Placa.Geracao(gts));
    }

    [Fact]
    public void Banda_teorica_desconta_a_codificacao_do_barramento()
    {
        // Gen4 x16: 16 GT/s * 16 lanes * 128/130 / 8 bits = 31,5 GB/s
        var gen4 = Placa.BandaGbS(16, 16);
        Assert.NotNull(gen4);
        Assert.InRange(gen4!.Value, 31.4, 31.6);

        // Gen1 x1: 2,5 GT/s * 1 lane * 8/10 / 8 bits = 0,25 GB/s
        var gen1 = Placa.BandaGbS(2.5, 1);
        Assert.NotNull(gen1);
        Assert.InRange(gen1!.Value, 0.24, 0.26);

        Assert.Null(Placa.BandaGbS(null, 4));
        Assert.Null(Placa.BandaGbS(8, null));
    }

    [Fact]
    public void Link_reduzido_so_conta_em_dispositivos_finais()
    {
        // ponta: anuncia Gen4 x16 mas está a Gen3 x8 -> reduzido
        var ponta = new LinkPcie("03:00.0", "Placa gráfica", "Placa gráfica", "0x030000", 8, 16, 8, 16);
        Assert.False(ponta.Ponte);
        Assert.True(ponta.Reduzido);
        Assert.Equal("PCIe 3.0 x8", ponta.TextoLink);
        Assert.Equal("PCIe 4.0 x16", ponta.TextoMaximo);

        // ponte (0x0604): o valor actual é o link que está a servir, não se avalia
        var ponte = new LinkPcie("00:01.1", "Ponte", "Ponte PCIe", "0x060400", 16, 32, 16, 16);
        Assert.True(ponte.Ponte);
        Assert.False(ponte.Reduzido);

        // ponta no máximo anunciado: nada a assinalar
        var cheia = new LinkPcie("04:00.0", "NVMe", "Armazenamento", "0x010802", 8, 8, 4, 4);
        Assert.False(cheia.Reduzido);
    }

    [Theory]
    [InlineData("0x030000", "Placa gráfica")]
    [InlineData("0x010802", "Armazenamento")]
    [InlineData("0x020000", "Rede")]
    [InlineData("0x040300", "Áudio")]
    [InlineData("0x060400", "Ponte PCIe")]
    [InlineData("0x0c0330", "Barramento série (USB)")]
    [InlineData("0x108000", "Controlador de cifra")]
    public void Classe_traduz_o_codigo_pci(string codigo, string esperado)
    {
        Assert.Equal(esperado, Placa.Classe(codigo));
    }

    [Theory]
    [InlineData("Advanced Micro Devices, Inc. [AMD/ATI]", "AMD/ATI")]
    [InlineData("Advanced Micro Devices, Inc. [AMD]", "AMD")]
    [InlineData("Realtek Semiconductor Co., Ltd.", "Realtek")]
    [InlineData("Phison Electronics Corporation", "Phison")]
    public void VendorCurto_encolhe_os_nomes_legais(string vendor, string esperado)
    {
        Assert.Equal(esperado, Placa.VendorCurto(vendor));
    }

    [Fact]
    public void Temperaturas_da_placa_excluem_cpu_e_gpu_e_identificam_o_chipset()
    {
        var sensores = new List<Sensor>
        {
            new("CPU (k10temp)", "Tctl", TipoSensor.Temperatura, 45, "°C"),
            new("GPU (amdgpu) · 03:00.0", "edge", TipoSensor.Temperatura, 60, "°C"),
            new("prom21_xhci · 13:00.0", "Temp 1", TipoSensor.Temperatura, 52.8, "°C"),
            new("Placa-mãe (NCT6687D)", "Thermistor 0", TipoSensor.Temperatura, 44, "°C"),
            new("spd5118", "Temp 1", TipoSensor.Temperatura, 33.3, "°C"),
            new("r8169_0_900:00", "Temp 1", TipoSensor.Temperatura, 38.5, "°C")
        };

        var temps = Placa.Temperaturas(sensores);

        Assert.Equal(4, temps.Count);
        Assert.DoesNotContain(temps, t => t.Fonte is "CPU" or "GPU");
        Assert.Equal("Chipset", temps[0].Fonte);                       // chipset vem primeiro
        Assert.Single(temps.Where(t => t.Fonte == "Memória"));
        Assert.Single(temps.Where(t => t.Fonte == "Rede"));
        Assert.Equal(33.3, temps.Single(t => t.Fonte == "Memória").Celsius, 3);
    }

    [Fact]
    public void Sem_sensor_de_chipset_a_lista_de_faltas_diz_isso()
    {
        var semNada = Placa.SemSensor(new List<TemperaturaPlaca>());

        Assert.Contains("chipset", semNada);
        Assert.Contains(semNada, f => f.Contains("VRM"));

        var comChipset = Placa.SemSensor(new List<TemperaturaPlaca>
        {
            new("Chipset", "prom21_xhci", "Temp 1", 50)
        });

        Assert.DoesNotContain("chipset", comChipset);
        Assert.Contains(comChipset, f => f.Contains("VRM"));           // VRM não existe nesta máquina
    }
}

/// <summary>
/// USB: a velocidade mostrada é a negociada com o kernel (<c>speed</c>), não um teste de débito; e um
/// dispositivo sem descritores de texto é identificado pelo driver em vez de ficar "Desconhecido".
/// </summary>
public sealed class UsbTests
{
    [Theory]
    [InlineData(1.5, "1,5 Mbps (low speed)")]
    [InlineData(12, "12 Mbps (full speed)")]
    [InlineData(480, "480 Mbps (high speed)")]
    [InlineData(5000, "5 Gbps (USB 3.0)")]
    [InlineData(10000, "10 Gbps (USB 3.1)")]
    [InlineData(20000, "20 Gbps (USB 3.2 2x2)")]
    public void Velocidade_negociada_em_texto(double mbps, string esperado)
    {
        Assert.Equal(esperado, Usb.VelocidadeTexto(mbps));
    }

    [Fact]
    public void Sem_leitura_de_velocidade_diz_porque()
    {
        Assert.Equal("velocidade não exposta", Usb.VelocidadeTexto(null));
    }

    [Theory]
    [InlineData("01", "áudio")]
    [InlineData("03", "HID")]
    [InlineData("08", "armazenamento")]
    [InlineData("09", "hub")]
    [InlineData("0e", "vídeo")]
    [InlineData("e0", "sem fios")]
    [InlineData("ff", "específico do fabricante")]
    public void Classe_de_interface_traduzida(string hex, string esperado)
    {
        Assert.Equal(esperado, Usb.Classe(hex));
    }

    [Fact]
    public void Dispositivo_sem_descritor_usa_o_driver_como_nome()
    {
        var bt = new DispositivoUsb("10-12", 10, 8, "0489", "e10a", null, null, null,
            12, "1.10", "100mA", false, new List<string> { "sem fios" }, new List<string> { "btusb" });

        Assert.Contains("Bluetooth", bt.Nome);
        Assert.False(bt.TemAudio);
    }

    [Fact]
    public void Dispositivo_com_produto_mostra_o_produto()
    {
        var webcam = new DispositivoUsb("10-6", 10, 3, "1bcf", "2701", "SHENZHEN AONI", "Full HD webcam",
            null, 480, "2.00", "500mA", false, new List<string> { "áudio", "vídeo" },
            new List<string> { "snd-usb-audio", "uvcvideo" });

        Assert.Equal("Full HD webcam", webcam.Nome);
        Assert.True(webcam.TemAudio);
        Assert.Contains("480 Mbps", Usb.Linha(webcam));
    }
}

/// <summary>
/// Áudio: bytes por amostra por formato ALSA, latência do período a partir de hw_params, e o parser
/// do <c>pw-top</c> (a coluna ERR é o contador de xruns do servidor).
/// </summary>
public sealed class AudioTests
{
    [Theory]
    [InlineData("S16_LE", 2)]
    [InlineData("S24_3LE", 4)]
    [InlineData("S32_LE", 4)]
    [InlineData("FLOAT_LE", 4)]
    [InlineData("FLOAT64_LE", 8)]
    [InlineData("U8", 1)]
    public void Bytes_por_amostra_por_formato(string formato, int esperado)
    {
        Assert.Equal(esperado, Audio.BytesPorFrame(formato));
    }

    [Fact]
    public void Latencia_e_buffer_saem_do_hw_params()
    {
        // período 1024 frames e buffer 16384 frames a 48 kHz; S16_LE estereofónico = 4 B por frame
        var fluxo = new FluxoAlsa("Portal", "pcm0p", "USB Audio", true, "RUNNING",
            48000, 2, "S16_LE", 1024 * 4, 16384 * 4, 512);

        Assert.True(fluxo.Activo);
        Assert.Equal(4, fluxo.BytesPorFrame);
        Assert.InRange(fluxo.LatenciaMs!.Value, 21.3, 21.4);
        Assert.InRange(fluxo.BufferMs!.Value, 341.2, 341.4);
        Assert.InRange(fluxo.AtrasoMs!.Value, 10.6, 10.7);
    }

    [Fact]
    public void Fluxo_fechado_nao_e_activo()
    {
        var fluxo = new FluxoAlsa("HDMI", "pcm3p", "HDMI 0", true, "closed", null, null, null, null, null, null);

        Assert.False(fluxo.Activo);
        Assert.Null(fluxo.LatenciaMs);
        Assert.Null(fluxo.BufferMs);
    }

    [Fact]
    public void PwTop_da_quantum_taxa_e_xruns_por_no()
    {
        // recorte com a forma real de `pw-top -b -n 2` (colunas fixas; FORMAT pode estar vazio)
        const string saida = """
            S   ID  QUANT   RATE    WAIT    BUSY   W/Q   B/Q  ERR FORMAT           NAME
            S   29      0      0    ---     ---   ---   ---     0                  Dummy-Driver
            S   57   1024  48000   0,1%    0,2%  0,01  0,02     3   F32LE 2 48000 alsa_output.pci-0000_03_00.1.hdmi-stereo
            S   64   1024  48000   0,3%    0,4%  0,01  0,02     1                  alsa_output.usb-Portal.analog-stereo
            """;

        var nos = Audio.ParsePwTop(saida);

        Assert.Equal(3, nos.Count);
        Assert.Equal(3, nos[0].Xruns);                                  // ordenado por xruns desc.
        Assert.Equal("alsa_output.pci-0000_03_00.1.hdmi-stereo", nos[0].Nome);
        Assert.Equal(1024, nos[0].Quantum);
        Assert.Equal(48000, nos[0].Taxa);
        Assert.Equal(4, nos.Sum(n => n.Xruns));
        Assert.Equal("Dummy-Driver", nos[^1].Nome);
        Assert.Equal(0, nos[^1].Quantum);
    }

    [Fact]
    public void PwTop_vazio_nao_rebenta()
    {
        Assert.Empty(Audio.ParsePwTop(""));
        Assert.Empty(Audio.ParsePwTop("lixo sem colunas"));
    }

    [Fact]
    public void Quantum_em_milissegundos()
    {
        var no = new NoAudio(57, "alsa_output", 1024, 48000, 0, true);

        Assert.InRange(no.QuantumMs!.Value, 21.3, 21.4);
    }

    [Fact]
    public void Placas_de_som_saem_do_proc_asound()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "linuxexplode-asound-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(pasta, "card1", "pcm0p", "sub0"));
        try
        {
            File.WriteAllText(Path.Combine(pasta, "cards"),
                " 0 [HDMI           ]: HDA-Intel - HDA ATI HDMI\n" +
                "                      HDA ATI HDMI at 0xdd920000 irq 139\n" +
                " 1 [Portal         ]: USB-Audio - The Portal\n" +
                "                      Solid State System Co.,Ltd. The Portal at usb-0000:15:00.0-9.4, full speed\n");
            File.WriteAllText(Path.Combine(pasta, "card1", "pcm0p", "info"), "name: USB Audio\nsubdevices_count: 1\n");
            File.WriteAllText(Path.Combine(pasta, "card1", "pcm0p", "sub0", "status"),
                "state: RUNNING\nowner_pid: 1234\ndelay: 480\navail: 1024\navail_max: 2048\nhw_ptr: 100\nappl_ptr: 200\n");
            File.WriteAllText(Path.Combine(pasta, "card1", "pcm0p", "sub0", "hw_params"),
                "access: RW_INTERLEAVED\nformat: S16_LE\nchannels: 2\nrate: 48000 (48000/1)\nperiod_size: 1024\nbuffer_size: 16384\n");

            var placas = Audio.LerPlacas(pasta);
            var fluxos = Audio.LerFluxos(pasta, placas);

            Assert.Equal(2, placas.Count);
            Assert.Equal("HDMI", placas[0].Id);
            Assert.Equal("HDA ATI HDMI", placas[0].Nome);
            Assert.Equal("usb 15:00.0-9.4", placas[1].Barramento);

            var activo = Assert.Single(fluxos);
            Assert.True(activo.Activo);
            Assert.Equal("USB Audio", activo.Dispositivo);
            Assert.Equal(48000, activo.Taxa);
            Assert.Equal(2, activo.Canais);
            Assert.Equal("S16_LE", activo.Formato);
            Assert.Equal(480, activo.AtrasoFrames);
        }
        finally
        {
            try { Directory.Delete(pasta, true); } catch { }
        }
    }

    [Fact]
    public void Pcm_fechado_aparece_como_closed()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "linuxexplode-asound2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(pasta, "card0", "pcm3p", "sub0"));
        try
        {
            File.WriteAllText(Path.Combine(pasta, "cards"), " 0 [HDMI           ]: HDA-Intel - HDA ATI HDMI\n");
            File.WriteAllText(Path.Combine(pasta, "card0", "pcm3p", "sub0", "status"), "closed\n");
            File.WriteAllText(Path.Combine(pasta, "card0", "pcm3p", "info"), "name: HDMI 0\n");

            var fluxos = Audio.LerFluxos(pasta);

            var f = Assert.Single(fluxos);
            Assert.Equal("closed", f.Estado);
            Assert.False(f.Activo);
            Assert.Equal("HDMI 0", f.Dispositivo);
        }
        finally
        {
            try { Directory.Delete(pasta, true); } catch { }
        }
    }
}

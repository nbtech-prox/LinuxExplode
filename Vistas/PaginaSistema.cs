using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaSistema : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };

    // suspender e retomar
    private readonly TextBlock _suspEstado = new() { Text = "pronto", Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _suspLinhas = new() { Spacing = 2 };
    private readonly ComboBox _suspSegundos = new() { Width = 130 };
    private readonly List<Button> _suspBotoes = new();
    private bool _suspACorrer;

    public PaginaSistema()
    {
        Content = UI.Pagina(
            UI.Cabecalho("Sistema", "Informação de hardware lida diretamente de /sys, /proc e DMI"),
            _corpo);

        _corpo.Children.Add(UI.Texto("a ler hardware…", "sub"));
        Loaded += async (_, _) => await Carregar();
    }

    private async Task Carregar()
    {
        // gancho de captura: LINUXEXPLODE_SECAO=suspender deixa só esse cartão no topo da janela
        if ((Environment.GetEnvironmentVariable("LINUXEXPLODE_SECAO") ?? "")
            .Equals("suspender", StringComparison.OrdinalIgnoreCase))
        {
            _corpo.Children.Clear();
            _corpo.Children.Add(CartaoSuspensao());

            // LINUXEXPLODE_AUTOTEST=suspender corre a simulação (nunca suspende) para provar o caminho
            // do botão sem clicar: o resultado tem de aparecer no cartão
            if ((Environment.GetEnvironmentVariable("LINUXEXPLODE_AUTOTEST") ?? "")
                .Equals("suspender", StringComparison.OrdinalIgnoreCase))
                _ = TestarSuspensao(true);

            return;
        }

        Sistema s;
        try { s = await Task.Run(Sistema.Recolher); }
        catch (Exception ex)
        {
            _corpo.Children.Clear();
            _corpo.Children.Add(UI.Texto(Textos.T("Falha a ler o hardware: ") + ex.Message, "sub"));
            return;
        }

        _corpo.Children.Clear();
        _corpo.Children.Add(Metricas(s));
        _corpo.Children.Add(UI.Cartao("Sistema operativo", CartaoSo(s)));
        _corpo.Children.Add(DuasColunas(UI.Cartao("Processador", CartaoCpu(s)), UI.Cartao("Memória", CartaoRam(s))));
        _corpo.Children.Add(UI.Cartao("Placa-mãe e BIOS", CartaoPlaca(s)));

        if (s.Gpus.Count == 0)
        {
            _corpo.Children.Add(UI.Cartao("Placa de vídeo",
                UI.Texto("Nenhuma GPU detetada em /sys/class/drm.", "sub"), true));
        }
        else
        {
            foreach (var g in s.Gpus)
                _corpo.Children.Add(UI.Cartao("Placa de vídeo", CartaoGpu(g)));
        }

        _corpo.Children.Add(CartaoSuspensao());
    }

    private static Control Metricas(Sistema s)
    {
        long ramUsada = s.RamTotal - s.RamLivre;
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), Margin = new Thickness(0, 0, 0, 2) };

        var celulas = new Control[]
        {
            UI.Metrica(s.Threads.ToString(), "threads", Textos.TF("{0} núcleos", s.NucleosFisicos)),
            UI.Metrica(Util.Gb(s.RamTotal), "RAM", Textos.TF("{0} em uso", Util.Gb(ramUsada))),
            UI.Metrica(s.CpuFreqAtual, "frequência", Textos.TF("máx {0}", s.CpuFreqMax)),
            UI.Metrica(s.Uptime, "ligado há", s.Hostname)
        };
        for (int i = 0; i < celulas.Length; i++)
        {
            var b = new Border { Child = celulas[i], Margin = new Thickness(0, 0, i == celulas.Length - 1 ? 0 : 16, 0) };
            Grid.SetColumn(b, i);
            g.Children.Add(b);
        }
        return g;
    }

    private static Control DuasColunas(Control esquerda, Control direita)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        esquerda.Margin = new Thickness(0, 0, 7, 0);
        direita.Margin = new Thickness(7, 0, 0, 0);
        g.Children.Add(esquerda);
        Grid.SetColumn(direita, 1);
        g.Children.Add(direita);
        return g;
    }

    private static Control CartaoSo(Sistema s)
    {
        var p = new StackPanel { Spacing = 0 };
        p.Children.Add(UI.Linha("Distribuição", s.So));
        p.Children.Add(UI.Linha("Kernel", s.Kernel));
        p.Children.Add(UI.Linha("Arquitetura", s.Arquitetura));
        p.Children.Add(UI.Linha("Máquina", s.Maquina.Length > 0 ? s.Maquina : "—", false));
        return p;
    }

    private static Control CartaoCpu(Sistema s)
    {
        var p = new StackPanel { Spacing = 0 };
        p.Children.Add(UI.Linha("Modelo", s.CpuModelo.Length > 0 ? s.CpuModelo : "—"));
        p.Children.Add(UI.Linha("Núcleos", $"{s.NucleosFisicos} / {s.Threads}"));
        p.Children.Add(UI.Linha("Frequência", s.CpuFreqAtual));
        p.Children.Add(UI.Linha("Máxima", s.CpuFreqMax));
        p.Children.Add(UI.Linha("Cache L3", s.CpuCache, false));
        return p;
    }

    private static Control CartaoRam(Sistema s)
    {
        long usada = s.RamTotal - s.RamLivre;
        var p = new StackPanel { Spacing = 0 };
        p.Children.Add(UI.Linha("Instalada", Util.Gb(s.RamTotal)));
        p.Children.Add(UI.Linha("Em uso", $"{Util.Gb(usada)} ({(s.RamTotal > 0 ? usada * 100.0 / s.RamTotal : 0):0}%)"));
        p.Children.Add(UI.Linha("Disponível", Util.Gb(s.RamLivre)));
        p.Children.Add(UI.Linha("Swap", s.SwapTotal > 0 ? Textos.TF("{0} de {1}", Util.Gb(s.SwapUsada), Util.Gb(s.SwapTotal)) : "sem swap", false));
        p.Children.Add(new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Child = UI.Barra(s.RamTotal > 0 ? (double)usada / s.RamTotal : 0)
        });

        if (s.Modulos.Count > 0)
        {
            foreach (var m in s.Modulos)
                p.Children.Add(UI.Linha(m.Posicao, m.Descricao));

            // módulos iguais (kit) não repetem a mesma linha de detalhe
            var distintos = s.Modulos.Select(m => $"{m.Fabricante} {m.Detalhe}".Trim())
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .ToList();
            var nota = UI.Texto(
                distintos.Count == 1
                    ? distintos[0] + (s.Modulos.Count > 1 ? $"  (×{s.Modulos.Count})" : "")
                    : string.Join("\n", s.Modulos.Select(m => $"{m.Posicao}: {m.Fabricante} {m.Detalhe}".Trim())),
                "sub");
            nota.Margin = new Thickness(0, 4, 0, 0);
            p.Children.Add(nota);
        }
        else
        {
            var nota = UI.Texto("Sem SPD legível sem root: em DDR5 é preciso o módulo spd5118 carregado " +
                                "(expõe /sys/bus/nvmem/devices) e em DDR4 o ee1004.", "sub");
            nota.Margin = new Thickness(0, 8, 0, 0);
            p.Children.Add(nota);
        }
        return p;
    }

    private static Control CartaoPlaca(Sistema s)
    {
        var p = new StackPanel { Spacing = 0 };
        p.Children.Add(UI.Linha("Placa-mãe", s.PlacaMae.Length > 0 ? s.PlacaMae : "—"));
        p.Children.Add(UI.Linha("BIOS", s.Bios.Length > 0 ? s.Bios : "—", false));
        return p;
    }

    private static Control CartaoGpu(GpuInfo g)
    {
        var p = new StackPanel { Spacing = 0 };
        p.Children.Add(UI.Linha("Modelo", g.Nome));
        p.Children.Add(UI.Linha("Barramento PCI", g.Pci));
        p.Children.Add(UI.Linha("Driver", g.Driver));
        if (g.VramTotal is long vt)
            p.Children.Add(UI.Linha("VRAM", $"{Util.Bytes(g.VramUsada ?? 0)} de {Util.Bytes(vt)}"));
        if (g.Temp is not null || g.Uso is not null)
            p.Children.Add(UI.Linha("Agora", Textos.TF("{0}   ·   uso {1}", g.Temp ?? "—", g.Uso ?? "—"), false));
        return p;
    }

    // ------------------------------------------------------------------ suspender e retomar

    private Control CartaoSuspensao()
    {
        var suporte = Suspender.Suporte();

        _suspSegundos.Items.Clear();
        _suspSegundos.Items.Add("10 s");
        _suspSegundos.Items.Add("15 s");
        _suspSegundos.Items.Add("30 s");
        _suspSegundos.Items.Add("60 s");
        _suspSegundos.SelectedIndex = 1;

        var testar = UI.Botao("Testar suspensão", true, () => _ = TestarSuspensao(false));
        var simular = UI.Botao("Simular (sem dormir)", false, () => _ = TestarSuspensao(true));
        testar.IsEnabled = suporte.Suportado && suporte.PodeSuspender;
        _suspBotoes.Clear();
        _suspBotoes.Add(testar);
        _suspBotoes.Add(simular);

        var acoes = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var b in _suspBotoes)
        {
            b.Margin = new Thickness(0, 0, 8, 0);
            acoes.Children.Add(b);
        }

        var suporteLinhas = new StackPanel { Spacing = 0 };
        suporteLinhas.Children.Add(UI.Linha("Estados", suporte.Estados.Length > 0 ? suporte.Estados : "—"));
        suporteLinhas.Children.Add(UI.Linha("Modo em memória", suporte.ModoMemoria ?? "—"));
        suporteLinhas.Children.Add(UI.Linha("Suspender", suporte.PodeSuspender ? "sim (sudo sem password)" : "não — falta permissão"));
        suporteLinhas.Children.Add(UI.Linha("Acordar sozinho", suporte.PodeAcordarSozinho ? "sim (rtcwake)" : "não — acorda-se à mão", false));

        var linhaConfig = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        linhaConfig.Children.Add(UI.Texto("DORMIR", "rotulo"));
        linhaConfig.Children.Add(_suspSegundos);
        linhaConfig.Children.Add(acoes);

        var pilha = new StackPanel { Spacing = 10 };
        pilha.Children.Add(suporteLinhas);
        pilha.Children.Add(UI.Texto(suporte.Nota, "sub"));
        pilha.Children.Add(linhaConfig);
        pilha.Children.Add(_suspEstado);
        pilha.Children.Add(_suspLinhas);

        if (Suspender.LerUltimo() is { } ultimo)
            pilha.Children.Add(UI.Texto(Textos.TF("último teste: {0} · {1}", $"{ultimo.Quando:yyyy-MM-dd HH:mm}", ultimo.Veredito), "sub"));

        pilha.Children.Add(UI.Texto(
            "O teste suspende a máquina a sério e mede o tempo de três formas independentes: relógio de parede " +
            "menos relógio monotónico (que pára no suspend), e o uptime (CLOCK_BOOTTIME). No fim verifica se " +
            "os sensores e a potência voltaram a responder — num portátil é aqui que se vê se o wake deixou " +
            "algo partido. Sem rtcwake no sudoers a máquina só acorda quando lhe tocas; o botão Simular mede " +
            "os relógios sem dormir e prova que a conta dá zero.", "sub"));

        return UI.Cartao("Suspender e retomar", pilha);
    }

    private async Task TestarSuspensao(bool simular)
    {
        if (_suspACorrer) return;
        _suspACorrer = true;

        foreach (var b in _suspBotoes) b.IsEnabled = false;
        _suspEstado.Text = simular ? Textos.T("simulação: a ler os relógios…") : Textos.T("a suspender…");
        _suspLinhas.Children.Clear();

        int segundos = _suspSegundos.SelectedIndex switch { 0 => 10, 2 => 30, 3 => 60, _ => 15 };

        try
        {
            var r = await Task.Run(() => Suspender.Testar(segundos, simular,
                s => Dispatcher.UIThread.Post(() => _suspEstado.Text = s)));
            MostrarSuspensao(r);
        }
        catch (Exception ex)
        {
            _suspEstado.Text = Textos.TF("o teste falhou: {0}", ex.Message);
        }
        finally
        {
            _suspACorrer = false;
            var suporte = Suspender.Suporte();
            foreach (var b in _suspBotoes) b.IsEnabled = true;
            if (_suspBotoes.Count > 0 && (!suporte.Suportado || !suporte.PodeSuspender))
                _suspBotoes[0].IsEnabled = false;
        }
    }

    private void MostrarSuspensao(ResultadoSuspensao r)
    {
        _suspEstado.Text = r.Veredito;
        _suspLinhas.Children.Clear();

        _suspLinhas.Children.Add(UI.Texto(
            Textos.TF("relógio de parede {0} s · suspend medido {1} s · uptime confirma {2} s",
                $"{r.RelogioParedeS:0.00}", $"{r.DormiuS:0.00}", $"{r.DiferencaUptimeS:0.00}"), "mono"));
        _suspLinhas.Children.Add(UI.Texto(
            Textos.TF("sensores CPU {0} → {1} · GPU {2} → {3} · potência {4} → {5}",
                Snap(r.Antes.CpuC), Snap(r.Depois.CpuC), Snap(r.Antes.GpuC), Snap(r.Depois.GpuC),
                SnapW(r.Antes.Watts), SnapW(r.Depois.Watts)), "mono"));
        _suspLinhas.Children.Add(UI.Texto(
            r.SensoresOk && r.EnergiaOk
                ? "sensores e potência voltaram a responder depois do wake"
                : "há leituras que não voltaram depois do wake — o cartão mostra quais", "sub"));

        foreach (var l in r.Linhas) _suspLinhas.Children.Add(UI.Texto("· " + l, "mono"));
    }

    private static string Snap(double? v) => v is double d ? $"{d:0} °C" : "—";

    private static string SnapW(double? v) => v is double d ? $"{d:0.0} W" : "—";
}

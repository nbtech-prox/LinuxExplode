using Avalonia;
using Avalonia.Controls;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaSistema : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };

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
        Sistema s;
        try { s = await Task.Run(Sistema.Recolher); }
        catch (Exception ex)
        {
            _corpo.Children.Clear();
            _corpo.Children.Add(UI.Texto("Falha a ler o hardware: " + ex.Message, "sub"));
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
    }

    private static Control Metricas(Sistema s)
    {
        long ramUsada = s.RamTotal - s.RamLivre;
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), Margin = new Thickness(0, 0, 0, 2) };

        var celulas = new Control[]
        {
            UI.Metrica(s.Threads.ToString(), "threads", $"{s.NucleosFisicos} núcleos"),
            UI.Metrica(Util.Gb(s.RamTotal), "RAM", $"{Util.Gb(ramUsada)} em uso"),
            UI.Metrica(s.CpuFreqAtual, "frequência", $"máx {s.CpuFreqMax}"),
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
        p.Children.Add(UI.Linha("Swap", s.SwapTotal > 0 ? $"{Util.Gb(s.SwapUsada)} de {Util.Gb(s.SwapTotal)}" : "sem swap", false));
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
            p.Children.Add(UI.Linha("Agora", $"{g.Temp ?? "—"}   ·   uso {g.Uso ?? "—"}", false));
        return p;
    }
}

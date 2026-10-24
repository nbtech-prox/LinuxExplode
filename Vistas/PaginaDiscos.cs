using Avalonia.Controls;
using Avalonia.Layout;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaDiscos : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };

    public PaginaDiscos()
    {
        Content = UI.Pagina(
            UI.Cabecalho("Discos e saúde", "/sys/block + smartctl — ocupação, temperatura e S.M.A.R.T.",
                UI.Botao("Atualizar", false, async () => await Carregar())),
            _corpo);

        _corpo.Children.Add(UI.Texto("a ler discos…", "sub"));
        Loaded += async (_, _) => await Carregar();
    }

    private async Task Carregar()
    {
        _corpo.Children.Clear();
        _corpo.Children.Add(UI.Texto("a ler discos…", "sub"));

        List<DiscoInfo> discos;
        try { discos = await Task.Run(Discos.Ler); }
        catch (Exception ex)
        {
            _corpo.Children.Clear();
            _corpo.Children.Add(UI.Texto("Falha a ler discos: " + ex.Message, "sub"));
            return;
        }

        _corpo.Children.Clear();
        if (discos.Count == 0)
        {
            _corpo.Children.Add(UI.Texto("Nenhum disco detetado.", "sub"));
            return;
        }

        foreach (var d in discos)
        {
            var conteudo = new StackPanel { Spacing = 10 };

            var topo = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var nome = new StackPanel { Spacing = 2 };
            nome.Children.Add(UI.Texto(d.Modelo, "valor", 15));
            nome.Children.Add(UI.Texto($"/dev/{d.Dev} · {Util.Bytes(d.Tamanho)} · {(d.Ssd ? "SSD" : "HDD")} · estado {d.Estado}", "sub"));
            topo.Children.Add(nome);

            var pilhaPilulas = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            if (d.Smart.Disponivel)
            {
                var corVeredito = d.Smart.Veredito switch
                {
                    "Bom" => "Bom",
                    "Atenção" => "Atencao",
                    "Mau" => "Mau",
                    _ => "Muted"
                };
                pilhaPilulas.Children.Add(UI.Pilula(d.Smart.Veredito, corVeredito));
            }
            else
            {
                pilhaPilulas.Children.Add(UI.Pilula("sem S.M.A.R.T.", "Muted"));
            }
            if (d.Smart.Temperatura is not null) pilhaPilulas.Children.Add(UI.Pilula(d.Smart.Temperatura, "Muted"));
            Grid.SetColumn(pilhaPilulas, 1);
            topo.Children.Add(pilhaPilulas);
            conteudo.Children.Add(topo);

            // detalhes SMART
            var detalhes = new StackPanel { Spacing = 0 };
            if (d.Smart.HorasLigado is long h) detalhes.Children.Add(UI.Linha("Horas ligado", $"{h:N0} h  ({h / 24.0:N0} dias)"));
            if (d.Smart.Ciclos is long c) detalhes.Children.Add(UI.Linha("Ciclos de energia", $"{c:N0}"));
            if (d.Smart.Desgaste is double des) detalhes.Children.Add(UI.Linha("Desgaste", $"{des:0} %"));
            if (d.Smart.SetoresRealocados is long sr) detalhes.Children.Add(UI.Linha("Setores realocados", $"{sr:N0}"));
            if (d.Smart.ErrosLeitura is long er) detalhes.Children.Add(UI.Linha("Erros I/O", $"{er:N0}"));
            if (detalhes.Children.Count > 0) conteudo.Children.Add(detalhes);

            // partições
            if (d.Particoes.Count > 0)
            {
                var partes = new StackPanel { Spacing = 8 };
                foreach (var p in d.Particoes)
                {
                    var bloco = new StackPanel { Spacing = 4 };
                    var cab = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                    cab.Children.Add(UI.Texto($"/dev/{p.Nome}", "mono"));
                    var txt = p.Total > 0
                        ? $"{Util.Bytes(p.Livre)} livres de {Util.Bytes(p.Total)}"
                        : "não montada";
                    var dir = UI.Texto(txt, "mono");
                    dir.HorizontalAlignment = HorizontalAlignment.Right;
                    Grid.SetColumn(dir, 1);
                    cab.Children.Add(dir);
                    bloco.Children.Add(cab);
                    if (p.Total > 0)
                        bloco.Children.Add(UI.Barra((double)(p.Total - p.Livre) / p.Total));
                    bloco.Children.Add(UI.Texto($"{p.Montagem} · {p.Fs}", "sub"));
                    partes.Children.Add(bloco);
                }
                conteudo.Children.Add(new Border { Height = 1, Background = UI.Cor("Linha"), Margin = new Avalonia.Thickness(0, 4) });
                conteudo.Children.Add(partes);
            }

            if (d.Smart.Nota is not null && d.Smart.Veredito == "—")
                conteudo.Children.Add(UI.Texto(d.Smart.Nota, "sub"));

            _corpo.Children.Add(UI.Cartao(null, conteudo));
        }

        _corpo.Children.Add(UI.Texto(
            "A saúde S.M.A.R.T. vem do smartctl (por sudo NOPASSWD em /etc/sudoers.d/smartctl). " +
            "Se aparecer \"sem S.M.A.R.T.\", é porque falta essa permissão de root — o cartão do disco " +
            "mostra o comando exato para a conceder.", "sub"));
    }
}

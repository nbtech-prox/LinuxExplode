using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace LinuxExplode.Vistas;

/// <summary>Construtores de blocos da interface (cartões, linhas, grelhas) para manter as páginas legíveis.</summary>
public static class UI
{
    public static IBrush Cor(string chave)
        => Application.Current!.FindResource(chave) as IBrush ?? Brushes.White;

    public static TextBlock Texto(string texto, string classe = "", double tamanho = 0)
    {
        var t = new TextBlock { Text = Core.Textos.T(texto), TextWrapping = TextWrapping.Wrap };
        if (classe.Length > 0) t.Classes.Add(classe);
        if (tamanho > 0) t.FontSize = tamanho;
        return t;
    }

    public static Border Cartao(string? titulo, Control conteudo, bool compacto = false)
    {
        var pilha = new StackPanel { Spacing = compacto ? 8 : 12 };
        if (titulo is not null)
            pilha.Children.Add(Texto(Core.Textos.T(titulo).ToUpperInvariant(), "rotulo"));
        pilha.Children.Add(conteudo);

        var b = new Border { Child = pilha };
        b.Classes.Add(compacto ? "cartao2" : "cartao");
        return b;
    }

    /// <summary>Linha chave/valor com separador.</summary>
    public static Control Linha(string chave, string valor, bool separador = true)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
        var k = Texto(chave, "chave");
        k.VerticalAlignment = VerticalAlignment.Center;
        var v = Texto(valor, "valor");
        v.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(v, 1);
        g.Children.Add(k);
        g.Children.Add(v);

        if (!separador) return g;
        var linha = new Border { Child = g };
        linha.Classes.Add("linha");
        return linha;
    }

    /// <summary>Bloco de métrica grande: número em mono + legenda.</summary>
    public static Control Metrica(string valor, string legenda, string? extra = null)
    {
        var p = new StackPanel { Spacing = 2 };
        p.Children.Add(Texto(valor, "numero"));
        p.Children.Add(Texto(legenda, "sub"));
        if (extra is not null) p.Children.Add(Texto(extra, "mono"));
        return p;
    }

    public static Control Barra(double fracao, string? etiqueta = null)
    {
        var p = new StackPanel { Spacing = 6 };
        var pb = new ProgressBar { Minimum = 0, Maximum = 1, Value = Math.Clamp(fracao, 0, 1) };
        p.Children.Add(pb);
        if (etiqueta is not null) p.Children.Add(Texto(etiqueta, "mono"));
        return p;
    }

    public static Control Grelha(int colunas, double espacamento, params Control[] filhos)
    {
        var g = new UniformGrid { Columns = colunas };
        // UniformGrid não tem espaçamento: usa margem interna
        foreach (var f in filhos) f.Margin = new Thickness(espacamento / 2);
        foreach (var f in filhos) g.Children.Add(f);
        return g;
    }

    /// <summary>Cabeçalho de página: título, subtítulo e ações à direita.</summary>
    public static Control Cabecalho(string titulo, string subtitulo, params Control[] acoes)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 4) };
        var p = new StackPanel { Spacing = 4 };
        p.Children.Add(Texto(titulo, "titulo"));
        p.Children.Add(Texto(subtitulo, "sub"));
        g.Children.Add(p);

        if (acoes.Length > 0)
        {
            var h = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            foreach (var a in acoes) h.Children.Add(a);
            Grid.SetColumn(h, 1);
            g.Children.Add(h);
        }
        return g;
    }

    public static Button Botao(string texto, bool primario, Action aoClicar)
    {
        var b = new Button { Content = Core.Textos.T(texto) };
        b.Classes.Add(primario ? "primario" : "secundario");
        b.Click += (_, _) => aoClicar();
        return b;
    }

    public static Border Pilula(string texto, string corChave)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(28,
                ((SolidColorBrush)Cor(corChave)).Color.R,
                ((SolidColorBrush)Cor(corChave)).Color.G,
                ((SolidColorBrush)Cor(corChave)).Color.B)),
            BorderBrush = Cor(corChave),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = Core.Textos.T(texto),
                FontSize = 11,
                Foreground = Cor(corChave),
                FontFamily = (FontFamily)Application.Current!.FindResource("Mono")!
            }
        };
    }

    /// <summary>Página com cabeçalho + conteúdo em coluna, com scroll.</summary>
    public static Control Pagina(Control cabecalho, Control corpo)
    {
        var p = new StackPanel { Spacing = 14, Margin = new Thickness(26, 22, 26, 26) };
        p.Children.Add(cabecalho);
        p.Children.Add(corpo);
        return new ScrollViewer
        {
            Content = p,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Padding = new Thickness(0)
        };
    }
}

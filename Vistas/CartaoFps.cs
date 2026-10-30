using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

/// <summary>
/// Cartão "FPS · sessão do MangoHud": resumo (média, 1% low, 0,1% low) e uma sparkline de
/// frametimes. Sem MangoHud instalado diz o que fazer, em vez de mostrar números vazios.
/// </summary>
public sealed class CartaoFps : UserControl
{
    private readonly ComboBox _sessoes = new() { Width = 560, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _estado = new() { Text = "", Classes = { "sub" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _media = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _low1 = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _low01 = new() { Text = "—", Classes = { "numero" } };
    private readonly TextBlock _frametime = new() { Text = "—", Classes = { "numero" } };
    private readonly GraficoFrametimes _grafico = new() { Height = 90 };
    private readonly List<string> _ficheiros = new();
    private readonly Avalonia.Threading.DispatcherTimer _vigia = new() { Interval = TimeSpan.FromSeconds(3) };
    private string? _emCima;          // sessão actualmente mostrada
    private string? _maisRecente;     // a mais recente na altura em que se escolheu

    public CartaoFps()
    {
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Procurar sessões", true, Procurar));
        botoes.Children.Add(UI.Botao("Ver a escolhida", false, MostrarEscolhida));

        var metricas = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        void Colocar(Control c, int coluna)
        {
            c.Margin = new Thickness(0, 0, 12, 0);
            Grid.SetColumn(c, coluna);
            metricas.Children.Add(c);
        }
        Colocar(BlocoMetrica(_media, "FPS médio"), 0);
        Colocar(BlocoMetrica(_low1, Textos.T("1% low")), 1);
        Colocar(BlocoMetrica(_low01, Textos.T("0,1% low")), 2);
        Colocar(BlocoMetrica(_frametime, Textos.T("frametime médio")), 3);

        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "Lê as sessões que o MangoHud grava em CSV (layer Vulkan/OpenGL). Em Wayland é a única " +
            "forma honesta de ter FPS por aplicação — o compositor não expõe frames de outros processos.",
            "sub"));
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_sessoes);
        conteudo.Children.Add(metricas);
        conteudo.Children.Add(_grafico);
        conteudo.Children.Add(_estado);

        Content = UI.Cartao("FPS · sessão do MangoHud", conteudo);
        Procurar();

        // vigia: se aparecer uma sessão nova (jogo fechado entretanto), actualiza sozinho
        _vigia.Tick += (_, _) => Vigiar();
        _vigia.Start();
    }

    /// <summary>Bloco de métrica grande cujo valor pode ser actualizado (UI.Metrica só aceita texto).</summary>
    private static Control BlocoMetrica(TextBlock valor, string legenda)
    {
        var pilha = new StackPanel { Spacing = 2 };
        pilha.Children.Add(valor);
        pilha.Children.Add(UI.Texto(legenda, "sub"));
        return pilha;
    }

    /// <summary>Se o CSV mais recente mudou, volta a ler a lista e mostra a sessão nova.</summary>
    private void Vigiar()
    {
        var recentes = FpsMangohud.Listar(6);
        if (recentes.Count == 0) return;

        bool mudou = _ficheiros.Count != recentes.Count;
        if (!mudou)
            for (int i = 0; i < recentes.Count; i++)
                if (_ficheiros[i] != recentes[i]) { mudou = true; break; }

        if (!mudou) return;

        bool estavaNaMaisRecente = _emCima is not null && _emCima == _maisRecente;
        Preencher(recentes);
        if (estavaNaMaisRecente) Mostrar(0);
    }

    /// <summary>Enche a lista de sessões com data/hora retiradas do nome do ficheiro.</summary>
    private void Preencher(List<string> recentes)
    {
        _ficheiros.Clear();
        _sessoes.Items.Clear();

        foreach (var caminho in recentes)
        {
            _ficheiros.Add(caminho);
            _sessoes.Items.Add(FpsMangohud.Etiqueta(caminho));
        }

        _maisRecente = _ficheiros.Count > 0 ? _ficheiros[0] : null;

        var anterior = _emCima is not null ? _ficheiros.IndexOf(_emCima) : -1;
        _sessoes.SelectedIndex = anterior >= 0 ? anterior : 0;
        _sessoes.IsEnabled = _ficheiros.Count > 0;
    }

    /// <summary>Procura os CSV mais recentes (atalho: só na pasta configurada e nas habituais).</summary>
    private void Procurar()
    {
        Preencher(FpsMangohud.Listar(6));

        if (_ficheiros.Count == 0)
        {
            _estado.Text = Textos.T("Sem sessões do MangoHud. Para gravar, corre o jogo/programa assim:  ") +
                           "MANGOHUD_CONFIG=\"output_folder=$HOME/mangologs,autostart_log=2\" mangohud <programa>  " +
                           Textos.T("(e instala o mangohud). O comando `LinuxExplode --cli fps` lê o mesmo formato.");
            _sessoes.IsEnabled = false;
            return;
        }

        if (_ficheiros.Count == 0)
        {
            _estado.Text = Textos.T("Sem sessões do MangoHud. Corre o jogo com MANGOHUD=1 (Steam: nas opções de " +
                                    "lançamento) e a pasta ~/mangologs recebe um CSV por sessão; o comando " +
                                    "`LinuxExplode --cli fps` lê o mesmo formato.");
            return;
        }

        _sessoes.IsEnabled = true;
        Mostrar(_sessoes.SelectedIndex >= 0 ? _sessoes.SelectedIndex : 0);
    }

    private void MostrarEscolhida() => Mostrar(_sessoes.SelectedIndex);

    private void Mostrar(int indice)
    {
        if (indice < 0 || indice >= _ficheiros.Count) return;
        _emCima = _ficheiros[indice];
        var s = FpsMangohud.Analisar(_ficheiros[indice]);
        if (!s.Valida)
        {
            _estado.Text = Textos.T("Esse ficheiro não tem amostras válidas: ") + _ficheiros[indice];
            return;
        }

        _media.Text = $"{s.FpsMedio:0.0}";
        _low1.Text = $"{s.FpsMinimo1:0.0}";
        _low01.Text = $"{s.FpsMinimo01:0.0}";
        _frametime.Text = $"{s.FrametimeMedioMs:0.00} ms";

        _grafico.Valores = s.Frametimes;
        _grafico.InvalidateVisual();

        var partes = new List<string> { s.App };
        if (s.Sistema.Length > 0) partes.Add(s.Sistema);
        if (s.DuracaoSegundos > 0) partes.Add(Textos.TF("{0} s · {1} amostras", $"{s.DuracaoSegundos:0.0}", s.Amostras));
        if (s.GpuLoad > 0) partes.Add($"GPU {s.GpuLoad:0}%");
        if (s.GpuTemp > 0) partes.Add($"{s.GpuTemp:0} °C");
        _estado.Text = string.Join("   ·   ", partes);
    }

    /// <summary>Sparkline dos frametimes: linha cheia = série, tracejada = média, vermelho = pior.</summary>
    private sealed class GraficoFrametimes : Control
    {
        public IReadOnlyList<double> Valores { get; set; } = Array.Empty<double>();

        public override void Render(DrawingContext ctx)
        {
            var w = Bounds.Width;
            var h = Bounds.Height;
            if (w < 4 || h < 4) return;

            ctx.FillRectangle(UI.Cor("Cartao2"), new Rect(0, 0, w, h));
            if (Valores.Count < 2) return;

            // sessões sem vsync podem ter 100 mil amostras: agrega por coluna de pixels,
            // ficando com o pior frametime de cada balde (os picos são o que interessa ver)
            var pontos = new List<double>();
            int colunas = Math.Max(2, (int)w);
            if (Valores.Count > colunas * 2)
            {
                for (int c = 0; c < colunas; c++)
                {
                    int inicio = (int)((long)c * Valores.Count / colunas);
                    int fim = (int)((long)(c + 1) * Valores.Count / colunas);
                    double pior = 0;
                    for (int i = inicio; i < fim && i < Valores.Count; i++)
                        if (Valores[i] > pior) pior = Valores[i];
                    pontos.Add(pior);
                }
            }
            else pontos.AddRange(Valores);

            double minimo = double.MaxValue, maximo = 0, soma = 0;
            foreach (var v in pontos)
            {
                if (v < minimo) minimo = v;
                if (v > maximo) maximo = v;
                soma += v;
            }
            double media = soma / pontos.Count;
            double margem = Math.Max(0.5, (maximo - minimo) * 0.15);
            double fundo = Math.Max(0.1, minimo - margem);
            double topo = maximo + margem;

            double Y(double valor) => h - (valor - fundo) / (topo - fundo) * h;

            var canetaMedia = new Pen(UI.Cor("Faint"), 1, new DashStyle(new double[] { 4, 4 }, 0));
            ctx.DrawLine(canetaMedia, new Point(0, Y(media)), new Point(w, Y(media)));

            var geometria = new StreamGeometry();
            using (var g = geometria.Open())
            {
                bool primeiro = true;
                for (int i = 0; i < pontos.Count; i++)
                {
                    var p = new Point(w * i / (pontos.Count - 1), Y(pontos[i]));
                    if (primeiro) { g.BeginFigure(p, false); primeiro = false; }
                    else g.LineTo(p);
                }
                g.EndFigure(false);
            }

            bool estavel = maximo <= media * 1.5;
            ctx.DrawGeometry(null, new Pen(UI.Cor(estavel ? "Bom" : "Atencao"), 1.4), geometria);
        }
    }
}

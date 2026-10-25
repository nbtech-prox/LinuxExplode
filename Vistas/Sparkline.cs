using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LinuxExplode.Vistas;

/// <summary>
/// Gráfico de linha simples (últimos N valores) desenhado à mão: sem dependências, sem eixos, só a
/// forma da série com o mínimo e o máximo à vista.
/// </summary>
public sealed class Sparkline : Control
{
    private readonly double[] _valores;
    private int _proximo;
    private int _contagem;

    public Sparkline(int amostras = 60)
    {
        _valores = new double[Math.Max(2, amostras)];
        Height = 56;
    }

    /// <summary>Cor da linha (chave da paleta). Fica vermelha acima de <see cref="Limite"/>.</summary>
    public string Cor { get; set; } = "Fg";

    /// <summary>Valor a partir do qual a linha muda para "Mau" (ex.: 90 °C). 0 = sem limite.</summary>
    public double Limite { get; set; }

    /// <summary>Máximo fixo da escala (0 = automático pelos valores).</summary>
    public double EscalaMaxima { get; set; }

    public void Adicionar(double valor)
    {
        if (double.IsNaN(valor) || double.IsInfinity(valor)) return;
        _valores[_proximo] = valor;
        _proximo = (_proximo + 1) % _valores.Length;
        _contagem = Math.Min(_contagem + 1, _valores.Length);
        InvalidateVisual();
    }

    public double Ultimo => _contagem == 0 ? 0 : _valores[(_proximo - 1 + _valores.Length) % _valores.Length];

    public double MaximoSerie
    {
        get
        {
            double m = double.MinValue;
            for (int i = 0; i < _contagem; i++) m = Math.Max(m, _valores[i]);
            return _contagem == 0 ? 0 : m;
        }
    }

    public double MinimoSerie
    {
        get
        {
            double m = double.MaxValue;
            for (int i = 0; i < _contagem; i++) m = Math.Min(m, _valores[i]);
            return _contagem == 0 ? 0 : m;
        }
    }

    public override void Render(DrawingContext contexto)
    {
        var area = new Rect(Bounds.Size);
        if (area.Width < 4 || area.Height < 4) return;

        // linha de base
        var canetaBase = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
        contexto.DrawLine(canetaBase, new Point(0, area.Height - 0.5), new Point(area.Width, area.Height - 0.5));

        if (_contagem < 2) return;

        double max = EscalaMaxima > 0 ? EscalaMaxima : MaximoSerie;
        double min = EscalaMaxima > 0 ? 0 : MinimoSerie;
        if (max - min < 0.0001) { max = min + 1; }

        double altura = area.Height - 3;
        double passo = area.Width / Math.Max(1, _valores.Length - 1);

        var geometria = new StreamGeometry();
        using (var c = geometria.Open())
        {
            bool primeiro = true;
            for (int i = 0; i < _contagem; i++)
            {
                int indice = (_proximo + _valores.Length - _contagem + i) % _valores.Length;
                double x = i * passo;
                double y = altura - (Math.Clamp(_valores[indice], min, max) - min) / (max - min) * altura + 1;

                if (primeiro) { c.BeginFigure(new Point(x, y), isFilled: false); primeiro = false; }
                else c.LineTo(new Point(x, y));
            }
            c.EndFigure(false);
        }

        bool acimaDoLimite = Limite > 0 && Ultimo >= Limite;
        string chave = acimaDoLimite ? "Mau" : Cor;
        var cor = UI.Cor(chave);

        contexto.DrawGeometry(null, new Pen(cor, 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometria);

        // ponto final, para se ver onde está agora
        int ultimoIndice = (_proximo - 1 + _valores.Length) % _valores.Length;
        double xf = (_contagem - 1) * passo;
        double yf = altura - (Math.Clamp(_valores[ultimoIndice], min, max) - min) / (max - min) * altura + 1;
        contexto.DrawEllipse(cor, null, new Point(xf - 1, yf), 2.5, 2.5);
    }
}

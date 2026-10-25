using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

/// <summary>
/// Curva de latência de memória: eixo X logarítmico (4 KB → 256 MB) e Y linear em nanossegundos.
/// Desenhada à mão como o <see cref="Sparkline"/> — sem dependências e com os valores reais de cada
/// ponto à vista, porque a forma da curva (os patamares das caches) é a informação.
/// </summary>
public sealed class CurvaLatencia : Control
{
    private IReadOnlyList<LatenciaBloco> _pontos = Array.Empty<LatenciaBloco>();

    public CurvaLatencia()
    {
        Height = 170;
    }

    public void Definir(IReadOnlyList<LatenciaBloco> pontos)
    {
        _pontos = pontos;
        InvalidateVisual();
    }

    public override void Render(DrawingContext contexto)
    {
        var area = new Rect(Bounds.Size);
        if (area.Width < 40 || area.Height < 40) return;

        double esquerda = 42, direita = 10, topo = 8, fundo = 26;
        double largura = Math.Max(10, area.Width - esquerda - direita);
        double altura = Math.Max(10, area.Height - topo - fundo);

        var canetaGrelha = new Pen(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), 1);
        var canetaCurva = new Pen(UI.Cor("Accent"), 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var canetaTexto = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));

        if (_pontos.Count < 2)
        {
            contexto.DrawText(Texto("sem medições", 11, canetaTexto), new Point(esquerda, topo + altura / 2));
            return;
        }

        double minNs = _pontos.Min(p => p.Ns);
        double maxNs = _pontos.Max(p => p.Ns);
        if (maxNs - minNs < 0.01) maxNs = minNs + 1;

        // três linhas de referência (mínimo, meio, máximo) com o valor em ns
        for (int i = 0; i <= 2; i++)
        {
            double fracao = i / 2.0;
            double y = topo + altura - fracao * altura;
            double valor = minNs + fracao * (maxNs - minNs);

            contexto.DrawLine(canetaGrelha, new Point(esquerda, y), new Point(esquerda + largura, y));
            contexto.DrawText(Texto($"{valor:0.#} ns", 10, canetaTexto), new Point(2, y - 7));
        }

        // X: log2 do tamanho, para 4 KB e 256 MB ficarem à mesma distância relativa que 4→16 e 16→64
        double logMin = Math.Log2(_pontos.Min(p => p.Bytes));
        double logMax = Math.Log2(_pontos.Max(p => p.Bytes));
        if (logMax - logMin < 0.001) logMax = logMin + 1;

        double X(LatenciaBloco p) => esquerda + (Math.Log2(p.Bytes) - logMin) / (logMax - logMin) * largura;
        double Y(LatenciaBloco p) => topo + altura - (p.Ns - minNs) / (maxNs - minNs) * altura;

        var geometria = new StreamGeometry();
        using (var c = geometria.Open())
        {
            c.BeginFigure(new Point(X(_pontos[0]), Y(_pontos[0])), isFilled: false);
            foreach (var p in _pontos.Skip(1)) c.LineTo(new Point(X(p), Y(p)));
            c.EndFigure(false);
        }
        contexto.DrawGeometry(null, canetaCurva, geometria);

        foreach (var p in _pontos)
            contexto.DrawEllipse(UI.Cor("Accent"), null, new Point(X(p), Y(p)), 2.4, 2.4);

        // etiquetas de X: primeiro, meio e último tamanhos
        var marcas = new[] { _pontos[0], _pontos[_pontos.Count / 2], _pontos[^1] };
        foreach (var p in marcas.Distinct())
        {
            var t = Texto(p.Tamanho, 10, canetaTexto);
            contexto.DrawText(t, new Point(X(p) - t.Width / 2, topo + altura + 6));
        }

        // nomes das fases por cima de cada patamar
        foreach (var grupo in _pontos.GroupBy(p => p.Fase))
        {
            var primeiro = grupo.First();
            var t = Texto(grupo.Key, 10, new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)));
            contexto.DrawText(t, new Point(X(primeiro) - t.Width / 2, topo + altura + 15));
        }
    }

    private static FormattedText Texto(string texto, double tamanho, IBrush cor) =>
        new(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), tamanho, cor);
}

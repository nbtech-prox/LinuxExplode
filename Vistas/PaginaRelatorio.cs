using System.Diagnostics;
using Avalonia.Controls;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaRelatorio : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };
    private readonly TextBlock _estado = new() { Text = Textos.T("pronto"), Classes = { "sub" } };
    private readonly TextBlock _caminho = new() { Text = "", Classes = { "mono" } };
    private readonly Button _abrir;
    private string? _ultimoFicheiro;

    public PaginaRelatorio()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "Gera um relatório com tudo o que a aplicação mediu: sistema, CPU, memória, GPUs, discos " +
            "(S.M.A.R.T. e auto-teste), sensores, ranking de benchmarks, o último stress/ciclos/VRAM e a " +
            "última sessão gravada. O HTML é autónomo — CSS e gráficos SVG embutidos, sem rede — e abre em " +
            "qualquer browser. O JSON serve para automação.", "sub"));

        _abrir = UI.Botao("Abrir", false, Abrir);
        _abrir.IsEnabled = false;

        var botoes = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Gerar HTML completo", true, () => _ = Gerar("html")));
        botoes.Children.Add(UI.Botao("Gerar JSON", false, () => _ = Gerar("json")));
        botoes.Children.Add(_abrir);
        botoes.Children.Add(UI.Botao("Abrir pasta", false, AbrirPasta));
        conteudo.Children.Add(botoes);

        conteudo.Children.Add(_estado);
        conteudo.Children.Add(_caminho);

        _corpo.Children.Add(UI.Cartao("Exportar", conteudo));
        _corpo.Children.Add(UI.Cartao("O que vai no relatório", Detalhe(), true));

        Content = UI.Pagina(UI.Cabecalho("Relatório", "Exportar a informação do sistema e o histórico de medições"), _corpo);
    }

    private static Control Detalhe()
    {
        var p = new StackPanel { Spacing = 0 };
        p.Children.Add(UI.Linha("Sistema", "distribuição, kernel, arquitetura, uptime, placa-mãe, BIOS"));
        p.Children.Add(UI.Linha("CPU", "modelo, núcleos, threads, frequências, cache L3"));
        p.Children.Add(UI.Linha("Memória", "total, disponível, swap"));
        p.Children.Add(UI.Linha("GPUs", "modelo, barramento PCI, driver, VRAM, temperatura"));
        p.Children.Add(UI.Linha("Discos", "modelo, tamanho, partições, ocupação, S.M.A.R.T. e auto-teste"));
        p.Children.Add(UI.Linha("Sensores", "todas as leituras de hwmon"));
        p.Children.Add(UI.Linha("Benchmarks", "ranking local com mediana, melhor, última e variação, mais as últimas 15 corridas"));
        p.Children.Add(UI.Linha("Stress", "último CPU/RAM/GPU/disco, série de ciclos agregada e enchimento de VRAM"));
        p.Children.Add(UI.Linha("Sessão", "resumo da última sessão gravada, com gráficos de CPU, GPU, potência e frequência", false));
        return p;
    }

    private static string Pasta()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(docs) || !Directory.Exists(docs))
            docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documentos");
        Directory.CreateDirectory(docs);
        return docs;
    }

    private async Task Gerar(string formato)
    {
        _estado.Text = Textos.T("a recolher dados e histórico…");
        try
        {
            var (s, sensores, discos) = await Task.Run(() =>
                (Sistema.Recolher(), Sensores.Ler(), Discos.Ler()));

            // o histórico é do mesmo processo: ler depois dos sensores não o torna mais velho
            var nome = $"linux-explode-relatorio-{DateTime.Now:yyyyMMdd-HHmmss}.{formato}";
            var caminho = Path.Combine(Pasta(), nome);
            var texto = formato == "json"
                ? Relatorio.GerarJson(s, sensores, discos)
                : Relatorio.GerarHtml(s, sensores, discos);

            await File.WriteAllTextAsync(caminho, texto);
            _ultimoFicheiro = caminho;
            _abrir.IsEnabled = true;
            _caminho.Text = caminho;
            _estado.Text = Textos.TF("gravado ({0} KB)", $"{new FileInfo(caminho).Length / 1024.0:0.0}") +
                           (formato == "html" ? Textos.T(" — relatório completo") : "");
        }
        catch (Exception ex)
        {
            _estado.Text = Textos.T("falhou: ") + ex.Message;
        }
    }

    private void Abrir()
    {
        if (_ultimoFicheiro is null) return;
        try { Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { _ultimoFicheiro } }); }
        catch (Exception ex) { _estado.Text = Textos.T("não foi possível abrir: ") + ex.Message; }
    }

    private void AbrirPasta()
    {
        try { Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { Pasta() } }); }
        catch (Exception ex) { _estado.Text = Textos.T("não foi possível abrir: ") + ex.Message; }
    }
}

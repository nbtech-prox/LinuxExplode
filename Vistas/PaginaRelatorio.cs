using System.Diagnostics;
using Avalonia.Controls;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaRelatorio : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };
    private readonly TextBlock _estado = new() { Text = "pronto", Classes = { "sub" } };
    private readonly TextBlock _caminho = new() { Text = "", Classes = { "mono" } };

    public PaginaRelatorio()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "Gera um relatório com sistema, CPU, memória, GPUs, discos (com S.M.A.R.T.) e sensores. " +
            "O HTML fica com a mesma linguagem visual do app; o JSON serve para automação.", "sub"));

        var botoes = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(UI.Botao("Gerar HTML", true, () => _ = Gerar("html")));
        botoes.Children.Add(UI.Botao("Gerar JSON", false, () => _ = Gerar("json")));
        botoes.Children.Add(UI.Botao("Abrir pasta", false, AbrirPasta));
        conteudo.Children.Add(botoes);

        conteudo.Children.Add(_estado);
        conteudo.Children.Add(_caminho);

        _corpo.Children.Add(UI.Cartao("Exportar", conteudo));
        _corpo.Children.Add(UI.Cartao("O que vai no relatório", Detalhe(), true));

        Content = UI.Pagina(UI.Cabecalho("Relatório", "Exportar a informação do sistema"), _corpo);
    }

    private static Control Detalhe()
    {
        var p = new StackPanel { Spacing = 0 };
        p.Children.Add(UI.Linha("Sistema", "distribuição, kernel, arquitetura, uptime, placa-mãe, BIOS"));
        p.Children.Add(UI.Linha("CPU", "modelo, núcleos, threads, frequências, cache L3"));
        p.Children.Add(UI.Linha("Memória", "total, disponível, swap"));
        p.Children.Add(UI.Linha("GPUs", "modelo, barramento PCI, driver, VRAM, temperatura"));
        p.Children.Add(UI.Linha("Discos", "modelo, tamanho, partições, ocupação, S.M.A.R.T."));
        p.Children.Add(UI.Linha("Sensores", "todas as leituras de hwmon", false));
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
        _estado.Text = "a recolher dados…";
        try
        {
            var (s, sensores, discos) = await Task.Run(() =>
                (Sistema.Recolher(), Sensores.Ler(), Discos.Ler()));

            var nome = $"linux-explode-{DateTime.Now:yyyyMMdd-HHmmss}.{formato}";
            var caminho = Path.Combine(Pasta(), nome);
            var texto = formato == "json"
                ? Relatorio.GerarJson(s, sensores, discos)
                : Relatorio.GerarHtml(s, sensores, discos);

            await File.WriteAllTextAsync(caminho, texto);
            _caminho.Text = caminho;
            _estado.Text = $"gravado ({new FileInfo(caminho).Length / 1024.0:0.0} KB)";
        }
        catch (Exception ex)
        {
            _estado.Text = "falhou: " + ex.Message;
        }
    }

    private void AbrirPasta()
    {
        try { Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { Pasta() } }); }
        catch (Exception ex) { _estado.Text = "não foi possível abrir: " + ex.Message; }
    }
}

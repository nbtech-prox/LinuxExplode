using Avalonia.Controls;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaRede : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };
    private readonly StackPanel _resultado = new() { Spacing = 0 };
    private readonly TextBlock _estado = new() { Text = "pronto para testar", Classes = { "sub" } };

    public PaginaRede()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "Teste de ~15 s: latência TCP até ao router e à internet, jitter, perda de pacotes, " +
            "tempo de DNS e velocidade de download/upload (servidores da Cloudflare).", "sub"));
        conteudo.Children.Add(UI.Texto(Rede.Interfaces(), "mono"));
        conteudo.Children.Add(UI.Botao("Testar rede", true, async () => await Testar()));
        conteudo.Children.Add(_estado);
        conteudo.Children.Add(_resultado);

        _corpo.Children.Add(UI.Cartao("Diagnóstico de rede", conteudo));
        Content = UI.Pagina(UI.Cabecalho("Rede", "Latência, jitter, perda e velocidade"), _corpo);
    }

    private async Task Testar()
    {
        _resultado.Children.Clear();
        _estado.Text = "a testar…";
        try
        {
            var r = await Task.Run(() => Rede.Correr(t => Dispatcher.UIThread.Post(() => _estado.Text = t)));

            var vereditoCor = r.Veredito switch
            {
                "Estável" => "Bom",
                "Rede local" or "Instável" or "Latência alta" => "Atencao",
                _ => "Mau"
            };

            var cab = new StackPanel { Spacing = 4 };
            var linha = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            linha.Children.Add(UI.Texto(r.Veredito, "valor", 18));
            var pilula = UI.Pilula(r.Detalhe, vereditoCor);
            Grid.SetColumn(pilula, 1);
            linha.Children.Add(pilula);
            cab.Children.Add(linha);
            _resultado.Children.Add(cab);

            foreach (var a in r.Alvos)
            {
                var linhaAlvo = new StackPanel { Spacing = 2 };
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                g.Children.Add(UI.Texto(a.Nome, "chave"));
                var v = UI.Texto($"{a.Media:0.0} ms", "valor");
                v.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
                Grid.SetColumn(v, 1);
                g.Children.Add(v);
                linhaAlvo.Children.Add(g);
                linhaAlvo.Children.Add(UI.Texto(
                    $"min {a.Min:0.0} · max {a.Max:0.0} ms · jitter {a.Jitter:0.0} ms · perda {a.Perda:0}%", "mono"));
                var borda = new Border { Child = linhaAlvo, Margin = new Avalonia.Thickness(0, 8, 0, 0) };
                borda.Classes.Add("linha");
                _resultado.Children.Add(borda);
            }

            _resultado.Children.Add(UI.Linha("DNS (github.com)", $"{r.DnsMs:0} ms"));
            _resultado.Children.Add(UI.Linha("Download", r.DownloadMBs > 0 ? $"{r.DownloadMBs:0.0} MB/s" : "sem dados"));
            _resultado.Children.Add(UI.Linha("Upload", r.UploadMBs > 0 ? $"{r.UploadMBs:0.0} MB/s" : "sem dados", false));

            _estado.Text = $"medição concluída às {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _estado.Text = "falhou: " + ex.Message;
        }
    }
}

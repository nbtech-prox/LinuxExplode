using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

/// <summary>
/// Rede: diagnóstico clássico (latência, jitter, perda, DNS e velocidade pela Cloudflare) e o débito
/// real entre dois processos da aplicação — um serve, o outro mede. O segundo não depende de terceiros.
/// </summary>
public sealed class PaginaRede : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };
    private readonly StackPanel _resultado = new() { Spacing = 0 };
    private readonly TextBlock _estado = new() { Text = Textos.T("pronto para testar"), Classes = { "sub" } };

    // débito real (cliente/servidor)
    private readonly ComboBox _nbModo = new() { Width = 260 };
    private readonly TextBox _nbHost = new() { Text = Textos.T("127.0.0.1"), Width = 200 };
    private readonly TextBox _nbPorta = new() { Text = BenchRede.PortaPadrao.ToString(), Width = 110 };
    private readonly ComboBox _nbMb = new() { Width = 130 };
    private readonly TextBlock _nbEstado = new() { Text = Textos.T("pronto"), Classes = { "mono" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _nbResultado = new() { Spacing = 2 };
    private readonly List<Button> _nbBotoes = new();
    private CancellationTokenSource? _nbCancelar;
    private bool _nbConcluido;

    public PaginaRede()
    {
        var diagnostico = new StackPanel { Spacing = 12 };
        diagnostico.Children.Add(UI.Texto(
            "Teste de ~15 s: latência TCP até ao router e à internet, jitter, perda de pacotes, " +
            "tempo de DNS e velocidade de download/upload (servidores da Cloudflare).", "sub"));
        diagnostico.Children.Add(UI.Texto(Rede.Interfaces(), "mono"));
        diagnostico.Children.Add(UI.Botao("Testar rede", true, async () => await Testar()));
        diagnostico.Children.Add(_estado);
        diagnostico.Children.Add(_resultado);

        // gancho de captura: LINUXEXPLODE_SECAO=netbench deixa só o cartão do débito real
        var secao = (Environment.GetEnvironmentVariable("LINUXEXPLODE_SECAO") ?? "").ToLowerInvariant();
        if (secao != "netbench") _corpo.Children.Add(UI.Cartao("Diagnóstico de rede", diagnostico));
        _corpo.Children.Add(CartaoNetbench());

        Content = UI.Pagina(UI.Cabecalho("Rede", "Latência, jitter, perda, velocidade e débito real"), _corpo);

        Loaded += (_, _) =>
        {
            if ((Environment.GetEnvironmentVariable("LINUXEXPLODE_AUTOTEST") ?? "")
                .Equals("netbench", StringComparison.OrdinalIgnoreCase))
                _ = IniciarNetbench();
        };
    }

    // ------------------------------------------------------------------ débito real

    private Control CartaoNetbench()
    {
        _nbModo.Items.Add(Textos.T("Cliente (medir até outro PC)"));
        _nbModo.Items.Add(Textos.T("Servidor (este PC a servir)"));
        _nbModo.SelectedIndex = 0;

        _nbMb.Items.Add("8 MB");
        _nbMb.Items.Add("32 MB");
        _nbMb.Items.Add("64 MB");
        _nbMb.Items.Add("128 MB");
        _nbMb.SelectedIndex = 1;

        var iniciar = UI.Botao("Iniciar", true, () => _ = IniciarNetbench());
        var parar = UI.Botao("Parar", false, PararNetbench);
        parar.IsEnabled = false;
        _nbBotoes.Add(iniciar);
        _nbBotoes.Add(parar);

        var acoes = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var b in _nbBotoes)
        {
            b.Margin = new Thickness(0, 0, 8, 8);
            acoes.Children.Add(b);
        }

        var grelha = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*,*,*") };
        var celulas = new Control[]
        {
            Campo("Modo", _nbModo), Campo("PC servidor", _nbHost),
            Campo("Porta", _nbPorta), Campo("Tamanho", _nbMb)
        };
        for (int i = 0; i < celulas.Length; i++)
        {
            celulas[i].Margin = new Thickness(6);
            Grid.SetColumn(celulas[i], i);
            grelha.Children.Add(celulas[i]);
        }

        var pilha = new StackPanel { Spacing = 12 };
        pilha.Children.Add(UI.Texto(
            "Mede o débito entre dois PC com a própria aplicação, sem servidores de terceiros: um lado " +
            "corre “Servidor” e o outro “Cliente” (ou --cli netbench). O cliente envia N MB e recebe os " +
            "mesmos N MB de volta; a latência vem de 20 idas e voltas de 8 bytes. Os dois sentidos são " +
            "cronometrados e contados em bytes — o que aparece é o que passou.", "sub"));
        pilha.Children.Add(grelha);
        pilha.Children.Add(acoes);
        pilha.Children.Add(_nbEstado);
        pilha.Children.Add(_nbResultado);

        return UI.Cartao("Débito real — cliente/servidor", pilha);
    }

    private static Control Campo(string rotulo, Control controlo)
    {
        var p = new StackPanel { Spacing = 4 };
        p.Children.Add(UI.Texto(Textos.T(rotulo).ToUpperInvariant(), "rotulo"));
        p.Children.Add(controlo);
        return p;
    }

    private async Task IniciarNetbench()
    {
        if (_nbCancelar is not null) return;

        int porta = int.TryParse(_nbPorta.Text, out int p) && p is > 0 and < 65536 ? p : BenchRede.PortaPadrao;
        bool servidor = _nbModo.SelectedIndex == 1;

        _nbCancelar = new CancellationTokenSource();
        var ct = _nbCancelar.Token;
        _nbConcluido = false;
        _nbResultado.Children.Clear();

        _nbBotoes[0].IsEnabled = false;
        _nbBotoes[1].IsEnabled = true;

        try
        {
            if (servidor)
            {
                _nbEstado.Text = Textos.TF("a escutar na porta {0} — inicia o cliente no outro PC…", porta);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await BenchRede.ServidorAsync(porta,
                            m => Dispatcher.UIThread.Post(() => _nbEstado.Text = m), umaVez: false, ct);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Dispatcher.UIThread.Post(() => _nbEstado.Text = Textos.T("servidor falhou: ") + ex.Message);
                    }
                    Dispatcher.UIThread.Post(TerminarNetbench);
                });
                return;
            }

            string host = string.IsNullOrWhiteSpace(_nbHost.Text) ? "127.0.0.1" : _nbHost.Text!.Trim();
            int mb = _nbMb.SelectedIndex switch { 0 => 8, 2 => 64, 3 => 128, _ => 32 };

            var r = await Task.Run(() => BenchRede.Cliente(host, porta, mb,
                t => Dispatcher.UIThread.Post(() => { if (!_nbConcluido) _nbEstado.Text = t; }), ct), ct);

            MostrarNetbench(r);
        }
        catch (OperationCanceledException)
        {
            _nbEstado.Text = Textos.T("parado pelo utilizador");
        }
        catch (Exception ex)
        {
            _nbEstado.Text = Textos.T("falhou: ") + ex.Message;
        }
        finally
        {
            if (!servidor) TerminarNetbench();
        }
    }

    private void MostrarNetbench(ResultadoRedeBench r)
    {
        _nbConcluido = true;
        _nbEstado.Text = Textos.TF("{0} · {1} em cada sentido · {2} s", r.Destino, Util.Bytes(r.Bytes), $"{r.Segundos:0.00}");
        _nbResultado.Children.Clear();
        _nbResultado.Children.Add(UI.Linha("Subida", $"{r.SubidaMbs:0.0} MB/s"));
        _nbResultado.Children.Add(UI.Linha("Descida", $"{r.DescidaMbs:0.0} MB/s"));
        _nbResultado.Children.Add(UI.Linha("Latência", Textos.TF("{0} ms · jitter {1} ms", $"{r.LatenciaMs:0.00}", $"{r.JitterMs:0.00}"), false));
        foreach (var l in r.Linhas) _nbResultado.Children.Add(UI.Texto("· " + l, "mono"));
    }

    private void PararNetbench()
    {
        if (_nbCancelar is null) return;
        _nbCancelar.Cancel();
        _nbEstado.Text = Textos.T("a parar…");
    }

    private void TerminarNetbench()
    {
        _nbCancelar?.Dispose();
        _nbCancelar = null;
        _nbBotoes[0].IsEnabled = true;
        _nbBotoes[1].IsEnabled = false;
    }

    // ------------------------------------------------------------------ diagnóstico

    private async Task Testar()
    {
        _resultado.Children.Clear();
        _estado.Text = Textos.T("a testar…");
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
            linha.Children.Add(UI.Texto(Textos.T(r.Veredito), "valor", 18));
            var pilula = UI.Pilula(r.Detalhe, vereditoCor);
            Grid.SetColumn(pilula, 1);
            linha.Children.Add(pilula);
            cab.Children.Add(linha);
            _resultado.Children.Add(cab);

            foreach (var a in r.Alvos)
            {
                var linhaAlvo = new StackPanel { Spacing = 2 };
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                g.Children.Add(UI.Texto(Textos.T(a.Nome), "chave"));
                var v = UI.Texto($"{a.Media:0.0} ms", "valor");
                v.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(v, 1);
                g.Children.Add(v);
                linhaAlvo.Children.Add(g);
                linhaAlvo.Children.Add(UI.Texto(
                    Textos.TF("min {0} · max {1} ms · jitter {2} ms · perda {3}%", $"{a.Min:0.0}", $"{a.Max:0.0}", $"{a.Jitter:0.0}", $"{a.Perda:0}"), "mono"));
                var borda = new Border { Child = linhaAlvo, Margin = new Thickness(0, 8, 0, 0) };
                borda.Classes.Add("linha");
                _resultado.Children.Add(borda);
            }

            _resultado.Children.Add(UI.Linha("DNS (github.com)", $"{r.DnsMs:0} ms"));
            _resultado.Children.Add(UI.Linha("Download", r.DownloadMBs > 0 ? $"{r.DownloadMBs:0.0} MB/s" : "sem dados"));
            _resultado.Children.Add(UI.Linha("Upload", r.UploadMBs > 0 ? $"{r.UploadMBs:0.0} MB/s" : "sem dados", false));

            _estado.Text = Textos.TF("medição concluída às {0}", $"{DateTime.Now:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            _estado.Text = Textos.T("falhou: ") + ex.Message;
        }
    }
}

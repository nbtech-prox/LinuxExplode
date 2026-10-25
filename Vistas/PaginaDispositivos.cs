using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

/// <summary>
/// Dispositivos: links PCI Express, dispositivos USB e áudio (placas, fluxos e xruns do PipeWire).
/// Os links e o USB releem-se a cada 2 s (sysfs é barato); o áudio a cada 6 s porque o <c>pw-top</c>
/// corre um processo e demora ~2 s.
/// </summary>
public sealed class PaginaDispositivos : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };
    private readonly StackPanel _pci = new() { Spacing = 0 };
    private readonly TextBlock _pciResumo = new() { Classes = { "sub" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _pciNota = new() { Classes = { "mono" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Border _cartaoPci;
    private readonly CheckBox _mostrarPontes = new() { Content = "Mostrar as pontes", FontSize = 12, IsChecked = false };

    private readonly StackPanel _usb = new() { Spacing = 0 };
    private readonly TextBlock _usbResumo = new() { Classes = { "sub" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Border _cartaoUsb;

    private readonly StackPanel _audio = new() { Spacing = 0 };
    private readonly TextBlock _audioResumo = new() { Classes = { "sub" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _audioNota = new() { Spacing = 4 };
    private readonly Border _cartaoAudio;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private int _batidas;
    private bool _aCarregar;
    private string _assinaturaPci = "";
    private string _assinaturaUsb = "";
    private string _assinaturaAudio = "";

    public PaginaDispositivos()
    {
        var cabecalho = UI.Cabecalho("Dispositivos",
            "/sys/bus/pci, /sys/bus/usb e /proc/asound — sem root, sem ferramentas externas");

        var pci = new StackPanel { Spacing = 8 };
        var linhaResumo = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        linhaResumo.Children.Add(_pciResumo);
        _mostrarPontes.VerticalAlignment = VerticalAlignment.Center;
        _mostrarPontes.Margin = new Avalonia.Thickness(12, 0, 0, 0);
        Avalonia.Controls.ToolTip.SetTip(_mostrarPontes,
            "As pontes mostram o link que estão a servir, não um máximo comparável — ficam escondidas para a tabela respirar");
        Grid.SetColumn(_mostrarPontes, 1);
        linhaResumo.Children.Add(_mostrarPontes);
        _mostrarPontes.IsCheckedChanged += (_, _) => { _assinaturaPci = ""; _ = Atualizar(comAudio: false); };
        pci.Children.Add(linhaResumo);
        pci.Children.Add(_pci);
        pci.Children.Add(_pciNota);
        _cartaoPci = UI.Cartao("PCI Express — link em uso vs máximo anunciado", pci);

        var usb = new StackPanel { Spacing = 8 };
        usb.Children.Add(_usbResumo);
        usb.Children.Add(_usb);
        _cartaoUsb = UI.Cartao("USB — o que está ligado e a que velocidade", usb);

        var audio = new StackPanel { Spacing = 8 };
        audio.Children.Add(_audioResumo);
        audio.Children.Add(_audio);
        audio.Children.Add(_audioNota);
        _cartaoAudio = UI.Cartao("Áudio — placas, fluxos e xruns do PipeWire", audio);

        _corpo.Children.Add(_cartaoPci);
        _corpo.Children.Add(_cartaoUsb);
        _corpo.Children.Add(_cartaoAudio);

        // gancho de teste/depuração: mostrar só uma secção (LINUXEXPLODE_DISPOSITIVOS=usb|audio|pci)
        switch ((Environment.GetEnvironmentVariable("LINUXEXPLODE_DISPOSITIVOS") ?? "").ToLowerInvariant())
        {
            case "pci":
                _corpo.Children.Remove(_cartaoUsb);
                _corpo.Children.Remove(_cartaoAudio);
                break;
            case "usb":
                _corpo.Children.Remove(_cartaoPci);
                _corpo.Children.Remove(_cartaoAudio);
                break;
            case "audio" or "áudio":
                _corpo.Children.Remove(_cartaoPci);
                _corpo.Children.Remove(_cartaoUsb);
                break;
        }

        Content = UI.Pagina(cabecalho, _corpo);

        _timer.Tick += async (_, _) =>
        {
            _batidas++;
            await Atualizar(comAudio: _batidas % 3 == 0);
        };
        Loaded += async (_, _) => { _timer.Start(); await Atualizar(comAudio: true); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private async Task Atualizar(bool comAudio)
    {
        if (_aCarregar) return;
        _aCarregar = true;
        try
        {
            var links = await Task.Run(Placa.Links);
            var dispositivos = await Task.Run(() => Usb.Listar());
            LeituraAudio? audio = comAudio ? await Task.Run(() => Core.Audio.Ler()) : null;

            MostrarPci(links);
            MostrarUsb(dispositivos);
            if (audio is not null) MostrarAudio(audio);
        }
        catch
        {
            // leitura falhou — fica o último estado desenhado
        }
        finally { _aCarregar = false; }
    }

    // ------------------------------------------------------------------ PCI Express

    private void MostrarPci(List<LinkPcie> links)
    {
        var finais = links.Where(l => !l.Ponte).ToList();
        var pontes = links.Where(l => l.Ponte).ToList();
        var reduzidos = finais.Where(l => l.Reduzido).ToList();

        var verPontes = _mostrarPontes.IsChecked == true;
        _mostrarPontes.Content = pontes.Count > 0 ? $"Mostrar as pontes ({pontes.Count})" : "Sem pontes";
        _mostrarPontes.IsEnabled = pontes.Count > 0;

        _pciResumo.Text = links.Count == 0
            ? "Nenhum link PCI Express legível em /sys/bus/pci/devices."
            : $"{finais.Count} dispositivo(s) final(is) e {pontes.Count} ponte(s). " +
              (reduzidos.Count == 0
                  ? "Todos os dispositivos finais negociaram o máximo que anunciam."
                  : $"{reduzidos.Count} dispositivo(s) abaixo do máximo anunciado (assinalados).");

        // a assinatura inclui o toggle: ligar/desligar as pontes volta a desenhar a tabela
        var assinatura = string.Join("|", finais.Select(l => l.Endereco + l.TextoLink + l.Nome)) +
                         "#" + (verPontes ? string.Join("|", pontes.Select(p => p.Endereco + p.TextoLink)) : "sem-pontes");

        if (assinatura != _assinaturaPci)
        {
            _assinaturaPci = assinatura;
            _pci.Children.Clear();

            foreach (var l in finais)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("92,126,142,*,108"), Margin = new Avalonia.Thickness(0, 1) };

                var endereco = UI.Texto(l.Endereco, "mono");
                endereco.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                endereco.VerticalAlignment = VerticalAlignment.Center;

                var link = UI.Texto(l.TextoLink, "valor");
                link.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                link.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(link, 1);

                var classe = UI.Texto(l.Classe, "rotulo");
                classe.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                classe.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
                classe.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(classe, 2);

                var nome = UI.Texto(l.Nome, "chave");
                nome.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                nome.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
                nome.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(nome, 3);

                var banda = UI.Texto(l.BandaTeoricaGbS is double b ? $"≈ {Placa.Numero(b)} GB/s" : "—", "mono");
                banda.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                banda.TextAlignment = Avalonia.Media.TextAlignment.Right;
                banda.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(banda, 4);

                g.Children.Add(endereco);
                g.Children.Add(link);
                g.Children.Add(classe);
                g.Children.Add(nome);
                g.Children.Add(banda);

                var linha = new Border { Child = g };
                linha.Classes.Add("linha");
                _pci.Children.Add(linha);

                if (l.Reduzido)
                {
                    var nota = UI.Texto($"anuncia {l.TextoMaximo} — o link em uso é mais estreito/lento " +
                                        "(slot, riser ou poupança de energia do driver)", "sub");
                    nota.Margin = new Avalonia.Thickness(96, 2, 0, 8);
                    _pci.Children.Add(nota);
                }
            }

            if (verPontes && pontes.Count > 0)
            {
                var titulo = UI.Texto("pontes (mostram o link que estão a servir)", "rotulo");
                titulo.Margin = new Avalonia.Thickness(0, 10, 0, 4);
                _pci.Children.Add(titulo);

                foreach (var p in pontes)
                {
                    var g = new Grid { ColumnDefinitions = new ColumnDefinitions("92,126,*"), Margin = new Avalonia.Thickness(0, 1) };
                    var endereco = UI.Texto(p.Endereco, "mono");
                    endereco.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                    endereco.VerticalAlignment = VerticalAlignment.Center;
                    var nome = UI.Texto(p.Nome, "chave");
                    nome.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                    nome.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
                    nome.VerticalAlignment = VerticalAlignment.Center;
                    var link = UI.Texto(p.TextoLink, "valor");
                    link.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
                    link.TextAlignment = Avalonia.Media.TextAlignment.Right;
                    link.VerticalAlignment = VerticalAlignment.Center;

                    link.HorizontalAlignment = HorizontalAlignment.Left;
                    Grid.SetColumn(link, 1);
                    Grid.SetColumn(nome, 2);
                    g.Children.Add(endereco);
                    g.Children.Add(link);
                    g.Children.Add(nome);

                    var linha = new Border { Child = g };
                    linha.Classes.Add("linha");
                    _pci.Children.Add(linha);
                }
            }
        }

        _pciNota.Text = "A largura e a velocidade são as negociadas agora (LnkSta do kernel). O máximo é o que o " +
                        "próprio dispositivo anuncia; nas pontes o valor é o link que estão a servir, por isso não " +
                        "é comparável e ficam escondidas até as pedires.";
    }

    // ------------------------------------------------------------------ USB

    private void MostrarUsb(List<DispositivoUsb> todos)
    {
        var dispositivos = todos.Where(d => !d.Hub && !d.Id.StartsWith("usb", StringComparison.Ordinal)).ToList();
        var hubs = todos.Where(d => d.Hub || d.Id.StartsWith("usb", StringComparison.Ordinal)).ToList();

        _usbResumo.Text = todos.Count == 0
            ? "Nenhum dispositivo USB legível em /sys/bus/usb/devices."
            : $"{dispositivos.Count} dispositivo(s) ligado(s) e {hubs.Count} concentrador(es)/controlador(es). " +
              "A velocidade é a negociada com o kernel, não um teste de débito.";

        var assinatura = string.Join("|", todos.Select(d => d.Id + d.IdVendor + d.IdProduto + d.TextoVelocidade + d.Nome));
        if (assinatura == _assinaturaUsb) return;
        _assinaturaUsb = assinatura;

        _usb.Children.Clear();
        foreach (var d in dispositivos)
        {
            _usb.Children.Add(LinhaUsb(d));

            var detalhe = new List<string>();
            if (d.Drivers.Count > 0) detalhe.Add("driver " + string.Join("/", d.Drivers));
            if (d.Potencia is { Length: > 0 } p) detalhe.Add("pede " + p);
            if (d.Serie is { Length: > 0 } s) detalhe.Add("série " + s);
            if (d.Barramento > 0) detalhe.Add($"bus {d.Barramento}, dispositivo {d.Dispositivo}");

            if (detalhe.Count > 0)
            {
                var nota = UI.Texto(string.Join(" · ", detalhe), "sub");
                nota.Margin = new Avalonia.Thickness(150, 2, 0, 6);
                _usb.Children.Add(nota);
            }
        }
    }

    private static Control LinhaUsb(DispositivoUsb d)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("190,110,*,150") };

        var nome = UI.Texto(d.Nome, "chave");
        nome.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
        nome.VerticalAlignment = VerticalAlignment.Center;
        nome.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;

        var id = UI.Texto(d.Id, "mono");
        id.VerticalAlignment = VerticalAlignment.Center;

        var classes = UI.Texto(d.TextoClasses.Length > 0 ? d.TextoClasses : $"USB {d.Versao}", "sub");
        classes.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
        classes.VerticalAlignment = VerticalAlignment.Center;

        var velocidade = UI.Texto(d.TextoVelocidade, "mono");
        velocidade.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
        velocidade.HorizontalAlignment = HorizontalAlignment.Right;
        velocidade.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(id, 1);
        Grid.SetColumn(classes, 2);
        Grid.SetColumn(velocidade, 3);
        g.Children.Add(nome);
        g.Children.Add(id);
        g.Children.Add(classes);
        g.Children.Add(velocidade);

        var linha = new Border { Child = g };
        linha.Classes.Add("linha");
        return linha;
    }

    // ------------------------------------------------------------------ áudio

    private void MostrarAudio(LeituraAudio a)
    {
        var activos = a.Activos;
        _audioResumo.Text = Core.Audio.Resumo(a) + ".";

        var assinatura = string.Join("|", a.Placas.Select(p => p.Indice + p.Id)) +
                         "#" + string.Join("|", a.Fluxos.Where(f => f.Activo).Select(f => f.Placa + f.Pcm)) +
                         "#" + a.Xruns;
        if (assinatura == _assinaturaAudio) return;
        _assinaturaAudio = assinatura;

        _audio.Children.Clear();

        foreach (var p in a.Placas)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("80,140,*,200") };
            var indice = UI.Texto($"card{p.Indice}", "mono");
            indice.VerticalAlignment = VerticalAlignment.Center;
            var id = UI.Texto(p.Id, "chave");
            id.VerticalAlignment = VerticalAlignment.Center;
            var nome = UI.Texto(p.Nome, "chave");
            nome.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
            nome.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
            nome.VerticalAlignment = VerticalAlignment.Center;
            var driver = UI.Texto(p.Driver + (p.Barramento is { Length: > 0 } b ? $" · {b}" : ""), "mono");
            driver.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
            driver.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
            driver.HorizontalAlignment = HorizontalAlignment.Right;

            Grid.SetColumn(id, 1);
            Grid.SetColumn(nome, 2);
            Grid.SetColumn(driver, 3);
            g.Children.Add(indice);
            g.Children.Add(id);
            g.Children.Add(nome);
            g.Children.Add(driver);

            var linha = new Border { Child = g };
            linha.Classes.Add("linha");
            _audio.Children.Add(linha);
        }

        if (activos.Count == 0)
        {
            var vazio = UI.Texto("Nenhum fluxo PCM aberto agora — todos os dispositivos estão \"closed\" em /proc/asound.",
                "sub");
            vazio.Margin = new Avalonia.Thickness(0, 8, 0, 0);
            _audio.Children.Add(vazio);
        }
        else
        {
            var titulo = UI.Texto("fluxos activos", "rotulo");
            titulo.Margin = new Avalonia.Thickness(0, 10, 0, 4);
            _audio.Children.Add(titulo);

            foreach (var f in activos)
            {
                var texto = $"{f.Placa} · {f.Pcm} ({f.Dispositivo}) — {f.Direcao}" +
                            (f.Taxa is int t ? $", {t} Hz" : "") +
                            (f.Canais is int c ? $", {c} canais" : "") +
                            (f.Formato is { Length: > 0 } fo ? $", {fo}" : "") +
                            (f.LatenciaMs is double l ? $" · período {l:0.00} ms" : "") +
                            (f.BufferMs is double b ? $" · buffer {b:0.0} ms" : "");
                var linha = UI.Texto(texto, "valor");
                linha.Margin = new Avalonia.Thickness(0, 2, 0, 2);
                _audio.Children.Add(linha);
            }
        }

        var xruns = a.Nos.Where(n => n.Xruns > 0).OrderByDescending(n => n.Xruns).Take(6).ToList();
        if (xruns.Count > 0)
        {
            var titulo = UI.Texto("xruns por nó do PipeWire", "rotulo");
            titulo.Margin = new Avalonia.Thickness(0, 10, 0, 4);
            _audio.Children.Add(titulo);
            foreach (var n in xruns)
            {
                var linha = UI.Texto($"{n.Xruns,6} × {n.Nome}  ({n.Quantum} frames @ {n.Taxa} Hz)", "mono");
                linha.Margin = new Avalonia.Thickness(0, 1, 0, 1);
                _audio.Children.Add(linha);
            }
        }

        _audioNota.Children.Clear();

        _audioNota.Children.Add(UI.Texto(a.Nos.Count > 0
            ? "Xruns = coluna ERR do pw-top: contador acumulado no servidor desde que cada nó arrancou. " +
              "Reiniciar o PipeWire ou a aplicação cliente volta a zero."
            : "Xruns não medidos: o PipeWire não respondeu ao pw-top.", "sub"));

        _audioNota.Children.Add(UI.Texto(a.LatenciaPadraoMs is double lat
            ? $"Quantum de {lat:0.00} ms — é o período com que o servidor acorda. Buffers maiores toleram mais jitter; " +
              "buffers menores dão menos latência."
            : "Quantum não lido: sem nó activo o pw-top não mostra o período e o pw-metadata não respondeu.", "sub"));

        if (a.Aviso is { Length: > 0 } aviso)
            _audioNota.Children.Add(UI.Texto(aviso, "sub"));
    }
}

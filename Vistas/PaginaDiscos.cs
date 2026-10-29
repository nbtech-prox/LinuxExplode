using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

/// <summary>
/// Discos e saúde: ocupação, temperatura e S.M.A.R.T. de /sys e do smartctl, mais o auto-teste do
/// próprio disco (curto/longo) com o progresso e o histórico — os mesmos dados que o --cli autoteste
/// imprime, aqui a actualizar sozinhos enquanto o teste corre.
/// </summary>
public sealed class PaginaDiscos : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };
    private readonly Dictionary<string, AutoTesteBloco> _autotestes = new();

    // o smartctl devolve o controlo logo; o disco continua o teste sozinho. Enquanto algum estiver a
    // correr, este relógio relê o estado (4 s chega bem para um teste de 2 minutos).
    private readonly DispatcherTimer _vigia = new() { Interval = TimeSpan.FromSeconds(4) };
    private bool _aAtualizar;

    public PaginaDiscos()
    {
        Content = UI.Pagina(
            UI.Cabecalho("Discos e saúde", "/sys/block + smartctl — ocupação, temperatura, S.M.A.R.T. e auto-teste",
                UI.Botao("Atualizar", false, () => _ = Carregar())),
            _corpo);

        _corpo.Children.Add(UI.Texto("a ler discos…", "sub"));

        _vigia.Tick += async (_, _) => await Vigiar();
        Loaded += async (_, _) => await Carregar();
        Unloaded += (_, _) => _vigia.Stop();
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
        _autotestes.Clear();

        if (discos.Count == 0)
        {
            _corpo.Children.Add(UI.Texto("Nenhum disco detetado.", "sub"));
            return;
        }

        // gancho de captura: LINUXEXPLODE_SECAO=autoteste deixa só o bloco do auto-teste no topo
        bool soAutoTeste = (Environment.GetEnvironmentVariable("LINUXEXPLODE_SECAO") ?? "")
            .Equals("autoteste", StringComparison.OrdinalIgnoreCase);

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

            if (!soAutoTeste)
            {
                // detalhes SMART
                var detalhes = new StackPanel { Spacing = 0 };
                if (d.Smart.HorasLigado is long h) detalhes.Children.Add(UI.Linha("Horas ligado", $"{h:N0} h  ({h / 24.0:N0} dias)"));
                if (d.Smart.Ciclos is long c) detalhes.Children.Add(UI.Linha("Ciclos de energia", $"{c:N0}"));
                if (d.Smart.Desgaste is double des) detalhes.Children.Add(UI.Linha("Desgaste", $"{des:0} %"));
                if (d.Smart.SetoresRealocados is long sr) detalhes.Children.Add(UI.Linha("Setores realocados", $"{sr:N0}"));
                if (d.Smart.ErrosLeitura is long er) detalhes.Children.Add(UI.Linha("Erros I/O", $"{er:N0}"));
                if (detalhes.Children.Count > 0) conteudo.Children.Add(detalhes);
            }

            // auto-teste: fica sempre visível, é o que diz se o disco está saudável a sério
            conteudo.Children.Add(BlocoAutoTeste(d));
            var bloco = _autotestes[d.Dev];
            bloco.Mostrar(d.AutoTeste);

            if (!soAutoTeste)
            {
                // partições
                if (d.Particoes.Count > 0)
                {
                    var partes = new StackPanel { Spacing = 8 };
                    foreach (var p in d.Particoes)
                    {
                        var blocoP = new StackPanel { Spacing = 4 };
                        var cab = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                        cab.Children.Add(UI.Texto($"/dev/{p.Nome}", "mono"));
                        var txt = p.Total > 0
                            ? $"{Util.Bytes(p.Livre)} livres de {Util.Bytes(p.Total)}"
                            : "não montada";
                        var dir = UI.Texto(txt, "mono");
                        dir.HorizontalAlignment = HorizontalAlignment.Right;
                        Grid.SetColumn(dir, 1);
                        cab.Children.Add(dir);
                        blocoP.Children.Add(cab);
                        if (p.Total > 0)
                            blocoP.Children.Add(UI.Barra((double)(p.Total - p.Livre) / p.Total));
                        blocoP.Children.Add(UI.Texto($"{p.Montagem} · {p.Fs}", "sub"));
                        partes.Children.Add(blocoP);
                    }
                    conteudo.Children.Add(new Border { Height = 1, Background = UI.Cor("Linha"), Margin = new Avalonia.Thickness(0, 4) });
                    conteudo.Children.Add(partes);
                }

                if (d.Smart.Nota is not null && d.Smart.Veredito == "—")
                    conteudo.Children.Add(UI.Texto(d.Smart.Nota, "sub"));
            }

            _corpo.Children.Add(UI.Cartao(null, conteudo));
        }

        if (!soAutoTeste)
        {
            _corpo.Children.Add(UI.Texto(
                "A saúde S.M.A.R.T. vem do smartctl (por sudo NOPASSWD em /etc/sudoers.d/smartctl). " +
                "Se aparecer \"sem S.M.A.R.T.\", é porque falta essa permissão de root — o cartão do disco " +
                "mostra o comando exato para a conceder. O auto-teste é executado pelo próprio controlador: " +
                "curto demora ~2 min num NVMe, longo varre a superfície toda (pode demorar horas) e o disco " +
                "continua utilizável durante o teste.", "sub"));
        }

        if (_autotestes.Values.Any(b => b.Info.EmCurso)) _vigia.Start();
    }

    // ------------------------------------------------------------------ auto-teste

    private Control BlocoAutoTeste(DiscoInfo d)
    {
        var estado = new TextBlock { Text = "a ler…", Classes = { "mono" }, TextWrapping = TextWrapping.Wrap };
        var barra = new ProgressBar { Minimum = 0, Maximum = 1, Value = 0, IsVisible = false };

        var curto = UI.Botao("Teste curto", false, () => _ = Iniciar(d.Dev, TipoAutoTeste.Curto));
        var longo = UI.Botao("Teste longo", false, () => _ = Iniciar(d.Dev, TipoAutoTeste.Longo));
        var abortar = UI.Botao("Abortar", false, () => _ = Abortar(d.Dev));

        var acoes = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var b in new[] { curto, longo, abortar })
        {
            b.Margin = new Avalonia.Thickness(0, 0, 8, 8);
            b.IsEnabled = false;
            acoes.Children.Add(b);
        }

        var historico = new StackPanel { Spacing = 2 };

        var pilha = new StackPanel { Spacing = 8 };
        pilha.Children.Add(new Border { Height = 1, Background = UI.Cor("Linha"), Margin = new Avalonia.Thickness(0, 4) });
        pilha.Children.Add(UI.Texto("AUTO-TESTE S.M.A.R.T.", "rotulo"));
        pilha.Children.Add(acoes);
        pilha.Children.Add(estado);
        pilha.Children.Add(barra);
        pilha.Children.Add(historico);

        _autotestes[d.Dev] = new AutoTesteBloco
        {
            Dev = d.Dev,
            Estado = estado,
            Barra = barra,
            Curto = curto,
            Longo = longo,
            Abortar = abortar,
            Historico = historico
        };

        return pilha;
    }

    private async Task Iniciar(string dev, TipoAutoTeste tipo)
    {
        if (_autotestes.TryGetValue(dev, out var bloco))
        {
            bloco.Estado.Text = $"a pedir o auto-teste {(tipo == TipoAutoTeste.Curto ? "curto" : "longo")}…";
            bloco.Curto.IsEnabled = bloco.Longo.IsEnabled = false;
        }

        var (ok, mensagem) = await Task.Run(() => Discos.IniciarAutoTeste(dev, tipo));

        if (_autotestes.TryGetValue(dev, out var b))
        {
            b.Estado.Text = mensagem;
            if (!ok) b.Curto.IsEnabled = b.Longo.IsEnabled = true;
        }

        await AtualizarAutoTeste(dev);
        _vigia.Start();
    }

    private async Task Abortar(string dev)
    {
        if (_autotestes.TryGetValue(dev, out var bloco))
        {
            bloco.Abortar.IsEnabled = false;
            bloco.Estado.Text = "a abortar o auto-teste…";
        }

        var (ok, mensagem) = await Task.Run(() => Discos.PararAutoTeste(dev));

        if (_autotestes.TryGetValue(dev, out var b))
        {
            b.Estado.Text = mensagem;
            if (!ok) b.Abortar.IsEnabled = b.Info.EmCurso;
        }

        await AtualizarAutoTeste(dev);
    }

    private async Task AtualizarAutoTeste(string dev)
    {
        AutoTesteInfo info;
        try { info = await Task.Run(() => Discos.LerAutoTeste(dev)); }
        catch (Exception ex) { info = AutoTesteInfo.Indisponivel("falha a ler o auto-teste: " + ex.Message); }

        if (_autotestes.TryGetValue(dev, out var bloco)) bloco.Mostrar(info);
    }

    /// <summary>Relê só os discos com teste a correr; sem nenhum, o relógio pára.</summary>
    private async Task Vigiar()
    {
        if (_aAtualizar) return;
        _aAtualizar = true;
        try
        {
            var emCurso = _autotestes.Values.Where(b => b.Info.EmCurso).Select(b => b.Dev).ToList();
            foreach (var dev in emCurso) await AtualizarAutoTeste(dev);

            if (!_autotestes.Values.Any(b => b.Info.EmCurso)) _vigia.Stop();
        }
        finally { _aAtualizar = false; }
    }

    private sealed class AutoTesteBloco
    {
        public required string Dev { get; init; }
        public required TextBlock Estado { get; init; }
        public required ProgressBar Barra { get; init; }
        public required Button Curto { get; init; }
        public required Button Longo { get; init; }
        public required Button Abortar { get; init; }
        public required StackPanel Historico { get; init; }
        public AutoTesteInfo Info { get; private set; } = AutoTesteInfo.Indisponivel("");

        public void Mostrar(AutoTesteInfo info)
        {
            Info = info;
            Historico.Children.Clear();

            if (!info.Suportado)
            {
                Estado.Text = info.Nota ?? "indisponível";
                Barra.IsVisible = false;
                Curto.IsEnabled = Longo.IsEnabled = Abortar.IsEnabled = false;
                return;
            }

            if (info.EmCurso)
            {
                Estado.Text = info.Percentagem is int pct
                    ? $"a correr · {pct} % · {info.TipoCorrida}"
                    : $"a correr · {info.TipoCorrida} (o controlador não publica a percentagem)";
                Barra.Value = (info.Percentagem ?? 0) / 100.0;
                Barra.IsVisible = info.Percentagem is not null;
                Curto.IsEnabled = Longo.IsEnabled = false;
                Abortar.IsEnabled = true;
            }
            else
            {
                Estado.Text = info.Historico.Count > 0
                    ? $"último resultado: {info.Historico[0].Resultado}"
                    : "nunca corrido";
                Barra.IsVisible = false;
                Curto.IsEnabled = Longo.IsEnabled = true;
                Abortar.IsEnabled = false;
            }

            if (info.Nota is not null) Estado.Text += "\n" + info.Nota;

            foreach (var h in info.Historico.Take(6))
            {
                var linha = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*,Auto") };
                var tipo = UI.Texto(h.Tipo, "mono");
                tipo.TextWrapping = TextWrapping.NoWrap;
                var resultado = UI.Texto(h.Resultado, "mono");
                resultado.TextWrapping = TextWrapping.NoWrap;
                resultado.TextTrimming = TextTrimming.CharacterEllipsis;
                var horas = UI.Texto(h.HorasLigado is long hs ? $"{hs:N0} h" : "", "sub");
                horas.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(resultado, 1);
                Grid.SetColumn(horas, 2);
                linha.Children.Add(tipo);
                linha.Children.Add(resultado);
                linha.Children.Add(horas);
                Historico.Children.Add(linha);
            }
        }
    }
}

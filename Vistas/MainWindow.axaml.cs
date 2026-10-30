using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, Control> _paginas = new();
    private Button? _ativo;
    private string _chaveAtual = "sistema";

    public MainWindow()
    {
        InitializeComponent();

        // A janela só grava a escolha de idioma depois de aberta: o índice inicial do ComboBox é
        // definido por código e não pode ser confundido com uma escolha do utilizador.
        Opened += (_, _) => _idiomaPronto = true;

        // a versão no rodapé vem do Relatorio (antes era texto fixo no .axaml e ficava desactualizada)
        if (this.FindControl<TextBlock>("RodapeVersao") is { } rodape)
            rodape.Text = "v" + Core.Relatorio.Versao;

        // tema e escala guardados em ~/.config/linux-explode/config.json
        Paleta.Aplicar(Config.Atual.Tema == "claro");
        LigarAparencia();
        LigarAtalhos();
        AplicarTextos();

        _ativo = this.FindControl<Button>("NavSistema");
        Navegar((Environment.GetEnvironmentVariable("LINUXEXPLODE_PAGINA") ?? "sistema").ToLowerInvariant());

        // gancho de teste/depuração: abrir já a prévia animada (LINUXEXPLODE_PREVIA=cpu|gpu|rt)
        var previa = Environment.GetEnvironmentVariable("LINUXEXPLODE_PREVIA")?.ToLowerInvariant();
        if (previa is "cpu" or "gpu" or "rt")
        {
            Opened += (_, _) =>
            {
                var modalidade = previa switch
                {
                    "cpu" => Vistas.ModalidadePrevia.Cpu,
                    "gpu" => Vistas.ModalidadePrevia.Gpu,
                    _ => Vistas.ModalidadePrevia.Rt
                };
                int threads = int.TryParse(Environment.GetEnvironmentVariable("LINUXEXPLODE_THREADS"), out int t) ? t : 0;
                new Vistas.JanelaPrevia(modalidade, 0, threads).Show(this);
            };
        }

        Opened += async (_, _) =>
        {
            var estado = this.FindControl<TextBlock>("RodapeEstado");
            if (estado is null) return;
            try
            {
                var s = await Task.Run(Sistema.Recolher);
                estado.Text = $"{s.So.Split('(')[0].Trim()}\n{s.CpuModelo}";
            }
            catch
            {
                estado.Text = Core.Textos.T("leitura de hardware indisponível");
            }
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void IrSistema(object? s, RoutedEventArgs e) => Navegar("sistema");
    private void IrSensores(object? s, RoutedEventArgs e) => Navegar("sensores");
    private void IrDiscos(object? s, RoutedEventArgs e) => Navegar("discos");
    private void IrBench(object? s, RoutedEventArgs e) => Navegar("bench");
    private void IrStress(object? s, RoutedEventArgs e) => Navegar("stress");
    private void IrRede(object? s, RoutedEventArgs e) => Navegar("rede");
    private void IrDispositivos(object? s, RoutedEventArgs e) => Navegar("dispositivos");
    private void IrRelatorio(object? s, RoutedEventArgs e) => Navegar("relatorio");

    /// <summary>Mostra a janela (vinda da bandeja) e, se indicado, muda de página.</summary>
    public void MostrarEm(string? pagina = null)
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (pagina is not null) Navegar(pagina);
    }

    /// <summary>Fecha a aplicação de vez, mesmo com «fechar para a bandeja» ligado (menu Sair).</summary>
    public void Sair()
    {
        _sairDeVez = true;
        Close();
    }

    private bool _sairDeVez;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Com bandeja disponível, fechar a janela esconde-a: o ícone continua a mostrar temperaturas
        // e a permitir parar o stress. "Sair" no menu da bandeja ou Ctrl+Q fecham de vez.
        if (!_sairDeVez && Config.Atual.BandejaFechar && Vistas.Bandeja.Disponivel)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    private void Navegar(string chave)
    {
        var conteudo = this.FindControl<ContentControl>("Conteudo");
        if (conteudo is null) return;
        _chaveAtual = chave;

        if (!_paginas.TryGetValue(chave, out var pagina))
        {
            pagina = chave switch
            {
                "sensores" => new PaginaSensores(),
                "discos" => new PaginaDiscos(),
                "bench" => new PaginaBench(),
                "stress" => new PaginaStress(),
                "rede" => new PaginaRede(),
                "dispositivos" => new PaginaDispositivos(),
                "relatorio" => new PaginaRelatorio(),
                _ => new PaginaSistema()
            };
            _paginas[chave] = pagina;
        }

        conteudo.Content = pagina;

        var botao = this.FindControl<Button>("Nav" + char.ToUpperInvariant(chave[0]) + chave[1..]);
        if (botao is not null)
        {
            _ativo?.Classes.Remove("ativo");
            botao.Classes.Add("ativo");
            _ativo = botao;
        }
    }

    // ------------------------------------------------------------------ idioma

    /// <summary>Aplica o idioma escolhido a tudo o que é estático e reconstrói a página actual.</summary>
    private void MudarIdioma(object? s, SelectionChangedEventArgs e)
    {
        if (_aAplicarIdioma || !_idiomaPronto) return;
        if (this.FindControl<ComboBox>("Idioma") is not { } combo) return;
        var idioma = combo.SelectedIndex switch
        {
            1 => Core.Idioma.Es,
            2 => Core.Idioma.En,
            _ => Core.Idioma.Pt
        };
        if (idioma == Textos.Atual) return;

        Textos.Aplicar(idioma, guardar: true);
        // as páginas constroem o texto no construtor: limpar a cache é o que as traduz
        _paginas.Clear();
        AplicarTextos();
        Navegar(_chaveAtual);
    }

    /// <summary>Textos que vivem no XAML (navegação, tema, rodapé) e não passam pelos construtores de UI.</summary>
    private void AplicarTextos()
    {
        void Nav(string nome, string chave)
        {
            if (this.FindControl<Button>(nome) is { } b) b.Content = Textos.T(chave);
        }

        Nav("NavSistema", "Sistema");
        Nav("NavSensores", "Sensores");
        Nav("NavDiscos", "Discos e saúde");
        Nav("NavBench", "Benchmark");
        Nav("NavStress", "Stress");
        Nav("NavRede", "Rede");
        Nav("NavDispositivos", "Dispositivos");
        Nav("NavRelatorio", "Relatório");

        if (this.FindControl<TextBlock>("Subtitulo") is { } sub)
            sub.Text = Textos.T("hardware · sensores · bench");

        if (this.FindControl<Slider>("Escala") is { } escala)
            Avalonia.Controls.ToolTip.SetTip(escala, Textos.T("Escala da interface (Ctrl+- e Ctrl++)"));

        if (this.FindControl<ComboBox>("Idioma") is { } combo && combo.SelectedIndex != (int)Textos.Atual)
        {
            // mudar o índice dispara SelectionChanged: a guarda acima impede o ciclo
            _aAplicarIdioma = true;
            combo.SelectedIndex = (int)Textos.Atual;
            _aAplicarIdioma = false;
        }

        AtualizarBotaoTema();
        Bandeja.AplicarTextos();
    }

    private bool _aAplicarIdioma;
    private bool _idiomaPronto;

    // ------------------------------------------------------------------ aparência e atalhos

    private void LigarAparencia()
    {
        AtualizarBotaoTema();

        if (this.FindControl<Slider>("Escala") is { } escala)
        {
            escala.Value = Math.Clamp(Config.Atual.Escala, 0.85, 1.35);
            AplicarEscala(escala.Value);

            escala.PropertyChanged += (_, e) =>
            {
                if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty) return;
                AplicarEscala(escala.Value);
            };
        }
    }

    private void AtualizarBotaoTema()
    {
        if (this.FindControl<Button>("BotaoTema") is { } botao)
            botao.Content = Textos.T(Paleta.Claro ? "Tema escuro" : "Tema claro");
    }

    private void AlternarTema(object? s, RoutedEventArgs e)
    {
        Paleta.Alternar();
        Config.Atual.Tema = Paleta.Claro ? "claro" : "escuro";
        Config.Atual.Guardar();
        AtualizarBotaoTema();
    }

    private void AplicarEscala(double valor)
    {
        if (this.FindControl<LayoutTransformControl>("Zoom") is { } zoom)
            zoom.LayoutTransform = new Avalonia.Media.ScaleTransform(valor, valor);

        Config.Atual.Escala = Math.Clamp(valor, 0.85, 1.35);
        Config.Atual.Guardar();
    }

    /// <summary>Ctrl+1…7 muda de página, Ctrl+= / Ctrl+- muda a escala, Ctrl+0 repõe, Ctrl+Q fecha.</summary>
    private void LigarAtalhos()
    {
        KeyDown += (_, e) =>
        {
            if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) == 0) return;

            string? destino = e.Key switch
            {
                Avalonia.Input.Key.D1 => "sistema",
                Avalonia.Input.Key.D2 => "sensores",
                Avalonia.Input.Key.D3 => "discos",
                Avalonia.Input.Key.D4 => "bench",
                Avalonia.Input.Key.D5 => "stress",
                Avalonia.Input.Key.D6 => "rede",
                Avalonia.Input.Key.D7 => "dispositivos",
                Avalonia.Input.Key.D8 => "relatorio",
                _ => null
            };

            if (destino is not null)
            {
                Navegar(destino);
                e.Handled = true;
                return;
            }

            switch (e.Key)
            {
                case Avalonia.Input.Key.OemPlus:
                case Avalonia.Input.Key.Add:
                    AjustarEscala(0.05);
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.OemMinus:
                case Avalonia.Input.Key.Subtract:
                    AjustarEscala(-0.05);
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.D0:
                    AjustarEscala(0, repor: true);
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.Q:
                    Sair();
                    break;
            }
        };
    }

    private void AjustarEscala(double delta, bool repor = false)
    {
        if (this.FindControl<Slider>("Escala") is not { } escala) return;
        escala.Value = repor ? 1.0 : Math.Clamp(escala.Value + delta, escala.Minimum, escala.Maximum);
    }
}

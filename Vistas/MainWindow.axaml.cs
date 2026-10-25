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

    public MainWindow()
    {
        InitializeComponent();

        // a versão no rodapé vem do Relatorio (antes era texto fixo no .axaml e ficava desactualizada)
        if (this.FindControl<TextBlock>("RodapeVersao") is { } rodape)
            rodape.Text = "v" + Core.Relatorio.Versao;

        // tema e escala guardados em ~/.config/linux-explode/config.json
        Paleta.Aplicar(Config.Atual.Tema == "claro");
        LigarAparencia();
        LigarAtalhos();

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
                estado.Text = "leitura de hardware indisponível";
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
    private void IrRelatorio(object? s, RoutedEventArgs e) => Navegar("relatorio");

    private void Navegar(string chave)
    {
        var conteudo = this.FindControl<ContentControl>("Conteudo");
        if (conteudo is null) return;

        if (!_paginas.TryGetValue(chave, out var pagina))
        {
            pagina = chave switch
            {
                "sensores" => new PaginaSensores(),
                "discos" => new PaginaDiscos(),
                "bench" => new PaginaBench(),
                "stress" => new PaginaStress(),
                "rede" => new PaginaRede(),
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
            botao.Content = Paleta.Claro ? "Tema escuro" : "Tema claro";
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
                Avalonia.Input.Key.D7 => "relatorio",
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
                    Close();
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

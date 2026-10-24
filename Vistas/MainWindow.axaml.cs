using Avalonia.Controls;
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

        _ativo = this.FindControl<Button>("NavSistema");
        Navegar((Environment.GetEnvironmentVariable("LINUXEXPLODE_PAGINA") ?? "sistema").ToLowerInvariant());

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
}

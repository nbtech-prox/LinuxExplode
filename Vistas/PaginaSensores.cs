using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using LinuxExplode.Core;

namespace LinuxExplode.Vistas;

public sealed class PaginaSensores : UserControl
{
    private readonly StackPanel _corpo = new() { Spacing = 14 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, TextBlock> _valores = new();
    private readonly Dictionary<int, TextBlock> _freqNucleos = new();
    private readonly StackPanel _lista = new() { Spacing = 14 };
    private readonly StackPanel _sensores = new() { Spacing = 14 };

    private readonly Dictionary<string, TextBlock> _potValores = new(StringComparer.Ordinal);

    private Border? _cartaoCpu;
    private TextBlock? _notaCpu;
    private Border? _cartaoPotencia;
    private TextBlock? _potNota;
    private TipoSensor? _filtro;
    private bool _aCarregar;

    public PaginaSensores()
    {
        var filtros = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        void F(string nome, TipoSensor? t)
        {
            var b = new Button { Content = nome, FontSize = 12 };
            b.Classes.Add("secundario");
            b.Click += (_, _) =>
            {
                _filtro = t;
                _valores.Clear();
                _freqNucleos.Clear();
                _potValores.Clear();
                _notaCpu = null;
                _cartaoCpu = null;
                _cartaoPotencia = null;
                _potNota = null;
                _sensores.Children.Clear();
                _lista.Children.Clear();
                _ = Atualizar();
            };
            filtros.Children.Add(b);
        }
        F("Todos", null);
        F("Temperatura", TipoSensor.Temperatura);
        F("Ventoinha", TipoSensor.Ventoinha);
        F("Potência", TipoSensor.Energia);
        F("Tensão", TipoSensor.Tensao);
        F("Frequência", TipoSensor.Frequencia);

        // gancho de teste/depuração: abrir já num filtro (LINUXEXPLODE_FILTRO=frequencia)
        var filtroInicial = Environment.GetEnvironmentVariable("LINUXEXPLODE_FILTRO")?.ToLowerInvariant();
        _filtro = filtroInicial switch
        {
            "frequencia" or "frequência" => TipoSensor.Frequencia,
            "temperatura" => TipoSensor.Temperatura,
            "ventoinha" => TipoSensor.Ventoinha,
            "potencia" or "potência" => TipoSensor.Energia,
            "tensao" or "tensão" => TipoSensor.Tensao,
            _ => null
        };

        var online = new TextBlock { Text = "ao vivo · 1 s", Classes = { "sub" }, VerticalAlignment = VerticalAlignment.Center };

        var cabecalho = UI.Cabecalho("Sensores", "/sys/class/hwmon e /sys/devices/system/cpu — leitura direta, sem root");

        var barra = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        barra.Children.Add(filtros);
        barra.Children.Add(online);

        _corpo.Children.Add(barra);
        _corpo.Children.Add(_lista);
        Content = UI.Pagina(cabecalho, _corpo);

        _timer.Tick += async (_, _) => await Atualizar();
        Loaded += async (_, _) => { _timer.Start(); await Atualizar(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private async Task Atualizar()
    {
        if (_aCarregar) return;
        _aCarregar = true;
        try
        {
            var todos = await Task.Run(Sensores.Ler);
            var lista = _filtro is null ? todos : todos.Where(s => s.Tipo == _filtro).ToList();

            // No filtro de frequência os núcleos da CPU vêm primeiro; a GPU (hwmon) vem depois.
            var verCpu = _filtro == TipoSensor.Frequencia;
            if (verCpu)
            {
                var f = await Task.Run(FrequenciaCpu.Recolher);
                AtualizarCartaoCpu(f);
            }

            // No filtro de potência o consumo do PC (RAPL + GPUs) vem antes da lista de hwmon.
            var verPot = _filtro == TipoSensor.Energia;
            if (verPot)
            {
                var p = await Task.Run(Potencia.Ler);
                AtualizarCartaoPotencia(p);
            }

            Ordenar(verCpu ? _cartaoCpu : null, verPot ? _cartaoPotencia : null);

            if (lista.Count == 0)
            {
                _sensores.Children.Clear();
                var texto = verCpu && _cartaoCpu is not null
                    ? "Nenhum sensor de frequência em /sys/class/hwmon — o detalhe por núcleo acima vem de cpufreq."
                    : verPot && _cartaoPotencia is not null
                        ? "Nenhum sensor de potência em /sys/class/hwmon — o consumo acima vem do RAPL (CPU) e dos contadores das GPUs."
                        : "Nenhum sensor disponível nesta categoria. Em muitos portáteis é preciso carregar o módulo " +
                          "do kernel (ex.: sudo modprobe nct6775) ou o CPU expõe apenas k10temp.";
                _sensores.Children.Add(UI.Texto(texto, "sub"));
                return;
            }

            var chaves = lista.Select(s => s.Chip + "|" + s.Nome).ToList();
            bool estruturaIgual = _valores.Count == chaves.Count && chaves.All(_valores.ContainsKey);

            if (!estruturaIgual)
            {
                _valores.Clear();
                _sensores.Children.Clear();
                foreach (var grupo in lista.GroupBy(x => x.Chip))
                {
                    var linhas = new StackPanel { Spacing = 0 };
                    foreach (var s in grupo)
                    {
                        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                        var nome = UI.Texto(s.Nome, "chave");
                        nome.VerticalAlignment = VerticalAlignment.Center;
                        var valor = UI.Texto("—", "valor");
                        valor.HorizontalAlignment = HorizontalAlignment.Right;
                        Grid.SetColumn(valor, 1);
                        g.Children.Add(nome);
                        g.Children.Add(valor);

                        var linha = new Border { Child = g };
                        linha.Classes.Add("linha");
                        linhas.Children.Add(linha);
                        _valores[s.Chip + "|" + s.Nome] = valor;
                    }
                    _sensores.Children.Add(UI.Cartao(grupo.Key, linhas));
                }
            }

            foreach (var s in lista)
                if (_valores.TryGetValue(s.Chip + "|" + s.Nome, out var tb))
                    tb.Text = s.ValorTexto;
        }
        catch
        {
            // leitura falhou — mantém o último valor conhecido
        }
        finally { _aCarregar = false; }
    }

    /// <summary>Garante a ordem dos cartões extra (CPU, potência) antes da lista de hwmon.</summary>
    private void Ordenar(params Control?[] extras)
    {
        var desejado = new List<Control>();
        foreach (var e in extras)
            if (e is not null) desejado.Add(e);
        desejado.Add(_sensores);

        bool igual = _lista.Children.Count == desejado.Count;
        for (int i = 0; igual && i < desejado.Count; i++)
            igual = ReferenceEquals(_lista.Children[i], desejado[i]);
        if (igual) return;

        _lista.Children.Clear();
        foreach (var c in desejado) _lista.Children.Add(c);
    }

    /// <summary>Cartão "Consumo do PC": potência da CPU (RAPL) e das GPUs (hwmon), com total estimado.</summary>
    private void AtualizarCartaoPotencia(LeituraPotencia p)
    {
        var gpus = p.Gpus.OrderBy(g => g.Integrada).ThenBy(g => g.Etiqueta, StringComparer.Ordinal).ToList();

        var linhas = new List<(string Chave, string Rotulo, bool Forte)>
        {
            ("pacote", "CPU — socket (RAPL)", true)
        };
        if (p.NucleosW is not null) linhas.Add(("nucleos", "CPU — núcleos (RAPL)", false));
        if (p.PlataformaW is not null) linhas.Add(("plataforma", "Plataforma (psys, RAPL)", true));
        foreach (var g in gpus)
            linhas.Add(("gpu:" + g.Etiqueta,
                        $"GPU {g.Etiqueta} · {g.Nome}" + (g.Integrada ? " (integrada, dentro do pacote)" : ""),
                        false));
        linhas.Add(("total", "Total medido", true));
        if (p.EnergiaKWh is not null) linhas.Add(("energia", "Energia desde o arranque", false));

        var chaves = linhas.Select(l => l.Chave).ToList();
        bool igual = _potNota is not null && _potValores.Count == chaves.Count && chaves.All(_potValores.ContainsKey);

        if (!igual)
        {
            _potValores.Clear();
            var pilha = new StackPanel { Spacing = 0 };

            foreach (var l in linhas)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                var chave = UI.Texto(l.Rotulo, "chave");
                chave.VerticalAlignment = VerticalAlignment.Center;
                if (l.Forte) chave.FontWeight = Avalonia.Media.FontWeight.SemiBold;
                var valor = UI.Texto("—", "valor");
                valor.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(valor, 1);
                g.Children.Add(chave);
                g.Children.Add(valor);

                var linha = new Border { Child = g };
                linha.Classes.Add("linha");
                pilha.Children.Add(linha);
                _potValores[l.Chave] = valor;
            }

            _potNota = UI.Texto("", "sub");
            _potNota.Margin = new Avalonia.Thickness(0, 8, 0, 0);
            pilha.Children.Add(_potNota);

            _cartaoPotencia = UI.Cartao("Consumo medido — CPU (socket) e GPUs", pilha);
        }

        void Pôr(string chave, string texto)
        {
            if (_potValores.TryGetValue(chave, out var tb)) tb.Text = texto;
        }

        static string W(double? w) => Potencia.Watts(w);

        Pôr("pacote", W(p.PacoteW));
        if (p.NucleosW is not null) Pôr("nucleos", W(p.NucleosW));
        if (p.PlataformaW is not null) Pôr("plataforma", W(p.PlataformaW));
        foreach (var g in gpus) Pôr("gpu:" + g.Etiqueta, W(g.Watts));

        Pôr("total", p.TotalW is null ? "—" : $"≈ {p.TotalW.Value:0.0} W   ({p.TotalRotulo})");
        if (p.EnergiaKWh is not null) Pôr("energia", $"{p.EnergiaKWh.Value:0.000} kWh");

        if (_potNota is not null)
        {
            var nota = "Isto NÃO é o consumo da tomada: mede o socket da CPU (RAPL, inclui núcleos, I/O e gráfica " +
                       "integrada) e as GPUs. Placa-mãe, memória, discos, ventoinhas e as perdas da fonte não têm sensor " +
                       "nesta máquina — só uma tomada inteligente/UPS ou uma fonte com telemetria daria o total real.";
            _potNota.Text = p.Aviso is null ? nota : p.Aviso + "   " + nota;
        }
    }

    private void AtualizarCartaoCpu(FrequenciaCpu f)
    {
        if (!f.Disponivel)
        {
            if (_cartaoCpu is null)
            {
                var aviso = new StackPanel { Spacing = 4 };
                aviso.Children.Add(UI.Texto("sem leitura de frequência de CPU — o kernel não expõe cpufreq " +
                                            "(comum em ARM sem driver de escala e em máquinas virtuais).", "sub"));
                _cartaoCpu = UI.Cartao("CPU — frequência por núcleo", aviso);
                _freqNucleos.Clear();
                _notaCpu = null;
            }
            return;
        }

        var nucleos = f.PorNucleo();
        bool igual = _notaCpu is not null && _freqNucleos.Count == nucleos.Count
                     && nucleos.All(n => _freqNucleos.ContainsKey(n.Nucleo));

        if (!igual)
        {
            _freqNucleos.Clear();
            var pilha = new StackPanel { Spacing = 0 };

            _notaCpu = UI.Texto("", "sub");
            _notaCpu.Margin = new Avalonia.Thickness(0, 0, 0, 6);
            pilha.Children.Add(_notaCpu);

            foreach (var n in nucleos)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
                var chave = UI.Texto($"Núcleo {n.Nucleo}", "chave");
                chave.VerticalAlignment = VerticalAlignment.Center;
                var valor = UI.Texto("—", "valor");
                valor.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(valor, 1);
                g.Children.Add(chave);
                g.Children.Add(valor);

                var linha = new Border { Child = g };
                linha.Classes.Add("linha");
                pilha.Children.Add(linha);
                _freqNucleos[n.Nucleo] = valor;
            }
            _cartaoCpu = UI.Cartao("CPU — frequência por núcleo", pilha);
        }

        if (_notaCpu is not null)
            _notaCpu.Text = $"{f.NucleosFisicos} núcleos · {f.Threads.Count} threads" +
                            (f.Driver.Length > 0 ? $" · {f.Driver}" : "") +
                            (f.Governador.Length > 0 ? $" · {f.Governador}" : "") +
                            $"   |   mín {FrequenciaCpu.Ghz(f.MinAtualKHz)} · média {FrequenciaCpu.Ghz(f.MediaAtualKHz)}" +
                            $" · pico {FrequenciaCpu.Ghz(f.MaxAtualKHz)} · turbo {FrequenciaCpu.Ghz(f.TurboKHz)}";

        foreach (var n in nucleos)
            if (_freqNucleos.TryGetValue(n.Nucleo, out var tb))
                tb.Text = string.Join("   ", n.Threads.Select(t => $"T{t.Cpu} {FrequenciaCpu.Ghz(t.AtualKHz)}"));
    }
}

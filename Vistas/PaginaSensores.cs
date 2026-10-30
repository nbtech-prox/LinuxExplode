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
    // ---- placa-mãe (chipset, memória, rede, Wi-Fi) ----
    private readonly StackPanel _placaLista = new() { Spacing = 0 };
    private readonly TextBlock _placaNota = new() { Classes = { "mono" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private string _placaChave = "";
    private Border? _cartaoPlaca;

    private bool _aCarregar;

    // ---- ao vivo, throttling e energia ----
    private readonly MonitorThrottle _monitorThrottle = new();
    private readonly MedidorEnergia _medidorEnergia = new();
    private readonly Sparkline _gTempCpu = new() { Cor = "Bom", Limite = 90 };
    private readonly Sparkline _gTempGpu = new() { Cor = "Bom", Limite = 90 };
    private readonly Sparkline _gPotencia = new() { Cor = "Fg" };
    private readonly Sparkline _gFreq = new() { Cor = "Atencao" };
    private readonly TextBlock _vTempCpu = new() { Text = "—", Classes = { "valor" } };
    private readonly TextBlock _vTempGpu = new() { Text = "—", Classes = { "valor" } };
    private readonly TextBlock _vPotencia = new() { Text = "—", Classes = { "valor" } };
    private readonly TextBlock _vFreq = new() { Text = "—", Classes = { "valor" } };
    private readonly ProgressBar _barraThrottle = new() { Minimum = 0, Maximum = 1, Value = 0, Height = 8 };
    private readonly TextBlock _throttleEstado = new() { Text = "a medir…", Classes = { "mono" } };
    private readonly TextBlock _throttleDetalhe = new() { Text = "", Classes = { "sub" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _energiaTexto = new() { Text = "—", Classes = { "mono" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _alertasLista = new() { Spacing = 6 };
    private readonly TextBlock _alertasEstado = new() { Text = "sem alertas", Classes = { "mono" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    // Sessão gravada (0.20.7)
    private readonly GravadorSessao _gravador = new();
    private readonly Button _botaoGravar = new();
    private readonly TextBlock _sessaoEstado = new() { Text = "sem gravação", Classes = { "mono" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _sessaoGraficos = new() { Spacing = 6 };
    private readonly TextBlock _sessaoResumo = new() { Text = "", Classes = { "mono" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private DateTime? _sessaoFimPedido;
    private readonly CheckBox _notificacoes = new() { Content = "Notificações do sistema" };
    private readonly NumericUpDown _limiteCpu = new() { Minimum = 50, Maximum = 110, Increment = 1, Width = 118, FormatString = "0" };
    private readonly NumericUpDown _limiteGpu = new() { Minimum = 50, Maximum = 110, Increment = 1, Width = 118, FormatString = "0" };
    private readonly NumericUpDown _espacoMin = new() { Minimum = 1, Maximum = 200, Increment = 1, Width = 118, FormatString = "0" };
    private readonly NumericUpDown _tarifa = new()
    {
        Minimum = 0, Maximum = 2, Increment = 0.01m, FormatString = "0.00",
        Value = (decimal)Config.Atual.TarifaEurKwh, Width = 120
    };

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

        // ganchos de teste/depuração: LINUXEXPLODE_SECAO=placa mostra só o cartão da placa-mãe,
        // =sessao mostra só o cartão da sessão gravada (para os poder fotografar no topo da janela).
        var secao = (Environment.GetEnvironmentVariable("LINUXEXPLODE_SECAO") ?? "").ToLowerInvariant();
        bool soPlaca = secao == "placa";
        bool soSessao = secao == "sessao";

        if (!soPlaca && !soSessao)
        {
            _corpo.Children.Add(CartaoAlertas());
            _corpo.Children.Add(CartaoAoVivo());
            _corpo.Children.Add(CartaoThrottle());
            _corpo.Children.Add(CartaoEnergia());
            _corpo.Children.Add(CartaoSessao());
        }
        else if (soSessao)
        {
            _corpo.Children.Add(CartaoSessao());
        }

        if (!soSessao) _corpo.Children.Add(_lista);
        Content = UI.Pagina(cabecalho, _corpo);

        _timer.Tick += async (_, _) => await Atualizar();
        Loaded += async (_, _) => { _timer.Start(); await Atualizar(); };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            if (_gravador.A_Gravar) PararSessao();   // sair da página fecha o CSV em vez de o deixar a meio
        };
    }

    private async Task Atualizar()
    {
        if (_aCarregar) return;
        _aCarregar = true;
        try
        {
            var todos = await Task.Run(Sensores.Ler);

            // leituras usadas pelos cartões novos (uma vez por segundo, não uma por cartão)
            var potenciaAgora = await Task.Run(Potencia.Ler);
            var throttleAgora = await Task.Run(_monitorThrottle.Amostrar);

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
                AtualizarCartaoPotencia(potenciaAgora);

            AtualizarAoVivo(todos, potenciaAgora, throttleAgora);
            AmostrarSessao();
            AtualizarAlertas(todos);
            AtualizarCartaoPlaca(todos);

            bool verPlaca = _filtro is null or TipoSensor.Temperatura;
            Ordenar(verCpu ? _cartaoCpu : null, verPot ? _cartaoPotencia : null, verPlaca ? _cartaoPlaca : null);

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

    // ------------------------------------------------------------------ ao vivo / throttling / energia

    private Control CartaoAoVivo()
    {
        var grelha = UI.Grelha(4, 12,
            BlocoAoVivo("CPU", _vTempCpu, _gTempCpu),
            BlocoAoVivo("GPU", _vTempGpu, _gTempGpu),
            BlocoAoVivo("potência", _vPotencia, _gPotencia),
            BlocoAoVivo("frequência média", _vFreq, _gFreq));

        var pilha = new StackPanel { Spacing = 10 };
        pilha.Children.Add(UI.Texto("últimos 60 s · uma amostra por segundo · temperaturas em °C, potência em W, frequência em GHz", "sub"));
        pilha.Children.Add(grelha);
        return UI.Cartao("Ao vivo", pilha);
    }

    private static Control BlocoAoVivo(string rotulo, TextBlock valor, Sparkline grafico)
    {
        var p = new StackPanel { Spacing = 4 };
        p.Children.Add(UI.Texto(rotulo.ToUpperInvariant(), "rotulo"));
        p.Children.Add(valor);
        p.Children.Add(grafico);
        return p;
    }

    private Control CartaoThrottle()
    {
        var pilha = new StackPanel { Spacing = 8 };
        pilha.Children.Add(_barraThrottle);
        pilha.Children.Add(_throttleEstado);
        pilha.Children.Add(_throttleDetalhe);
        return UI.Cartao("Throttling — frequência efectiva vs máxima", pilha);
    }

    private Control CartaoEnergia()
    {
        var pilha = new StackPanel { Spacing = 10 };
        pilha.Children.Add(_energiaTexto);

        var linha = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        linha.Children.Add(UI.Texto("tarifa (€/kWh)", "chave"));
        linha.Children.Add(_tarifa);
        pilha.Children.Add(linha);

        _tarifa.ValueChanged += (_, e) =>
        {
            if (e.NewValue is not decimal v) return;
            Config.Atual.TarifaEurKwh = (double)v;
            Config.Atual.Guardar();
            AtualizarEnergia();
        };

        return UI.Cartao("Energia e custo", pilha);
    }

    private void AtualizarAoVivo(List<Sensor> todos, LeituraPotencia potencia, LeituraThrottle throttle)
    {
        var cpu = Sensores.CpuTemperatura(todos);
        var gpu = Sensores.GpuTemperatura(todos);
        var watts = potencia.TotalW;

        if (cpu is double c)
        {
            _gTempCpu.Adicionar(c);
            _vTempCpu.Text = $"{c:0} °C";
        }
        else
        {
            _vTempCpu.Text = "sem sensor";
        }

        if (gpu is double g)
        {
            _gTempGpu.Adicionar(g);
            _vTempGpu.Text = $"{g:0} °C";
        }
        else
        {
            _vTempGpu.Text = "sem sensor";
        }

        if (watts is double w)
        {
            _gPotencia.Adicionar(w);
            _vPotencia.Text = $"{w:0} W";
        }
        else
        {
            _vPotencia.Text = "sem RAPL";
        }

        if (throttle.MediaMhz > 0)
        {
            _gFreq.Adicionar(throttle.MediaMhz);
            _vFreq.Text = $"{throttle.MediaMhz / 1000:0.00} GHz";
        }

        _barraThrottle.Value = Math.Clamp(throttle.Percentagem / 100.0, 0, 1);
        _throttleEstado.Text = throttle.MediaMhz <= 0
            ? "sem leitura de cpufreq"
            : $"{throttle.Percentagem:0} % do máximo ({throttle.MediaMhz:0} de {throttle.MaximoMhz:0} MHz) · " +
              $"mínimo {throttle.MinimoMhz:0} MHz · carga {throttle.CargaCpu * 100:0} %";

        var notas = new List<string> { throttle.Veredito };
        if (throttle.ContadorFirmware is long fw) notas.Add($"contador de firmware {fw:N0}");
        else notas.Add("o firmware não expõe contadores de throttle (normal em AMD)");
        _throttleDetalhe.Text = string.Join(" · ", notas);

        _medidorEnergia.Amostrar(potencia.TotalW);
        AtualizarEnergia();
    }

    private void AtualizarEnergia()
    {
        var custo = _medidorEnergia.Calcular(Config.Atual.TarifaEurKwh);

        if (custo.Aviso is not null && custo.EurDia is null)
        {
            _energiaTexto.Text = "sem leitura de potência — não é possível estimar o custo (" + custo.Aviso + ")";
            return;
        }

        string tempo = custo.Horas >= 1 / 60.0
            ? $"{custo.Horas * 60:0} min"
            : $"{custo.Horas * 3600:0} s";

        _energiaTexto.Text =
            $"nesta sessão: {custo.Wh:0.000} Wh em {tempo} · média {custo.MediaW:0} W\n" +
            $"projecção a este ritmo: {custo.EurDia:0.00} €/dia · {custo.EurMes:0.00} €/mês\n" +
            "a média é a do tempo em que a janela esteve aberta (inclui o PC parado)";
    }

    // ------------------------------------------------------------------ alertas

    /// <summary>
    /// Cartão da placa-mãe: temperatura do chipset, dos módulos de memória, do NVMe, da NIC e do
    /// Wi-Fi — os sensores hwmon que não são CPU nem GPU, com a origem identificada. O que não tem
    /// sensor (VRM) é dito, não estimado.
    /// </summary>
    private void AtualizarCartaoPlaca(List<Sensor> sensores)
    {
        var temps = Placa.Temperaturas(sensores);
        var faltas = Placa.SemSensor(temps);

        string chave = string.Join("|", temps.Select(t => t.Chip + t.Nome + t.Texto)) + "#" + string.Join(",", faltas);
        if (chave == _placaChave) return;
        _placaChave = chave;

        if (temps.Count == 0)
        {
            _placaLista.Children.Clear();
            _placaLista.Children.Add(UI.Texto("Nenhum sensor de temperatura fora da CPU e da GPU nesta máquina.", "sub"));
            _cartaoPlaca ??= UI.Cartao("Placa-mãe, chipset e memória", Envolver(_placaLista));
            return;
        }

        _placaLista.Children.Clear();
        foreach (var t in temps)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*,Auto") };

            var fonte = UI.Texto(t.Fonte, "rotulo");
            fonte.VerticalAlignment = VerticalAlignment.Center;

            var nome = UI.Texto($"{t.Chip} · {t.Nome}", "chave");
            nome.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
            nome.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(nome, 1);

            var valor = UI.Texto(t.Texto, "valor");
            valor.HorizontalAlignment = HorizontalAlignment.Right;
            valor.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(valor, 2);

            g.Children.Add(fonte);
            g.Children.Add(nome);
            g.Children.Add(valor);

            var linha = new Border { Child = g };
            linha.Classes.Add("linha");
            _placaLista.Children.Add(linha);
        }

        _placaNota.Text = faltas.Count > 0
            ? $"sem sensor nesta máquina: {string.Join(", ", faltas)} — a aplicação não inventa valores."
            : "sensores lidos de /sys/class/hwmon; uma leitura ausente fica em branco em vez de zero.";

        if (_cartaoPlaca is null)
            _cartaoPlaca = UI.Cartao("Placa-mãe, chipset e memória", Envolver(_placaLista));
    }

    private Control Envolver(Control conteudo)
    {
        var pilha = new StackPanel { Spacing = 8 };
        pilha.Children.Add(conteudo);
        pilha.Children.Add(_placaNota);
        return pilha;
    }

    /// <summary>
    /// Sessão de monitorização gravada (item 18): uma amostra por segundo para CSV e, no fim, o resumo e o
    /// gráfico do que aconteceu. Serve para ver o que se passou durante um jogo ou uma compilação sem
    /// estar a olhar para os números. As linhas são escritas à medida que são lidas, por isso fechar a
    /// aplicação a meio não perde o que já foi medido.
    /// </summary>
    private Control CartaoSessao()
    {
        var conteudo = new StackPanel { Spacing = 12 };
        conteudo.Children.Add(UI.Texto(
            "Grava CPU, GPU, potência e frequência uma vez por segundo em " +
            "~/.local/share/linux-explode/sessoes. No fim ficam aqui o resumo e o gráfico da sessão inteira.", "sub"));

        _botaoGravar.Content = "Gravar sessão";
        _botaoGravar.Classes.Add("primario");
        _botaoGravar.Click += (_, _) =>
        {
            if (_gravador.A_Gravar) PararSessao();
            else IniciarSessao();
        };

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        botoes.Children.Add(_botaoGravar);
        conteudo.Children.Add(botoes);
        conteudo.Children.Add(_sessaoEstado);
        conteudo.Children.Add(_sessaoGraficos);
        conteudo.Children.Add(_sessaoResumo);

        // gancho de verificação: LINUXEXPLODE_GRAVAR=<segundos> grava sozinho ao entrar na página
        if (int.TryParse(Environment.GetEnvironmentVariable("LINUXEXPLODE_GRAVAR"), out int segs) && segs > 0)
            Loaded += (_, _) =>
            {
                IniciarSessao();
                _sessaoFimPedido = DateTime.Now.AddSeconds(segs);
            };

        return UI.Cartao("Sessão gravada · monitorização em CSV", conteudo);
    }

    private void IniciarSessao()
    {
        string caminho = _gravador.Iniciar();
        _sessaoGraficos.Children.Clear();
        _sessaoResumo.Text = "";

        _botaoGravar.Content = "Parar e ver";
        _sessaoEstado.Text = _gravador.FalhouEscrita
            ? $"a gravar mas o CSV não pôde ser escrito — amostras só em memória ({caminho})"
            : $"a gravar · 0 amostras · {caminho}";
    }

    private void PararSessao()
    {
        var (amostras, resumo) = _gravador.Parar();
        _botaoGravar.Content = "Gravar sessão";

        _sessaoEstado.Text = $"{amostras.Count} amostras em {resumo.Duracao.TotalSeconds:0} s · {_gravador.Caminho}";
        if (resumo.Amostras == 0)
        {
            _sessaoResumo.Text = "nada gravado";
            return;
        }

        // Uma sparkline por série, alimentada com a sessão inteira (o controlo guarda as últimas N).
        _sessaoGraficos.Children.Clear();
        AdicionarSerie("CPU", amostras.Select(a => a.CpuC), "°C", "Bom", 90, resumo.Cpu);
        AdicionarSerie("GPU", amostras.Select(a => a.GpuC), "°C", "Bom", 90, resumo.Gpu);
        AdicionarSerie("potência", amostras.Select(a => a.Watts), "W", "Fg", 0, resumo.Watts);
        AdicionarSerie("frequência", amostras.Select(a => a.FreqMhz), "MHz", "Atencao", 0, resumo.Freq);

        _sessaoResumo.Text = $"CPU {TextoResumo(resumo.Cpu, "°C")} · GPU {TextoResumo(resumo.Gpu, "°C")}\n" +
                             $"potência {TextoResumo(resumo.Watts, "W")} · frequência {TextoResumo(resumo.Freq, "MHz")}";

        static string TextoResumo((double? Min, double? Max, double? Media) t, string unidade) =>
            t.Media is double m ? $"{m:0.0} {unidade} (min {t.Min:0.0}, max {t.Max:0.0})" : "sem leitura";
    }

    private void AdicionarSerie(string nome, IEnumerable<double?> valores, string unidade, string cor,
        double limite, (double? Min, double? Max, double? Media) resumo)
    {
        var serie = valores.Where(v => v is not null).Select(v => v!.Value).ToList();
        if (serie.Count < 2) return;

        var cabecalho = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        cabecalho.Children.Add(UI.Texto(nome, "rotulo"));
        cabecalho.Children.Add(UI.Texto($"{resumo.Media:0.0} {unidade}", "mono"));
        _sessaoGraficos.Children.Add(cabecalho);

        // capacidade = amostras (até 600): uma sessão de 10 s ocupa a largura toda em vez de ficar
        // espremida no primeiro sexto do gráfico. Acima de 600 mostra-se o fim da sessão.
        var grafico = new Sparkline(Math.Clamp(serie.Count, 2, 600)) { Cor = cor, Limite = limite };
        foreach (double v in serie) grafico.Adicionar(v);
        _sessaoGraficos.Children.Add(grafico);
    }

    /// <summary>Chamado no ciclo de 1 s: guarda a amostra se houver gravação em curso (ou se o gancho já pediu o fim).</summary>
    private void AmostrarSessao()
    {
        if (!_gravador.A_Gravar) return;

        _gravador.Amostrar();

        if (_sessaoFimPedido is DateTime fim && DateTime.Now >= fim)
        {
            PararSessao();
            _sessaoFimPedido = null;
            return;
        }

        if (!_gravador.FalhouEscrita)
            _sessaoEstado.Text = $"a gravar · {_gravador.Amostras} amostras · {_gravador.Caminho}";
        else
            _sessaoEstado.Text = $"a gravar (falha ao escrever o CSV) · {_gravador.Amostras} amostras em memória";
    }

    private Control CartaoAlertas()
    {
        _notificacoes.IsChecked = Config.Atual.Notificacoes;
        _notificacoes.IsEnabled = Notificacoes.Disponivel;
        Avalonia.Controls.ToolTip.SetTip(_notificacoes, Notificacoes.Disponivel
            ? "Avisa com notify-send quando um limite é passado (no máximo um aviso por regra a cada 15 min)"
            : "notify-send não existe nesta instalação (o Flatpak não o traz) — os alertas continuam na interface");

        _notificacoes.IsCheckedChanged += (_, _) =>
        {
            Config.Atual.Notificacoes = _notificacoes.IsChecked == true;
            Config.Atual.Guardar();
        };

        _limiteCpu.Value = (decimal)Config.Atual.LimiteCpuAviso;
        _limiteGpu.Value = (decimal)Config.Atual.LimiteGpuAviso;
        _espacoMin.Value = (decimal)Config.Atual.EspacoMinimoGb;

        void Guardar(string campo, decimal? valor)
        {
            if (valor is not decimal v) return;
            switch (campo)
            {
                case "cpu": Config.Atual.LimiteCpuAviso = (double)v; break;
                case "gpu": Config.Atual.LimiteGpuAviso = (double)v; break;
                default: Config.Atual.EspacoMinimoGb = (double)v; break;
            }
            Config.Atual.Guardar();
        }

        _limiteCpu.ValueChanged += (_, e) => Guardar("cpu", e.NewValue);
        _limiteGpu.ValueChanged += (_, e) => Guardar("gpu", e.NewValue);
        _espacoMin.ValueChanged += (_, e) => Guardar("disco", e.NewValue);

        // Limites e notificações num WrapPanel: com a janela estreita (ou a escala a 135 %) a caixa
        // das notificações desce para a linha seguinte em vez de ser cortada na margem direita.
        var limites = new WrapPanel { Orientation = Orientation.Horizontal };

        Control GrupoLimite(string rotulo, Control campo)
        {
            var grupo = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Avalonia.Thickness(0, 0, 18, 8),
                VerticalAlignment = VerticalAlignment.Center
            };
            grupo.Children.Add(UI.Texto(rotulo, "chave"));
            grupo.Children.Add(campo);
            return grupo;
        }

        limites.Children.Add(GrupoLimite("CPU °C", _limiteCpu));
        limites.Children.Add(GrupoLimite("GPU °C", _limiteGpu));
        limites.Children.Add(GrupoLimite("disco GB", _espacoMin));
        _notificacoes.VerticalAlignment = VerticalAlignment.Center;
        _notificacoes.Margin = new Avalonia.Thickness(0, 0, 0, 8);
        limites.Children.Add(_notificacoes);

        var pilha = new StackPanel { Spacing = 10 };
        pilha.Children.Add(_alertasEstado);
        pilha.Children.Add(_alertasLista);
        pilha.Children.Add(limites);
        return UI.Cartao("Alertas", pilha);
    }

    private string _alertasChave = "";

    private void AtualizarAlertas(List<Sensor> sensores)
    {
        var alertas = Alertas.Avaliar(sensores);
        Notificacoes.AvisarAlertas(alertas);

        _alertasEstado.Text = alertas.Count == 0
            ? "sem alertas — temperaturas, ventoinhas e espaço dentro dos limites"
            : $"{alertas.Count} alerta(s) activo(s)";

        string chave = string.Join("|", alertas.Select(a => a.Chave));
        if (chave == _alertasChave) return;
        _alertasChave = chave;

        _alertasLista.Children.Clear();
        foreach (var a in alertas)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            linha.Children.Add(UI.Pilula(a.Grave ? "grave" : "aviso", a.Grave ? "Mau" : "Atencao"));
            var texto = new StackPanel { Spacing = 2 };
            texto.Children.Add(UI.Texto(a.Titulo, "valor"));
            texto.Children.Add(UI.Texto(a.Detalhe, "sub"));
            linha.Children.Add(texto);
            _alertasLista.Children.Add(linha);
        }
    }
}

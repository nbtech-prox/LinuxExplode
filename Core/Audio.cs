using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>Placa de som do ALSA, lida de <c>/proc/asound/cards</c>.</summary>
public sealed record PlacaSom(int Indice, string Id, string Nome, string Driver, string? Barramento)
{
    public string Rotulo => $"card{Indice} · {Id}";
}

/// <summary>Fluxo PCM (reprodução ou captura) e o estado que o kernel reporta em /proc/asound.</summary>
public sealed record FluxoAlsa(
    string Placa,
    string Pcm,
    string Dispositivo,
    bool Reproducao,
    string Estado,
    int? Taxa,
    int? Canais,
    string? Formato,
    long? PeriodoBytes,
    long? BufferBytes,
    long? AtrasoFrames)
{
    public string Direcao => Reproducao ? "reprodução" : "captura";

    /// <summary>Latência do período do buffer (o que o ALSA chama de period_size).</summary>
    public double? LatenciaMs =>
        Taxa is int r && r > 0 && PeriodoBytes is long p && p > 0 && BytesPorFrame is int b && b > 0
            ? p / (double)b / r * 1000.0
            : null;

    /// <summary>Buffer total configurado, em milissegundos.</summary>
    public double? BufferMs =>
        Taxa is int r && r > 0 && BufferBytes is long bf && bf > 0 && BytesPorFrame is int b && b > 0
            ? bf / (double)b / r * 1000.0
            : null;

    /// <summary>Atraso actual do fluxo (frames por preencher) em milissegundos.</summary>
    public double? AtrasoMs =>
        Taxa is int r && r > 0 && AtrasoFrames is long a ? a / (double)r * 1000.0 : null;

    public int? BytesPorFrame => Formato is { Length: > 0 } f && Canais is int c ? Audio.BytesPorFrame(f) * c : null;

    public bool Activo => Estado.Equals("RUNNING", StringComparison.OrdinalIgnoreCase);

    public string Texto => $"{Placa} · {Pcm}" + (Dispositivo.Length > 0 ? $" ({Dispositivo})" : "") +
                           $" — {Estado.ToLowerInvariant()}" +
                           (Taxa is int t ? $", {t} Hz" : "") +
                           (Canais is int ch ? $", {ch} canais" : "") +
                           (Formato is { Length: > 0 } fo ? $", {fo}" : "") +
                           (LatenciaMs is double l ? $", período {l:0.0} ms" : "") +
                           (BufferMs is double b ? $", buffer {b:0.0} ms" : "");
}

/// <summary>Nó do PipeWire com contador de xruns (coluna ERR do <c>pw-top</c>).</summary>
public sealed record NoAudio(int Id, string Nome, long Quantum, int Taxa, long Xruns, bool Driver)
{
    /// <summary>Duração de um quantum, à taxa do nó.</summary>
    public double? QuantumMs => Taxa > 0 && Quantum > 0 ? Quantum / (double)Taxa * 1000.0 : null;
}

/// <summary>Tudo o que o cartão de áudio mostra de uma vez.</summary>
public sealed record LeituraAudio(
    List<PlacaSom> Placas,
    List<FluxoAlsa> Fluxos,
    List<NoAudio> Nos,
    double? LatenciaPadraoMs,
    string? Aviso)
{
    public long Xruns => Nos.Sum(n => n.Xruns);
    public List<FluxoAlsa> Activos => Fluxos.Where(f => f.Activo).ToList();
}

/// <summary>
/// Áudio do sistema: placas ALSA, fluxos activos e contagem de xruns do PipeWire. Os xruns vêm da
/// coluna ERR do <c>pw-top</c>, que é um contador do servidor — não uma estimativa nossa.
/// </summary>
public static class Audio
{
    private static readonly string[] _formatosPcm =
    {
        "S8", "U8", "S16_LE", "S16_BE", "U16_LE", "U16_BE", "S24_LE", "S24_BE", "S24_3LE", "S24_3BE",
        "S32_LE", "S32_BE", "FLOAT_LE", "FLOAT_BE", "FLOAT64_LE", "FLOAT64_BE", "IEC958_SUBFRAME_LE",
        "MU_LAW", "A_LAW", "IMA_ADPCM", "MPEG", "GSM", "SPECIAL", "DSD_U8", "DSD_U16_LE", "DSD_U32_LE"
    };

    /// <summary>Tabela de referência dos formatos PCM do ALSA (documentação do kernel).</summary>
    public static IReadOnlyList<string> FormatosConhecidos => _formatosPcm;

    /// <summary>Bytes por amostra de um formato ALSA ("S16_LE" → 2).</summary>
    public static int BytesPorFrame(string formato)
    {
        var f = formato.Trim().ToUpperInvariant();
        return f switch
        {
            "S8" or "U8" or "MU_LAW" or "A_LAW" or "DSD_U8" => 1,
            "S16_LE" or "S16_BE" or "U16_LE" or "U16_BE" or "DSD_U16_LE" => 2,
            "S24_LE" or "S24_BE" or "S24_3LE" or "S24_3BE" or "S32_LE" or "S32_BE" or "FLOAT_LE" or "FLOAT_BE" or "DSD_U32_LE" => 4,
            "FLOAT64_LE" or "FLOAT64_BE" => 8,
            _ => 4
        };
    }

    // ------------------------------------------------------------------ leitura completa

    public static LeituraAudio Ler(string raizAsound = "/proc/asound")
    {
        var placas = LerPlacas(raizAsound);
        var fluxos = LerFluxos(raizAsound, placas);
        var (nos, avisoPw) = LerPipeWire();

        double? latencia = null;
        var driverPadrao = nos.FirstOrDefault(n => n.Driver && n.Quantum > 0 && n.Taxa > 0);
        if (driverPadrao is not null) latencia = driverPadrao.QuantumMs;

        // Sem um nó activo (máquina em silêncio) o pw-top não mostra quantum: usa-se o valor
        // configurado no servidor, que é igualmente real.
        if (latencia is null)
        {
            var (taxa, quantum, avisoMeta) = LerConfigPipeWire();
            if (taxa is > 0 && quantum is > 0) latencia = quantum / (double)taxa * 1000.0;
            else if (avisoPw is null) avisoPw = avisoMeta;
        }

        return new LeituraAudio(placas, fluxos, nos, latencia, avisoPw);
    }

    /// <summary>Quantum e taxa configurados no servidor (<c>pw-metadata -n settings</c>).</summary>
    public static (int? Taxa, int? Quantum, string? Aviso) LerConfigPipeWire()
    {
        var saida = Util.Comando("pw-metadata", "-n", "settings");
        if (string.IsNullOrWhiteSpace(saida) && Util.EmFlatpak)
            saida = Util.Comando("flatpak-spawn", "--host", "pw-metadata", "-n", "settings");

        if (string.IsNullOrWhiteSpace(saida))
            return (null, null, "pw-metadata indisponível — quantum do servidor não lido");

        int? taxa = null, quantum = null;
        foreach (var linha in saida.Split('\n'))
        {
            if (!linha.Contains("key:'clock.", StringComparison.Ordinal)) continue;
            var valor = Extrair(linha, "value:'");
            if (!int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v <= 0) continue;

            if (linha.Contains("key:'clock.rate'", StringComparison.Ordinal)) taxa = v;
            else if (linha.Contains("key:'clock.quantum'", StringComparison.Ordinal)) quantum = v;
        }
        return (taxa, quantum, null);
    }

    private static string Extrair(string linha, string marca)
    {
        var i = linha.IndexOf(marca, StringComparison.Ordinal);
        if (i < 0) return "";
        i += marca.Length;
        var fim = linha.IndexOf('\'', i);
        return fim > i ? linha[i..fim] : "";
    }

    /// <summary>Placas de som de <c>/proc/asound/cards</c>.</summary>
    public static List<PlacaSom> LerPlacas(string raiz = "/proc/asound")
    {
        var lista = new List<PlacaSom>();
        var linhas = Util.Ler(Path.Combine(raiz, "cards")).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < linhas.Length; i++)
        {
            var l = linhas[i];
            // " 0 [HDMI           ]: HDA-Intel - HDA ATI HDMI"
            var ini = l.IndexOf('[');
            var fim = l.IndexOf(']');
            if (ini < 0 || fim < ini) continue;

            var indiceTexto = l[..ini].Trim();
            if (!int.TryParse(indiceTexto, out var indice)) continue;

            var id = l[(ini + 1)..fim].Trim();
            var resto = l[(fim + 1)..].TrimStart(' ', ':');
            var hifen = resto.IndexOf(" - ", StringComparison.Ordinal);
            var driver = hifen > 0 ? resto[..hifen].Trim() : resto.Trim();
            var nome = hifen > 0 ? resto[(hifen + 3)..].Trim() : id;

            // a linha seguinte traz o barramento ("HDA ATI HDMI at 0xdd920000 irq 139")
            string? barramento = null;
            if (i + 1 < linhas.Length && !char.IsDigit(linhas[i + 1].TrimStart().FirstOrDefault(' ')))
            {
                var detalhe = linhas[i + 1].Trim();
                var at = detalhe.IndexOf(" at ", StringComparison.Ordinal);
                var usb = detalhe.IndexOf(" at usb-", StringComparison.Ordinal);
                if (usb > 0) barramento = "usb " + detalhe[(usb + 4)..].Split(' ')[0].TrimEnd(',').Replace("usb-", "").Replace("0000:", "");
                else if (at > 0) barramento = detalhe[(at + 4)..].Split(' ')[0].TrimEnd(',');
                i++;
            }

            lista.Add(new PlacaSom(indice, id, nome, driver, barramento));
        }
        return lista;
    }

    /// <summary>Fluxos PCM com estado em <c>/proc/asound/cardN/pcmXp/subY/status</c>.</summary>
    public static List<FluxoAlsa> LerFluxos(string raiz = "/proc/asound", List<PlacaSom>? placas = null)
    {
        var lista = new List<FluxoAlsa>();
        if (!Directory.Exists(raiz)) return lista;

        foreach (var dirCard in Directory.GetDirectories(raiz).Where(d => Path.GetFileName(d).StartsWith("card")).OrderBy(x => x))
        {
            var nomeCard = Path.GetFileName(dirCard);
            if (!int.TryParse(new string(nomeCard.Where(char.IsDigit).ToArray()), out var indice)) continue;
            var apelido = placas?.FirstOrDefault(p => p.Indice == indice)?.Id ?? nomeCard;

            foreach (var dirPcm in Directory.GetDirectories(dirCard).Where(d => Path.GetFileName(d).StartsWith("pcm")).OrderBy(x => x))
            {
                var nomePcm = Path.GetFileName(dirPcm);
                var reprod = nomePcm.EndsWith('p');
                var info = Campos(Util.Ler(dirPcm + "/info"));
                var descricao = info.TryGetValue("name", out var dn) && dn.Length > 0 ? dn : nomePcm;

                foreach (var dirSub in Directory.GetDirectories(dirPcm).Where(d => Path.GetFileName(d).StartsWith("sub")).OrderBy(x => x))
                {
                    var estado = Util.Ler(dirSub + "/status");
                    if (estado.Length == 0) continue;

                    var campos = Campos(estado);
                    // um PCM fechado escreve apenas "closed", sem pares chave: valor
                    var state = campos.TryGetValue("state", out var s1) ? s1
                              : estado.StartsWith("closed", StringComparison.OrdinalIgnoreCase) ? "closed"
                              : null;
                    if (state is null) continue;

                    var hw = Campos(Util.Ler(dirSub + "/hw_params"));
                    int? taxa = null, canais = null;
                    if (hw.TryGetValue("rate", out var rt))
                    {
                        var p = rt.Split(' ')[0];
                        if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t)) taxa = t;
                    }
                    if (hw.TryGetValue("channels", out var ch) && int.TryParse(ch, out var c2)) canais = c2;

                    lista.Add(new FluxoAlsa(
                        apelido,
                        nomePcm,
                        descricao,
                        reprod,
                        state,
                        taxa,
                        canais,
                        hw.TryGetValue("format", out var fm) ? fm : null,
                        hw.TryGetValue("period_size", out var pe) && long.TryParse(pe, out var pv) ? pv : null,
                        hw.TryGetValue("buffer_size", out var bu) && long.TryParse(bu, out var bv) ? bv : null,
                        campos.TryGetValue("delay", out var de) && long.TryParse(de, out var dv) ? dv : null));
                }
            }
        }
        return lista;
    }

    private static Dictionary<string, string> Campos(string texto)
    {
        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var linha in texto.Split('\n'))
        {
            var i = linha.IndexOf(':');
            if (i <= 0) continue;
            mapa[linha[..i].Trim()] = linha[(i + 1)..].Trim();
        }
        return mapa;
    }

    // ------------------------------------------------------------------ PipeWire

    /// <summary>
    /// Lê o contador de xruns do PipeWire. Devolve a lista de nós e uma nota quando o servidor não
    /// está acessível (por exemplo dentro de um sandbox sem o socket do PipeWire).
    /// </summary>
    public static (List<NoAudio> Nos, string? Aviso) LerPipeWire()
    {
        var saida = Util.Comando("pw-top", "-b", "-n", "2");
        if (string.IsNullOrWhiteSpace(saida) && Util.EmFlatpak)
            saida = Util.Comando("flatpak-spawn", "--host", "pw-top", "-b", "-n", "2");

        if (string.IsNullOrWhiteSpace(saida))
            return (new List<NoAudio>(), "PipeWire não respondeu (pw-top indisponível ou servidor inacessível) — xruns não medidos");

        return (ParsePwTop(saida), null);
    }

    /// <summary>
    /// Interpreta a saída do <c>pw-top</c>. As colunas são fixas (ID, QUANT, RATE, WAIT, BUSY, W/Q,
    /// B/Q, ERR, FORMAT, NAME); o ERR é o número de xruns acumulado no servidor para esse nó.
    /// </summary>
    public static List<NoAudio> ParsePwTop(string saida)
    {
        var lista = new List<NoAudio>();
        if (string.IsNullOrWhiteSpace(saida)) return lista;

        foreach (var linha in saida.Split('\n'))
        {
            var t = linha.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 10) continue;
            if (t[0] != "S" && t[0] != "C") continue;
            if (!int.TryParse(t[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) continue;

            long quantum = long.TryParse(t[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) ? q : 0;
            int taxa = int.TryParse(t[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : 0;
            long erros = long.TryParse(t[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var e) ? e : 0;
            var nome = t[^1];

            var existente = lista.FirstOrDefault(n => n.Id == id);
            if (existente is not null) lista.Remove(existente);

            lista.Add(new NoAudio(id, nome, quantum, taxa, erros, t[0] == "S"));
        }

        return lista.OrderByDescending(n => n.Xruns).ThenBy(n => n.Id).ToList();
    }

    /// <summary>Texto de resumo para a linha de comandos.</summary>
    public static string Resumo(LeituraAudio a)
    {
        var partes = new List<string>
        {
            $"{a.Placas.Count} placa(s) de som",
            $"{a.Activos.Count} fluxo(s) activo(s) de {a.Fluxos.Count} PCM"
        };
        if (a.Nos.Count > 0) partes.Add($"{a.Xruns} xrun(s) em {a.Nos.Count} nó(s) do PipeWire");
        if (a.LatenciaPadraoMs is double l) partes.Add($"quantum {l:0.00} ms");
        return string.Join(" · ", partes);
    }
}

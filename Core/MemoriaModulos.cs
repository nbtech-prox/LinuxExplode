using System.Buffers.Binary;
using System.Text;

namespace LinuxExplode.Core;

/// <summary>Um módulo de memória. Vem do SPD (EEPROM do módulo) ou, em último caso, do SMBIOS.</summary>
public sealed record ModuloRam(
    string Posicao,
    string PartNumber,
    string Fabricante,
    long CapacidadeBytes,
    int? VelocidadeMts,
    int? VelocidadeJedecMts,
    string Tipo,
    string Formato,
    bool Ecc,
    string Fabrico,
    string Fonte)
{
    public string CapacidadeTexto
    {
        get
        {
            if (CapacidadeBytes <= 0) return "—";
            double gb = CapacidadeBytes / (1024.0 * 1024 * 1024);
            // 32 GB lê-se melhor que 32.0 GB; valores não inteiros mantêm o decimal
            return Math.Abs(gb - Math.Round(gb)) < 0.05 ? $"{Math.Round(gb):0} GB" : Util.Gb(CapacidadeBytes);
        }
    }

    /// <summary>"32 GB DDR5-5200" (sem o "-5200" quando não há velocidade conhecida).</summary>
    public string Descricao
    {
        get
        {
            var vel = VelocidadeMts is int v ? $"-{v}" : "";
            var ecc = Ecc ? " ECC" : "";
            return $"{CapacidadeTexto} {Tipo}{vel}{ecc}";
        }
    }

    public string Detalhe
    {
        get
        {
            var partes = new List<string>();
            if (Formato is "RDIMM" or "LRDIMM" or "Mini-UDIMM") partes.Add(Formato);
            if (PartNumber.Length > 0) partes.Add(PartNumber);
            if (VelocidadeJedecMts is int j && VelocidadeMts is int x && j != x) partes.Add($"JEDEC {j}");
            if (Fabrico.Length > 0) partes.Add(Fabrico);
            if (Fonte == "SMBIOS") partes.Add("via SMBIOS");
            return string.Join(" · ", partes);
        }
    }
}

/// <summary>
/// Módulos de memória sem root e sem depender da máquina: tenta o SPD do próprio módulo e,
/// se não houver, o SMBIOS.
///
/// SPD:
///  • DDR5 — /sys/bus/nvmem/devices/&lt;bus&gt;-&lt;addr&gt;/nvmem (módulo spd5118), 1024 bytes,
///    JESD400-5 (base 0x00-0x1FF, fabricante 0x200-0x27F, XMP 3.0 em 0x280+).
///  • DDR4 — /sys/bus/nvmem/devices/... ou /sys/bus/i2c/devices/&lt;bus&gt;-005x/eeprom
///    (módulo ee1004), 512 bytes, SPD Annex L.
///
/// SMBIOS: /sys/firmware/dmi/entries/17-*/raw — é 0400 root em muitos kernels, mas em várias
/// distribuições é legível; nesse caso serve de alternativa sem privilégios.
/// </summary>
public static class MemoriaModulos
{
    private static readonly Dictionary<int, string> TiposDdr = new()
    {
        [0x01] = "RDIMM", [0x02] = "UDIMM", [0x03] = "SODIMM", [0x04] = "LRDIMM",
        [0x06] = "Mini-UDIMM", [0x09] = "SODIMM", [0x0C] = "UDIMM"
    };

    private static readonly Dictionary<int, string> TiposSmbios = new()
    {
        [0x12] = "DDR", [0x13] = "DDR2", [0x18] = "DDR3", [0x1A] = "DDR4", [0x22] = "DDR5",
        [0x1B] = "LPDDR", [0x1C] = "LPDDR2", [0x1D] = "LPDDR3", [0x1E] = "LPDDR4", [0x23] = "LPDDR5",
        [0x20] = "HBM", [0x21] = "HBM2", [0x24] = "HBM3"
    };

    public static List<ModuloRam> Ler()
    {
        var spd = LerSpd();
        return spd.Count > 0 ? spd : LerSmbios();
    }

    // ───────────────────────────── SPD ─────────────────────────────

    private static List<ModuloRam> LerSpd()
    {
        var modulos = new List<ModuloRam>();

        // diagnóstico: LINUXEXPLODE_MEMORIA_TESTE=<ficheiro|diretório> descodifica dumps em vez de /sys
        // (aceita SPD em bruto e estruturas SMBIOS tipo 17 guardadas como 17-*.raw)
        var teste = Environment.GetEnvironmentVariable("LINUXEXPLODE_MEMORIA_TESTE");
        if (!string.IsNullOrWhiteSpace(teste))
        {
            IEnumerable<string> ficheiros = Directory.Exists(teste)
                ? Directory.GetFiles(teste).OrderBy(f => f, StringComparer.Ordinal)
                : new[] { teste };
            foreach (var f in ficheiros)
            {
                try
                {
                    var nome = Path.GetFileName(f);
                    if (nome.StartsWith("17-", StringComparison.Ordinal))
                    {
                        var dmi = Smbios17(File.ReadAllBytes(f), modulos.Count + 1);
                        if (dmi is not null) modulos.Add(dmi);
                    }
                    else
                    {
                        var spdMod = Descodificar(File.ReadAllBytes(f), modulos.Count + 1, nome);
                        if (spdMod is not null) modulos.Add(spdMod);
                    }
                }
                catch { /* dump inválido */ }
            }
            return modulos;
        }

        // nvmem: DDR5 (spd5118) e DDR4 (ee1004, em kernels recentes)
        const string raizNvmem = "/sys/bus/nvmem/devices";
        if (Directory.Exists(raizNvmem))
        {
            foreach (var dir in Directory.GetDirectories(raizNvmem).OrderBy(d => d, StringComparer.Ordinal))
            {
                var nome = Path.GetFileName(dir);
                if (!EhSpd(nome)) continue;
                try
                {
                    var m = Descodificar(File.ReadAllBytes(Path.Combine(dir, "nvmem")), modulos.Count + 1, nome);
                    if (m is not null) modulos.Add(m);
                }
                catch { /* sem permissão */ }
            }
        }

        // DDR4 clássico: o ee1004 expõe a EEPROM por i2c
        if (modulos.Count == 0 && Directory.Exists("/sys/bus/i2c/devices"))
        {
            foreach (var dir in Directory.GetDirectories("/sys/bus/i2c/devices").OrderBy(d => d, StringComparer.Ordinal))
            {
                var nome = Path.GetFileName(dir);
                if (!EhSpd(nome)) continue;
                var eeprom = Path.Combine(dir, "eeprom");
                if (!File.Exists(eeprom)) continue;
                try
                {
                    var m = Descodificar(File.ReadAllBytes(eeprom), modulos.Count + 1, nome);
                    if (m is not null) modulos.Add(m);
                }
                catch { /* eeprom costuma ser 0400 em alguns kernels */ }
            }
        }

        return modulos;
    }

    /// <summary>Diz se o dispositivo é um SPD (e não o CMOS, um SSD ou um sensor qualquer).</summary>
    private static bool EhSpd(string nome)
    {
        foreach (var raiz in new[] { "/sys/bus/i2c/devices/", "/sys/bus/nvmem/devices/" })
        {
            try
            {
                var n = File.ReadAllText(raiz + nome + "/name").Trim();
                if (n is "spd5118" or "ee1004") return true;
            }
            catch { /* sem ficheiro name — tenta a próxima raiz */ }
        }
        return false;
    }

    /// <summary>Descodifica um dump SPD; devolve null quando não é DDR4/DDR5 reconhecível.</summary>
    private static ModuloRam? Descodificar(byte[] d, int indice, string origem)
    {
        if (d.Length < 128) return null;
        return d[2] switch
        {
            0x12 when d.Length >= 640 => Ddr5(d, indice),
            0x0C or 0x0E when d.Length >= 128 => Ddr4(d, indice),
            _ => null
        };
    }

    // ───────────────────────────── DDR5 ─────────────────────────────

    private static readonly Dictionary<int, long> DensidadeDdr5 = new()
    {
        [1] = 512, [2] = 1024, [3] = 1536, [4] = 2048, [5] = 3072, [6] = 4096, [7] = 6144, [8] = 8192
    };

    private static ModuloRam Ddr5(byte[] d, int indice)
    {
        long mb = 0;
        for (int volta = 1; volta <= 2; volta++)
        {
            byte b = d[volta * 4];
            if (!DensidadeDdr5.TryGetValue(b & 0x1F, out var atual)) break;

            int dies = (b >> 5) & 0x07;
            if (dies is > 1 and <= 5) atual *= 1L << (dies - 1);

            byte barramento = d[235];
            if (((barramento >> 5) & 0x03) == 1) atual *= 2;      // dois canais por DIMM
            atual *= 1L << ((barramento & 0x03) + 3);             // largura primária

            byte larguraIo = d[volta * 4 + 2];
            atual /= 1L << (((larguraIo >> 5) & 0x03) + 2);       // largura de I/O do die

            byte ranks = d[234];
            atual *= 1L << ((ranks >> 3) & 0x07);

            mb += atual;
            if (((ranks >> 6) & 0x01) == 0) break;                 // simétrico: só um bloco
        }

        int? jedec = VelocidadeDdr5(d[21] << 8 | d[20]);
        int? xmp = VelocidadeDdr5(d[0x2C6] << 8 | d[0x2C5]) ?? VelocidadeDdr5(d[0x306] << 8 | d[0x305]);

        return new ModuloRam(
            Posicao: $"Módulo {indice}",
            PartNumber: Ascii(d, 0x209, 30),
            Fabricante: Fabricante(d[0x200], d[0x201]),
            CapacidadeBytes: mb * 1024L * 1024L,
            VelocidadeMts: xmp ?? jedec,
            VelocidadeJedecMts: jedec,
            Tipo: "DDR5",
            Formato: FormatoModulo(d[3]),
            Ecc: ((d[235] >> 3) & 3) > 0,
            Fabrico: Fabrico(d[0x203], d[0x204]),
            Fonte: "SPD");
    }

    /// <summary>tCK em ps -> MT/s arredondado à centena; null quando reservado.</summary>
    private static int? VelocidadeDdr5(int tckPs)
    {
        if (tckPs < 100) return null;
        return Arredondar(2_000_000.0 / tckPs);
    }

    // ───────────────────────────── DDR4 ─────────────────────────────

    private static ModuloRam Ddr4(byte[] d, int indice)
    {
        // densidade por die: 256 Mb << código (JESD21-C, Annex L) -> MB
        long densidadeMb = (256L << (d[4] & 0x0F)) / 8;
        int larguraBarramento = 8 << (d[13] & 0x07);
        int larguraDispositivo = 4 << (d[12] & 0x07);
        int ranks = ((d[12] >> 3) & 0x07) + 1;
        bool tresDs = (d[6] & 0x03) == 0x02;                    // signal loading = 3DS
        int diesPorPackage = 1 << ((d[6] >> 4) & 0x07);

        long mb = 0;
        if (densidadeMb > 0 && larguraDispositivo > 0)
        {
            mb = densidadeMb * Math.Max(1, larguraBarramento / larguraDispositivo) * ranks;
            if (tresDs) mb *= diesPorPackage;
        }

        int? jedec = VelocidadeDdr4(d[0x12], d[0x7D]);
        int? xmp = d.Length > 0x1B0 && d[0x180] == 0x0C && d[0x181] == 0x4A
            ? VelocidadeDdr4(d[0x18C], d[0x1AF])
            : null;

        // dados de fabricante só existem a partir de 0x140 (dumps de 256 bytes não os têm)
        var part = d.Length >= 0x15D ? Ascii(d, 0x149, 20) : "";
        var fabricante = d.Length > 0x141 ? Fabricante(d[0x140], d[0x141]) : "—";

        return new ModuloRam(
            Posicao: $"Módulo {indice}",
            PartNumber: part,
            Fabricante: fabricante,
            CapacidadeBytes: mb * 1024L * 1024L,
            VelocidadeMts: xmp ?? jedec,
            VelocidadeJedecMts: jedec,
            Tipo: "DDR4",
            Formato: FormatoModulo(d[3]),
            Ecc: ((d[13] >> 3) & 1) == 1,
            Fabrico: d.Length > 0x144 ? Fabrico(d[0x143], d[0x144]) : "",
            Fonte: "SPD");
    }

    /// <summary>DDR4: tCK = MTB(0,125 ns)·byte + FTB(0,001 ns)·byte(com sinal).</summary>
    private static int? VelocidadeDdr4(byte mtb, byte ftb)
    {
        if (mtb == 0) return null;
        double ns = mtb * 0.125 + (sbyte)ftb * 0.001;
        return ns > 0.1 ? Arredondar(2000.0 / ns) : null;
    }

    // ───────────────────────────── SMBIOS ─────────────────────────────

    private static List<ModuloRam> LerSmbios()
    {
        var modulos = new List<ModuloRam>();
        const string raiz = "/sys/firmware/dmi/entries";
        if (!Directory.Exists(raiz)) return modulos;

        foreach (var dir in Directory.GetDirectories(raiz).OrderBy(d => d, StringComparer.Ordinal))
        {
            if (!Path.GetFileName(dir).StartsWith("17-", StringComparison.Ordinal)) continue;
            try
            {
                var m = Smbios17(File.ReadAllBytes(Path.Combine(dir, "raw")), modulos.Count + 1);
                if (m is not null) modulos.Add(m);
            }
            catch { /* normalmente 0400 root — não é erro, só não há dados */ }
        }
        return modulos;
    }

    /// <summary>Estrutura SMBIOS tipo 17 (Memory Device), incluindo tabela de strings.</summary>
    private static ModuloRam? Smbios17(byte[] b, int indice)
    {
        if (b.Length < 0x15 || b[0] != 17) return null;
        int comprimento = b[1];
        if (comprimento < 0x15 || comprimento > b.Length) return null;

        var textos = Strings(b, comprimento);
        string Texto(int deslocamento)
        {
            if (deslocamento >= comprimento || deslocamento >= b.Length) return "";
            int i = b[deslocamento];
            return i > 0 && i <= textos.Count ? textos[i - 1] : "";
        }

        long mb = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0x0C));
        if (mb == 0 || mb == 0xFFFF) return null;                     // slot vazio ou desconhecido
        if (mb == 0x7FFF && b.Length >= 0x20)
        {
            long extendido = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(0x1C)) & 0x7FFFFFFF;
            mb = extendido > 0 ? extendido : 0;                       // MB (bit 31 reservado)
            if (mb == 0) return null;
        }

        int tipoCodigo = b[0x12];
        var tipo = TiposSmbios.TryGetValue(tipoCodigo, out var t) ? t : "RAM";

        int velocidade = b.Length >= 0x17 ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0x15)) : 0;
        int configurada = b.Length >= 0x22 ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0x20)) : 0;
        int efetiva = configurada > 0 ? configurada : velocidade;

        int larguraTotal = b.Length >= 0x0A ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0x08)) : 0;
        int larguraDados = b.Length >= 0x0C ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0x0A)) : 0;

        var posicao = Texto(0x10);
        var fabricante = Texto(0x17);

        return new ModuloRam(
            Posicao: posicao.Length > 0 ? posicao : $"Módulo {indice}",
            PartNumber: Texto(0x1A),
            Fabricante: fabricante.Length > 0 ? fabricante : "—",
            CapacidadeBytes: mb * 1024L * 1024L,
            VelocidadeMts: efetiva > 0 ? efetiva : null,
            VelocidadeJedecMts: null,
            Tipo: tipo,
            Formato: FormatoSmbios(b.Length > 0x0F ? b[0x0E] : (byte)0),
            Ecc: larguraTotal > larguraDados && larguraDados > 0,
            Fabrico: "",
            Fonte: "SMBIOS");
    }

    /// <summary>Tabela de strings do SMBIOS: NUL-separadas, terminadas por NUL duplo.</summary>
    private static List<string> Strings(byte[] b, int inicio)
    {
        var lista = new List<string>();
        if (inicio >= b.Length) return lista;
        int i = inicio;
        while (i < b.Length && b[i] != 0)
        {
            int fim = i;
            while (fim < b.Length && b[fim] != 0) fim++;
            lista.Add(Encoding.ASCII.GetString(b, i, fim - i));
            i = fim + 1;
        }
        return lista;
    }

    // ───────────────────────────── comuns ─────────────────────────────

    private static int? Arredondar(double mts)
    {
        var valor = (int)(Math.Round(mts / 100.0) * 100);
        return valor is > 0 and < 20000 ? valor : null;
    }

    // JEP106: o primeiro byte traz o banco (bits 4:0) e o segundo o código (7 bits com paridade).
    // Só entram aqui pares confirmados; o resto mostra o código em vez de arriscar um nome errado.
    private static string Fabricante(byte primeiro, byte segundo) => (primeiro & 0x1F, segundo & 0x7F) switch
    {
        (0x00, 0x4E) => "Samsung",
        (0x00, 0x2D) => "SK hynix",
        (0x00, 0x2C) => "Micron",
        (0x02, 0x1E) => "Corsair",      // DDR5, confirmado pelo part number CMK… desta máquina
        (0x01, 0x1A) => "Corsair",      // JEP106 clássico da Corsair
        (0x00, 0x1D) => "Exel",
        _ => $"ID {primeiro:X2}{segundo:X2}"
    };

    private static string FormatoModulo(byte codigo)
        => TiposDdr.TryGetValue(codigo & 0x0F, out var t) ? t : "DIMM";

    /// <summary>SMBIOS tipo 17, byte 0x0E (form factor).</summary>
    private static string FormatoSmbios(byte codigo) => codigo switch
    {
        0x02 => "SIMM", 0x03 => "SIMM", 0x04 => "SIMM", 0x05 => "SIMM",
        0x06 => "SIMM", 0x07 => "SIMM", 0x08 => "DIMM", 0x09 => "DIMM",
        0x0A => "DIMM", 0x0B => "DIMM", 0x0C => "SODIMM", 0x0D => "SODIMM",
        0x0E => "SODIMM", 0x0F => "SODIMM", 0x10 => "SODIMM", 0x11 => "SODIMM",
        0x12 => "RDIMM", 0x13 => "RDIMM", 0x14 => "RDIMM", 0x15 => "LRDIMM",
        _ => "DIMM"
    };

    /// <summary>Data de fabrico em BCD (ano desde 2000, semana).</summary>
    private static string Fabrico(byte ano, byte semana)
    {
        int a = 2000 + ((ano >> 4) * 10 + (ano & 0x0F));
        int s = (semana >> 4) * 10 + (semana & 0x0F);
        return s is >= 1 and <= 53 && a >= 2000 && a <= 2100 ? $"fabrico {a}-W{s:00}" : "";
    }

    private static string Ascii(byte[] d, int inicio, int comprimento)
    {
        if (inicio + comprimento > d.Length) return "";
        var sb = new StringBuilder(comprimento);
        for (int i = inicio; i < inicio + comprimento; i++)
        {
            byte c = d[i];
            if (c is 0 or 0x20) continue;
            if (c is >= 0x20 and < 0x7F) sb.Append((char)c);
        }
        return sb.ToString().Trim();
    }
}

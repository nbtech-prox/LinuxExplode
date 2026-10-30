using System.Globalization;

namespace LinuxExplode.Core;

/// <summary>Um aviso activo. <c>Chave</c> identifica a regra (para o cooldown das notificações).</summary>
public sealed record Alerta(string Chave, string Titulo, string Detalhe, bool Grave);

/// <summary>
/// Regras de aviso sobre os sensores que já existem. Nada é inventado: cada alerta sai de uma leitura
/// real (temperatura, ventoinha, espaço em disco, veredito SMART) comparada com um limite configurável.
/// </summary>
public static class Alertas
{
    public static List<Alerta> Avaliar(List<Sensor>? sensores = null, List<Particao>? particoes = null)
    {
        var alertas = new List<Alerta>();
        var cfg = Config.Atual;

        double? cpu = null, gpu = null;
        try
        {
            sensores ??= Sensores.Ler();
            cpu = Sensores.CpuTemperatura(sensores);
            gpu = Sensores.GpuTemperatura(sensores);
        }
        catch { }

        if (cpu is double c && c >= cfg.LimiteCpuAviso)
            alertas.Add(new Alerta("cpu-temp", Textos.T("CPU quente"),
                Textos.TF("{0} °C (limite {1} °C)", $"{c:0}", $"{cfg.LimiteCpuAviso:0}"), true));

        if (gpu is double g && g >= cfg.LimiteGpuAviso)
            alertas.Add(new Alerta("gpu-temp", Textos.T("GPU quente"),
                Textos.TF("{0} °C (limite {1} °C)", $"{g:0}", $"{cfg.LimiteGpuAviso:0}"), true));

        // ventoinha a zero com a CPU quente: ou está desligada, ou o sensor não a vê
        if (sensores is not null && cpu is double c2 && c2 >= 65)
        {
            var ventoinhas = sensores.Where(s => s.Tipo == TipoSensor.Ventoinha).ToList();
            if (ventoinhas.Count > 0 && ventoinhas.All(v => v.Valor <= 0))
                alertas.Add(new Alerta("ventoinha", Textos.TF("Ventoinhas a zero com a CPU a {0} °C", $"{c2:0}"),
                    Textos.TF("{0} ventoinha(s) lidas a 0 RPM — verifica cabo/curva ou o sensor", ventoinhas.Count), true));
        }

        // espaço em disco (mesma fonte da aba Discos: /proc/mounts + statvfs)
        try
        {
            // Montagens() está indexado por dispositivo e Espaco() por ponto de montagem: é preciso
            // cruzar os dois, senão nenhuma partição real é encontrada.
            var fsPorMontagem = Util.Montagens().Values
                .GroupBy(m => m.Montagem)
                .ToDictionary(g => g.Key, g => g.First().Fs, StringComparer.Ordinal);

            // Só discos a sério: tmpfs, efivarfs e /run não enchem por uso, e /boot é pequeno de
            // propósito. Avisar sobre eles era ruído.
            var tipos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ext4", "ext3", "ext2", "xfs", "btrfs", "f2fs", "zfs", "ntfs", "ntfs3", "exfat"
            };

            foreach (var (nome, espaco) in Util.Espaco())
            {
                if (espaco.Total < 8L * 1024 * 1024 * 1024) continue;      // ignora /boot e afins
                if (!fsPorMontagem.TryGetValue(nome, out var fs) || !tipos.Contains(fs)) continue;

                double livreGb = espaco.Livre / 1073741824.0;

                if (livreGb < cfg.EspacoMinimoGb)
                    alertas.Add(new Alerta("disco-" + nome, Textos.TF("Pouco espaço em {0}", nome),
                        Textos.TF("{0} GB livres (mínimo {1} GB) · {2}", $"{livreGb:0.0}", $"{cfg.EspacoMinimoGb:0}", fs), false));
            }
        }
        catch { }

        return alertas;
    }
}

/// <summary>
/// Notificações do ambiente (libnotify). Se <c>notify-send</c> não existir — típico dentro do Flatpak —
/// a aplicação di-lo e continua a mostrar o alerta na interface.
/// </summary>
public static class Notificacoes
{
    private static readonly Dictionary<string, DateTime> Ultimas = new();

    public static bool Disponivel => Util.ExisteComando("notify-send");

    /// <summary>Envia no máximo uma notificação por chave a cada 15 minutos.</summary>
    public static void Avisar(string chave, string titulo, string corpo)
    {
        if (!Config.Atual.Notificacoes || !Disponivel) return;

        if (Ultimas.TryGetValue(chave, out var quando) && (DateTime.Now - quando).TotalMinutes < 15)
            return;

        Ultimas[chave] = DateTime.Now;
        Util.Comando("notify-send", "-a", "LINUX EXPLODE", "-i", "dialog-warning", titulo, corpo);
    }

    /// <summary>Uma notificação por cada alerta que ainda não foi anunciado nesta sessão.</summary>
    public static void AvisarAlertas(IEnumerable<Alerta> alertas)
    {
        foreach (var a in alertas)
            Avisar(a.Chave, a.Titulo, a.Detalhe);
    }
}

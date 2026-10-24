using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LinuxExplode.Core;

public sealed record RedeAlvo(string Nome, string Endereco, double Media, double Min, double Max, double Jitter, double Perda);

public sealed record RedeResultado(double DnsMs, double DownloadMBs, double UploadMBs, List<RedeAlvo> Alvos, string Veredito, string Detalhe);

/// <summary>Diagnóstico de rede: latência TCP, jitter, perda, DNS e velocidade.</summary>
public static class Rede
{
    public static async Task<RedeResultado> Correr(Action<string>? progresso = null, CancellationToken ct = default)
    {
        var alvos = new List<RedeAlvo>();

        progresso?.Invoke("a medir latência até ao router…");
        var gw = Gateway();
        if (gw is not null)
            alvos.Add(await MedirTcp("Router", gw, null, 12, ct));

        progresso?.Invoke("a medir latência até à internet…");
        alvos.Add(await MedirTcp("Internet (1.1.1.1)", "1.1.1.1", 443, 12, ct));
        alvos.Add(await MedirTcp("Internet (8.8.8.8)", "8.8.8.8", 443, 12, ct));

        progresso?.Invoke("a medir DNS…");
        double dns = 0;
        try
        {
            var sw = Stopwatch.StartNew();
            await Dns.GetHostAddressesAsync("github.com", ct);
            sw.Stop();
            dns = sw.Elapsed.TotalMilliseconds;
        }
        catch { }

        double down = 0, up = 0;
        try
        {
            progresso?.Invoke("a medir download…");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            const int bytesDown = 25_000_000;
            var sw = Stopwatch.StartNew();
            using (var resp = await http.GetAsync($"https://speed.cloudflare.com/__down?bytes={bytesDown}", ct))
            {
                await using var s = await resp.Content.ReadAsStreamAsync(ct);
                var buf = new byte[81920];
                long total = 0;
                int r;
                while ((r = await s.ReadAsync(buf, ct)) > 0) total += r;
                sw.Stop();
                down = total / 1048576.0 / Math.Max(0.001, sw.Elapsed.TotalSeconds);
            }

            progresso?.Invoke("a medir upload…");
            var dados = new byte[10_000_000];
            Random.Shared.NextBytes(dados);
            sw.Restart();
            using (var body = new ByteArrayContent(dados))
            {
                using var resp = await http.PostAsync("https://speed.cloudflare.com/__up", body, ct);
                await resp.Content.ReadAsByteArrayAsync(ct);
            }
            sw.Stop();
            up = dados.Length / 1048576.0 / Math.Max(0.001, sw.Elapsed.TotalSeconds);
        }
        catch { }

        // Veredito
        var internet = alvos.FirstOrDefault(a => a.Nome.Contains("1.1.1.1"));
        var router = alvos.FirstOrDefault(a => a.Nome == "Router");
        string veredito, detalhe;

        if (internet is null || internet.Perda >= 100)
        { veredito = "Sem internet"; detalhe = "não foi possível ligar a nenhum destino externo"; }
        else if (internet.Perda > 5)
        { veredito = "Perda de pacotes"; detalhe = $"{internet.Perda:0}% de falhas até à internet"; }
        else if (router is not null && router.Media > 5)
        { veredito = "Rede local"; detalhe = $"o router já responde em {router.Media:0} ms — o atraso começa antes da operadora"; }
        else if (internet.Jitter > 30)
        { veredito = "Instável"; detalhe = $"jitter de {internet.Jitter:0} ms até à internet"; }
        else if (internet.Media > 60)
        { veredito = "Latência alta"; detalhe = $"{internet.Media:0} ms de média até à internet"; }
        else
        { veredito = "Estável"; detalhe = $"{internet.Media:0} ms de média, jitter {internet.Jitter:0} ms"; }

        return new RedeResultado(dns, down, up, alvos, veredito, detalhe);
    }

    private static async Task<RedeAlvo> MedirTcp(string nome, string host, int? porta, int amostras, CancellationToken ct)
    {
        var tempos = new List<double>();
        int falhas = 0;
        for (int i = 0; i < amostras; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var cliente = new TcpClient();
                var sw = Stopwatch.StartNew();
                var tarefa = cliente.ConnectAsync(host, porta ?? 80, ct).AsTask();
                var concluida = await Task.WhenAny(tarefa, Task.Delay(1500, ct));
                sw.Stop();
                if (concluida != tarefa || !cliente.Connected) { falhas++; continue; }
                await tarefa;
                tempos.Add(sw.Elapsed.TotalMilliseconds);
            }
            catch { falhas++; }
            await Task.Delay(30, ct);
        }

        if (tempos.Count == 0)
            return new RedeAlvo(nome, host, 0, 0, 0, 0, 100);

        double media = tempos.Average();
        double jitter = Math.Sqrt(tempos.Select(t => (t - media) * (t - media)).Average());
        return new RedeAlvo(nome, host, media, tempos.Min(), tempos.Max(), jitter, falhas * 100.0 / amostras);
    }

    /// <summary>Gateway predefinido a partir do /proc/net/route (sem depender de iproute2).</summary>
    public static string? Gateway()
    {
        try
        {
            foreach (var linha in File.ReadLines("/proc/net/route").Skip(1))
            {
                var p = linha.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 3) p = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 3) continue;
                if (p[1] != "00000000") continue;
                var hex = p[2];
                if (hex.Length != 8) continue;
                var octetos = new[] { hex[6..8], hex[4..6], hex[2..4], hex[0..2] }
                    .Select(x => Convert.ToInt32(x, 16));
                return string.Join('.', octetos);
            }
        }
        catch { }
        return null;
    }

    public static string Interfaces()
    {
        try
        {
            var virtuais = new[] { "docker", "veth", "br-", "virbr", "tailscale", "tun", "tap", "wg", "podman", "cni", "flannel" };
            return string.Join(" · ", NetworkInterface.GetAllNetworkInterfaces()
                .Where(i => i.OperationalStatus == OperationalStatus.Up &&
                            i.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                                or NetworkInterfaceType.Wireless80211 &&
                            !virtuais.Any(v => i.Name.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
                .Select(i => $"{i.Name} ({i.NetworkInterfaceType}, {i.Speed / 1_000_000} Mb/s)"));
        }
        catch { return "—"; }
    }
}

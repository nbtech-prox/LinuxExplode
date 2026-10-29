using System.Diagnostics;
using System.Globalization;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace LinuxExplode.Core;

/// <summary>Informação de uma GPU para o teste de VRAM: nome e heap que o driver anuncia.</summary>
public sealed record InfoVram(int Indice, string Nome, string Tipo, long HeapBytes,
    long? SysfsTotal, long? SysfsUsado);

/// <summary>
/// Resultado do enchimento de VRAM. Tudo o que aqui está foi medido: o total saiu das alocações que o
/// driver aceitou, o pico e o uso vêm de <c>/sys/class/drm/…/mem_info_vram_used</c> (fonte independente
/// da aplicação) e a verificação é a leitura de volta do que se escreveu.
/// </summary>
public sealed record ResultadoVram(
    string Gpu,
    long TotalAlocado,
    int Blocos,
    long BlocoBytes,
    long? HeapTotal,
    string? Erro,
    long? SysfsTotal,
    long? SysfsAntes,
    long? SysfsPico,
    long? SysfsDepois,
    int BlocosVerificados,
    long BytesVerificados,
    int ErrosVerificacao,
    double Segundos,
    bool Parado,
    List<string> Linhas);

/// <summary>
/// Enche a memória da GPU até o driver recusar, escrevendo um padrão em cada bloco e relendo-o. É o
/// único subsistema que o stress normal não toca; serve para apanhar VRAM defeituosa e para mostrar
/// quanto é que a placa realmente consegue alocar.
/// </summary>
public static unsafe class StressVram
{
    public const long BlocoPadrao = 128L * 1024 * 1024;
    private const int Amostra = 4096;
    private const uint Padrao = 0xA5A5A5A5;

    /// <summary>GPUs com memória alocável, com o tamanho do maior heap do driver e o uso em /sys.</summary>
    public static List<InfoVram> Dispositivos()
    {
        var lista = new List<InfoVram>();
        Vk vk;
        Instance inst;
        try { (vk, inst) = BenchGpu.CriarInstancia(); }
        catch { return lista; }

        try
        {
            var pds = BenchGpu.DispositivosFisicos(vk, inst);
            foreach (var info in BenchGpu.Dispositivos())
            {
                if (info.Indice < 0 || info.Indice >= pds.Length) continue;
                var pd = pds[info.Indice];

                vk.GetPhysicalDeviceProperties(pd, out var props);
                vk.GetPhysicalDeviceMemoryProperties(pd, out var mp);

                long heap = 0;
                for (uint i = 0; i < mp.MemoryTypeCount; i++)
                {
                    if ((mp.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) == 0) continue;
                    long tamanho = (long)mp.MemoryHeaps[(int)mp.MemoryTypes[(int)i].HeapIndex].Size;
                    if (tamanho > heap) heap = tamanho;
                }

                var sys = SysfsVram(props.VendorID, props.DeviceID);
                lista.Add(new InfoVram(info.Indice, info.Nome, info.Tipo, heap, sys?.Total, sys?.Usado));
            }
        }
        catch { }
        finally { vk.DestroyInstance(inst, null); vk.Dispose(); }

        return lista;
    }

    /// <summary>
    /// Aloca blocos de <paramref name="blocoBytes"/> até o driver recusar (ou até <paramref name="limiteBytes"/>,
    /// quando maior que zero), preenchendo e verificando cada um. Devolve um resultado mesmo quando falha:
    /// "o driver recusou mais 128 MB (ErrorOutOfDeviceMemory)" é a resposta, não um erro da aplicação.
    /// </summary>
    public static ResultadoVram Correr(int indice = 0, long limiteBytes = 0, long blocoBytes = BlocoPadrao,
        Action<string>? progresso = null, CancellationToken ct = default)
    {
        blocoBytes = Math.Max(16 * 1024 * 1024, blocoBytes & ~4095L);
        var linhas = new List<string>();
        var relogio = Stopwatch.StartNew();

        long alocado = 0, blocoAtual = blocoBytes, bytesVerificados = 0;
        int blocos = 0, verificados = 0, errosVerificacao = 0;
        string? erro = null;
        bool parado = false;
        string nome = "GPU";
        long? heapTotal = null, sysTotal = null, sysAntes = null, sysPico = null, sysDepois = null;

        Vk vk;
        Instance inst;
        try { (vk, inst) = BenchGpu.CriarInstancia(); }
        catch (Exception ex)
        {
            return new ResultadoVram("GPU", 0, 0, blocoBytes, null, "Vulkan indisponível: " + ex.Message,
                null, null, null, null, 0, 0, 0, 0, false, new List<string> { "sem Vulkan nesta máquina" });
        }

        Device dev = default;
        CommandPool pool = default;
        Queue fila = default;
        Fence cerca = default;
        VkBuffer staging = default;
        DeviceMemory memStaging = default;
        uint tipoVram = uint.MaxValue;
        uint vendorId = 0, deviceId = 0;
        var memorias = new List<(DeviceMemory Mem, VkBuffer Buf)>();

        try
        {
            var pds = BenchGpu.DispositivosFisicos(vk, inst);
            if (pds.Length == 0)
                throw new InvalidOperationException("nenhum dispositivo Vulkan");

            var pd = indice >= 0 && indice < pds.Length ? pds[indice] : pds[0];
            nome = BenchGpu.NomeDoDispositivo(vk, pd);
            vk.GetPhysicalDeviceProperties(pd, out var props);
            vendorId = props.VendorID;
            deviceId = props.DeviceID;

            // heap da VRAM: o maior tipo DeviceLocal (numa APU é a memória do sistema partilhada)
            vk.GetPhysicalDeviceMemoryProperties(pd, out var mp);
            for (uint i = 0; i < mp.MemoryTypeCount; i++)
            {
                if ((mp.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) == 0) continue;
                long tamanho = (long)mp.MemoryHeaps[(int)mp.MemoryTypes[(int)i].HeapIndex].Size;
                if (heapTotal is null || tamanho > heapTotal) { heapTotal = tamanho; tipoVram = i; }
            }
            if (tipoVram == uint.MaxValue)
                throw new InvalidOperationException("a GPU não expõe memória DeviceLocal");

            var sys = SysfsVram(props.VendorID, props.DeviceID);
            sysTotal = sys?.Total;
            sysAntes = sys?.Usado;
            sysPico = sysAntes;

            int familia = BenchGpu.FamiliaCompute(vk, pd);
            if (familia < 0) throw new InvalidOperationException("sem fila de computação/transferência");

            float prioridade = 1f;
            var filaInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = (uint)familia,
                QueueCount = 1,
                PQueuePriorities = &prioridade
            };
            var devInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &filaInfo
            };
            var r = vk.CreateDevice(pd, &devInfo, null, out dev);
            if (r != Result.Success) throw new InvalidOperationException("vkCreateDevice: " + r);
            vk.GetDeviceQueue(dev, (uint)familia, 0, out fila);

            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = (uint)familia,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };
            if (vk.CreateCommandPool(dev, &poolInfo, null, out pool) != Result.Success)
                throw new InvalidOperationException("vkCreateCommandPool falhou");

            var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
            if (vk.CreateFence(dev, &fenceInfo, null, out cerca) != Result.Success)
                throw new InvalidOperationException("vkCreateFence falhou");

            // staging de 2 amostras: início e fim de cada bloco
            var stagingInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = Amostra * 2,
                Usage = BufferUsageFlags.TransferDstBit,
                SharingMode = SharingMode.Exclusive
            };
            if (vk.CreateBuffer(dev, &stagingInfo, null, out staging) != Result.Success)
                throw new InvalidOperationException("vkCreateBuffer (staging) falhou");
            vk.GetBufferMemoryRequirements(dev, staging, out var reqStaging);
            var alocStaging = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = reqStaging.Size,
                MemoryTypeIndex = BenchGpu.TipoMemoria(vk, pd, reqStaging.MemoryTypeBits,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
            };
            if (vk.AllocateMemory(dev, &alocStaging, null, out memStaging) != Result.Success)
                throw new InvalidOperationException("vkAllocateMemory (staging) falhou");
            vk.BindBufferMemory(dev, staging, memStaging, 0);

            linhas.Add($"GPU: {nome} · heap {Util.Bytes(heapTotal ?? 0)}" +
                       (sysAntes is long sa ? $" · {Util.Bytes(sa)} já em uso" : ""));

            while (true)
            {
                if (ct.IsCancellationRequested) { parado = true; erro = "parado pelo utilizador"; break; }

                var (termicaOk, motivo, _, _) = GuardaTermica.Avaliar();
                if (!termicaOk) { erro = motivo; break; }

                if (limiteBytes > 0)
                {
                    long resta = limiteBytes - alocado;
                    if (resta < 16 * 1024 * 1024) break;
                    if (resta < blocoAtual) blocoAtual = resta & ~4095L;
                }

                // buffer primeiro: o pedido de memória real vem das requirements dele
                var bufInfo = new BufferCreateInfo
                {
                    SType = StructureType.BufferCreateInfo,
                    Size = (ulong)blocoAtual,
                    Usage = BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
                    SharingMode = SharingMode.Exclusive
                };
                if (vk.CreateBuffer(dev, &bufInfo, null, out var buf) != Result.Success)
                {
                    erro = $"o driver recusou criar um buffer de {Util.Bytes(blocoAtual)}";
                    break;
                }

                vk.GetBufferMemoryRequirements(dev, buf, out var req);
                var aloc = new MemoryAllocateInfo
                {
                    SType = StructureType.MemoryAllocateInfo,
                    AllocationSize = req.Size,
                    MemoryTypeIndex = tipoVram
                };

                var resultado = vk.AllocateMemory(dev, &aloc, null, out var mem);
                if (resultado != Result.Success)
                {
                    vk.DestroyBuffer(dev, buf, null);
                    erro = $"o driver recusou mais {Util.Bytes((long)req.Size)}: {resultado}";
                    break;
                }

                if (vk.BindBufferMemory(dev, buf, mem, 0) != Result.Success)
                {
                    vk.FreeMemory(dev, mem, null);
                    vk.DestroyBuffer(dev, buf, null);
                    erro = "vkBindBufferMemory falhou";
                    break;
                }

                memorias.Add((mem, buf));
                alocado += (long)req.Size;
                blocos++;

                var (verificou, porque) = PreencherEVerificar(vk, dev, pool, fila, cerca, buf, (ulong)blocoAtual,
                    staging, memStaging);
                if (verificou) { verificados++; bytesVerificados += Amostra * 2; }
                else { errosVerificacao++; linhas.Add("  ! " + (porque ?? "verificação falhou")); }

                if (SysfsVram(props.VendorID, props.DeviceID)?.Usado is long usado)
                    sysPico = Math.Max(sysPico ?? usado, usado);

                progresso?.Invoke($"{Util.Bytes(alocado)} alocados · {verificados} bloco(s) verificado(s)");
                blocoAtual = blocoBytes;
            }

        }
        catch (Exception ex)
        {
            erro ??= ex.Message;
        }
        finally
        {
            // libertar tudo: a VRAM tem de voltar ao que era, senão a app deixa a GPU cheia
            foreach (var (mem, buf) in memorias)
            {
                try { vk.DestroyBuffer(dev, buf, null); } catch { }
                try { vk.FreeMemory(dev, mem, null); } catch { }
            }
            if (memStaging.Handle != 0) { try { vk.DestroyBuffer(dev, staging, null); } catch { } try { vk.FreeMemory(dev, memStaging, null); } catch { } }
            if (cerca.Handle != 0) try { vk.DestroyFence(dev, cerca, null); } catch { }
            if (pool.Handle != 0) try { vk.DestroyCommandPool(dev, pool, null); } catch { }
            if (dev.Handle != 0) try { vk.DestroyDevice(dev, null); } catch { }
            try { vk.DestroyInstance(inst, null); vk.Dispose(); } catch { }
            sysDepois = SysfsVram(vendorId, deviceId)?.Usado;
            relogio.Stop();
        }

        var usadoDepoisFinal = sysDepois;

        if (erro is not null && !erro.Contains("parado pelo utilizador") && !erro.StartsWith("guarda térmica"))
            linhas.Add("fim: " + erro);
        linhas.Add($"alocados {blocos} bloco(s) · {Util.Bytes(alocado)} em {relogio.Elapsed.TotalSeconds:0.0} s");
        if (verificados > 0)
            linhas.Add($"verificação: {verificados} bloco(s) preenchidos por inteiro; relidas 2 amostras de 4 KB em cada " +
                       $"({Util.Bytes(bytesVerificados)}) — " +
                       (errosVerificacao == 0 ? "iguais ao padrão escrito" : $"{errosVerificacao} bloco(s) com diferenças"));
        if (sysAntes is long antes)
            linhas.Add($"sysfs: usado {Util.Bytes(antes)} antes · pico {Util.Bytes(sysPico ?? antes)} · " +
                       $"depois {Util.Bytes(usadoDepoisFinal is long ud ? ud : 0)} (ainda no processo — o driver " +
                       $"só devolve o heap quando o processo termina) · total {Util.Bytes(sysTotal ?? 0)}");
        if (erro is not null && blocos == 0) linhas.Add("nada foi alocado — " + erro);

        return new ResultadoVram(nome, alocado, blocos, blocoBytes, heapTotal, erro, sysTotal, sysAntes,
            sysPico, usadoDepoisFinal, verificados, bytesVerificados, errosVerificacao,
            relogio.Elapsed.TotalSeconds, parado, linhas);
    }

    /// <summary>
    /// Enche o buffer com um padrão (vkCmdFillBuffer) e relê o primeiro e o último bloco de 4 KB por
    /// uma staging buffer. A releitura é o que prova que a memória guarda o que se escreveu.
    /// </summary>
    private static (bool Ok, string? Motivo) PreencherEVerificar(Vk vk, Device dev, CommandPool pool, Queue fila,
        Fence cerca, VkBuffer destino, ulong tamanho, VkBuffer staging, DeviceMemory memStaging)
    {
        var alocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = pool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        if (vk.AllocateCommandBuffers(dev, &alocInfo, out var cmd) != Result.Success)
            return (false, "sem command buffer");

        var inicio = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        if (vk.BeginCommandBuffer(cmd, &inicio) != Result.Success) return (false, "vkBeginCommandBuffer falhou");

        vk.CmdFillBuffer(cmd, destino, 0, ulong.MaxValue, Padrao);

        var barreira = new BufferMemoryBarrier
        {
            SType = StructureType.BufferMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.TransferReadBit,
            SrcQueueFamilyIndex = uint.MaxValue,
            DstQueueFamilyIndex = uint.MaxValue,
            Buffer = destino,
            Offset = 0,
            Size = ulong.MaxValue
        };
        vk.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit,
            0, 0, null, 1, &barreira, 0, null);

        var primeira = new BufferCopy { SrcOffset = 0, DstOffset = 0, Size = Amostra };
        var ultima = new BufferCopy { SrcOffset = tamanho - Amostra, DstOffset = Amostra, Size = Amostra };
        vk.CmdCopyBuffer(cmd, destino, staging, 1, &primeira);
        vk.CmdCopyBuffer(cmd, destino, staging, 1, &ultima);

        if (vk.EndCommandBuffer(cmd) != Result.Success) return (false, "vkEndCommandBuffer falhou");

        var submeter = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd
        };
        vk.ResetFences(dev, 1, &cerca);
        if (vk.QueueSubmit(fila, 1, &submeter, cerca) != Result.Success) return (false, "vkQueueSubmit falhou");
        if (vk.WaitForFences(dev, 1, &cerca, true, 30_000_000_000) != Result.Success)
            return (false, "a GPU não terminou o preenchimento em 30 s");

        void* mapa;
        if (vk.MapMemory(dev, memStaging, 0, Amostra * 2, 0, &mapa) != Result.Success)
            return (false, "vkMapMemory falhou");

        var span = new Span<byte>(mapa, Amostra * 2);
        int diferentes = 0;
        for (int i = 0; i + 4 <= span.Length; i += 4)
        {
            if (BitConverter.ToUInt32(span[i..]) != Padrao) diferentes++;
        }
        vk.UnmapMemory(dev, memStaging);
        vk.FreeCommandBuffers(dev, pool, 1, &cmd);

        return diferentes == 0
            ? (true, null)
            : (false, $"{diferentes} palavra(s) de 4 B não voltaram como o padrão escrito");
    }

    /// <summary>
    /// Uso de VRAM do cartão cujo vendor/device batem com os que o Vulkan reporta. É a fonte
    /// independente para cruzar com o total que as nossas alocações somaram.
    /// </summary>
    private static (long Total, long Usado)? SysfsVram(uint vendorId, uint deviceId)
    {
        try
        {
            if (!Directory.Exists("/sys/class/drm")) return null;
            foreach (var dir in Directory.GetDirectories("/sys/class/drm"))
            {
                var nome = Path.GetFileName(dir);
                if (!nome.StartsWith("card", StringComparison.Ordinal) || nome.Length <= 4 ||
                    !nome[4..].All(char.IsDigit)) continue;

                var dev = Path.Combine(dir, "device");
                if (!uint.TryParse(Util.Ler(dev + "/vendor").Replace("0x", ""), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out uint v)) continue;
                if (!uint.TryParse(Util.Ler(dev + "/device").Replace("0x", ""), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out uint d)) continue;
                if (v != vendorId || d != deviceId) continue;

                long total = Util.LerLong(dev + "/mem_info_vram_total");
                long usado = Util.LerLong(dev + "/mem_info_vram_used");
                if (total > 0) return (total, usado);
            }
        }
        catch { }
        return null;
    }
}

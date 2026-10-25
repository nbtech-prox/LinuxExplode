using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace LinuxExplode.Core;

public sealed record InfoGpu(int Indice, string Nome, string Tipo);
public sealed record ResultadoGpu(string Nome, double Segundos, long Pontos, int Largura, int Altura,
    int Amostras, int Passagens, byte[] Rgba);

/// <summary>
/// Benchmark de GPU: path tracer em compute shader Vulkan (SPIR-V embutido).
/// Mesma cena e mesma matemática do benchmark de CPU, mas executada nos shaders.
/// </summary>
public static unsafe class BenchGpu
{
    /// <summary>Ângulo da câmara na prévia animada: uma volta completa a cada ~18 s.</summary>
    internal static float AnguloDaVez(Stopwatch relogio) => (float)(relogio.Elapsed.TotalSeconds * 0.35);

    public const int LarguraPadrao = 1280;
    public const int AlturaPadrao = 720;
    public const int AmostrasPadrao = 64;
    /// <summary>Tempo mínimo de medição: repete o dispatch até chegar aqui (nota estável).</summary>
    public const double AlvoSegundos = 1.5;
    private const uint Grupo = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct Parametros
    {
        public uint Largura;
        public uint Altura;
        public uint Amostras;
        public uint Semente;
        public float Angulo;   // 0 = câmara do benchmark; != 0 orbita a cena (prévia animada)
    }

    private static byte[] Spirv()
    {
        var asm = Assembly.GetExecutingAssembly();
        var nome = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("path.comp.spv", StringComparison.Ordinal))
                   ?? throw new InvalidOperationException("shader path.comp.spv não embutido");
        using var s = asm.GetManifestResourceStream(nome)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static (Vk Vk, Instance Inst) CriarInstancia()
    {
        var vk = Vk.GetApi();
        var pNome = Marshal.StringToHGlobalAnsi("LINUX EXPLODE");
        var app = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)pNome,
            ApplicationVersion = 1,
            PEngineName = (byte*)pNome,
            EngineVersion = 1,
            ApiVersion = Vk.Version11
        };
        var ci = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &app };
        var r = vk.CreateInstance(&ci, null, out var inst);
        Marshal.FreeHGlobal(pNome);
        if (r != Result.Success) throw new InvalidOperationException($"vkCreateInstance: {r}");
        return (vk, inst);
    }

    private static PhysicalDevice[] DispositivosFisicos(Vk vk, Instance inst)
    {
        uint n = 0;
        vk.EnumeratePhysicalDevices(inst, ref n, null);
        if (n == 0) return Array.Empty<PhysicalDevice>();
        var arr = new PhysicalDevice[n];
        fixed (PhysicalDevice* p = arr) vk.EnumeratePhysicalDevices(inst, ref n, p);
        return arr;
    }

    private static string NomeDoDispositivo(Vk vk, PhysicalDevice pd)
    {
        PhysicalDeviceProperties props;
        vk.GetPhysicalDeviceProperties(pd, &props);
        return Marshal.PtrToStringAnsi((IntPtr)props.DeviceName) ?? "GPU";
    }

    /// <summary>Lista as GPUs que expõem uma fila de computação.</summary>
    public static List<InfoGpu> Dispositivos()
    {
        var lista = new List<InfoGpu>();
        try
        {
            var (vk, inst) = CriarInstancia();
            try
            {
                var pds = DispositivosFisicos(vk, inst);
                var vistos = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < pds.Length; i++)
                {
                    if (FamiliaCompute(vk, pds[i]) < 0) continue;
                    PhysicalDeviceProperties props;
                    vk.GetPhysicalDeviceProperties(pds[i], &props);
                    // llvmpipe/software aparece duplicado dentro do Flatpak e não mede GPU nenhuma
                    if (props.DeviceType == PhysicalDeviceType.Cpu) continue;
                    var nome = NomeDoDispositivo(vk, pds[i]);
                    if (!vistos.Add(nome)) continue;
                    var tipo = props.DeviceType switch
                    {
                        PhysicalDeviceType.DiscreteGpu => "dedicada",
                        PhysicalDeviceType.IntegratedGpu => "integrada",
                        PhysicalDeviceType.VirtualGpu => "virtual",
                        _ => "outra"
                    };
                    lista.Add(new InfoGpu(i, nome, tipo));
                }
            }
            finally { vk.DestroyInstance(inst, null); vk.Dispose(); }
        }
        catch { }
        // dedicada primeiro: é a que interessa medir por omissão
        return lista.OrderBy(g => g.Tipo == "dedicada" ? 0 : g.Tipo == "integrada" ? 1 : 2).ToList();
    }

    private static int FamiliaCompute(Vk vk, PhysicalDevice pd)
    {
        uint n = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(pd, ref n, null);
        if (n == 0) return -1;
        var props = new QueueFamilyProperties[n];
        fixed (QueueFamilyProperties* p = props) vk.GetPhysicalDeviceQueueFamilyProperties(pd, ref n, p);
        for (int i = 0; i < props.Length; i++)
            if ((props[i].QueueFlags & QueueFlags.ComputeBit) != 0) return i;
        return -1;
    }

    private static uint TipoMemoria(Vk vk, PhysicalDevice pd, uint mascara, MemoryPropertyFlags desejado)
    {
        vk.GetPhysicalDeviceMemoryProperties(pd, out var mp);
        for (uint i = 0; i < mp.MemoryTypeCount; i++)
        {
            if ((mascara & (1u << (int)i)) == 0) continue;
            if ((mp.MemoryTypes[(int)i].PropertyFlags & desejado) == desejado) return i;
        }
        throw new InvalidOperationException("sem tipo de memória adequado");
    }

    /// <summary>Corre o path tracer na GPU indicada. Devolve null se o Vulkan não estiver disponível.</summary>
    public static ResultadoGpu? Correr(int indice, int largura = LarguraPadrao, int altura = AlturaPadrao,
        int amostras = AmostrasPadrao, Action<byte[], int>? fotograma = null, CancellationToken ct = default)
    {
        if (largura <= 0 || altura <= 0 || amostras <= 0) throw new ArgumentOutOfRangeException(nameof(largura));

        Vk vk;
        Instance inst;
        try { (vk, inst) = CriarInstancia(); }
        catch { return null; }

        Device dispositivo = default;
        VkBuffer buffer = default;
        DeviceMemory memoria = default;
        ShaderModule modulo = default;
        DescriptorSetLayout layoutConjunto = default;
        DescriptorPool pool = default;
        DescriptorSet conjunto = default;
        PipelineLayout layoutPipeline = default;
        Pipeline pipeline = default;
        CommandPool poolComandos = default;
        Fence cerca = default;
        var nome = "GPU";

        try
        {
            var pds = DispositivosFisicos(vk, inst);
            if (pds.Length == 0) return null;
            var pd = indice >= 0 && indice < pds.Length ? pds[indice] : pds[0];
            nome = NomeDoDispositivo(vk, pd);

            int familia = FamiliaCompute(vk, pd);
            if (familia < 0) return null;

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
            if (vk.CreateDevice(pd, &devInfo, null, out dispositivo) != Result.Success) return null;
            vk.GetDeviceQueue(dispositivo, (uint)familia, 0, out var fila);

            // ---- buffer de saída (host visível, coerente) ----
            ulong tamanho = (ulong)(largura * altura * 4);
            var bufInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = tamanho,
                Usage = BufferUsageFlags.StorageBufferBit,
                SharingMode = SharingMode.Exclusive
            };
            if (vk.CreateBuffer(dispositivo, &bufInfo, null, out buffer) != Result.Success) return null;

            vk.GetBufferMemoryRequirements(dispositivo, buffer, out var req);
            var aloc = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = req.Size,
                MemoryTypeIndex = TipoMemoria(vk, pd, req.MemoryTypeBits,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
            };
            if (vk.AllocateMemory(dispositivo, &aloc, null, out memoria) != Result.Success) return null;
            vk.BindBufferMemory(dispositivo, buffer, memoria, 0);

            void* mapeado;
            if (vk.MapMemory(dispositivo, memoria, 0, tamanho, 0, &mapeado) != Result.Success) return null;

            // ---- shader ----
            var codigo = Spirv();
            fixed (byte* p = codigo)
            {
                var smInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)codigo.Length,
                    PCode = (uint*)p
                };
                if (vk.CreateShaderModule(dispositivo, &smInfo, null, out modulo) != Result.Success) return null;
            }

            // ---- descritores ----
            var ligacao = new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit
            };
            var dsl = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 1,
                PBindings = &ligacao
            };
            if (vk.CreateDescriptorSetLayout(dispositivo, &dsl, null, out layoutConjunto) != Result.Success) return null;

            var poolTamanho = new DescriptorPoolSize { Type = DescriptorType.StorageBuffer, DescriptorCount = 1 };
            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = 1,
                PoolSizeCount = 1,
                PPoolSizes = &poolTamanho
            };
            if (vk.CreateDescriptorPool(dispositivo, &poolInfo, null, out pool) != Result.Success) return null;

            var alocaConjunto = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = pool,
                DescriptorSetCount = 1,
                PSetLayouts = &layoutConjunto
            };
            if (vk.AllocateDescriptorSets(dispositivo, &alocaConjunto, out conjunto) != Result.Success) return null;

            var infoBuffer = new DescriptorBufferInfo { Buffer = buffer, Offset = 0, Range = tamanho };
            var escrita = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = conjunto,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &infoBuffer
            };
            vk.UpdateDescriptorSets(dispositivo, 1, &escrita, 0, null);

            // ---- pipeline ----
            var faixa = new PushConstantRange
            {
                StageFlags = ShaderStageFlags.ComputeBit,
                Offset = 0,
                Size = (uint)sizeof(Parametros)
            };
            var plInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &layoutConjunto,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &faixa
            };
            if (vk.CreatePipelineLayout(dispositivo, &plInfo, null, out layoutPipeline) != Result.Success) return null;

            var pPrincipal = (byte*)Marshal.StringToHGlobalAnsi("main");
            try
            {
                var estagio = new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ComputeBit,
                    Module = modulo,
                    PName = pPrincipal
                };
                var cpInfo = new ComputePipelineCreateInfo
                {
                    SType = StructureType.ComputePipelineCreateInfo,
                    Stage = estagio,
                    Layout = layoutPipeline
                };
                if (vk.CreateComputePipelines(dispositivo, default, 1, &cpInfo, null, out pipeline) != Result.Success)
                    return null;
            }
            finally { Marshal.FreeHGlobal((IntPtr)pPrincipal); }

            // ---- comandos ----
            var cpPool = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = (uint)familia,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };
            if (vk.CreateCommandPool(dispositivo, &cpPool, null, out poolComandos) != Result.Success) return null;

            var alocaCmd = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = poolComandos,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };
            if (vk.AllocateCommandBuffers(dispositivo, &alocaCmd, out var cmd) != Result.Success) return null;

            var cercaInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
            if (vk.CreateFence(dispositivo, &cercaInfo, null, out cerca) != Result.Success) return null;

            uint gruposX = (uint)((largura + Grupo - 1) / Grupo);
            uint gruposY = (uint)((altura + Grupo - 1) / Grupo);

            void Despachar(CommandBuffer cmdL, Fence cercaL, DescriptorSet conjuntoL, Parametros pars)
            {
                vk.ResetCommandBuffer(cmdL, 0);
                var inicio = new CommandBufferBeginInfo
                {
                    SType = StructureType.CommandBufferBeginInfo,
                    Flags = CommandBufferUsageFlags.OneTimeSubmitBit
                };
                vk.BeginCommandBuffer(cmdL, &inicio);

                vk.CmdBindPipeline(cmdL, PipelineBindPoint.Compute, pipeline);
                vk.CmdBindDescriptorSets(cmdL, PipelineBindPoint.Compute, layoutPipeline, 0, 1, &conjuntoL, 0, null);
                vk.CmdPushConstants(cmdL, layoutPipeline, ShaderStageFlags.ComputeBit, 0,
                    (uint)sizeof(Parametros), &pars);
                vk.CmdDispatch(cmdL, gruposX, gruposY, 1);

                var barreira = new MemoryBarrier
                {
                    SType = StructureType.MemoryBarrier,
                    SrcAccessMask = AccessFlags.ShaderWriteBit,
                    DstAccessMask = AccessFlags.HostReadBit
                };
                vk.CmdPipelineBarrier(cmdL, PipelineStageFlags.ComputeShaderBit, PipelineStageFlags.HostBit,
                    0, 1, &barreira, 0, null, 0, null);

                vk.EndCommandBuffer(cmdL);

                var submissao = new SubmitInfo
                {
                    SType = StructureType.SubmitInfo,
                    CommandBufferCount = 1,
                    PCommandBuffers = &cmdL
                };
                vk.ResetFences(dispositivo, 1, &cercaL);
                vk.QueueSubmit(fila, 1, &submissao, cercaL);
            }

            // aquecimento: compila o pipeline e sobe os relógios da GPU
            Despachar(cmd, cerca, conjunto, new Parametros { Largura = (uint)largura, Altura = (uint)altura, Amostras = 1, Semente = 1 });
            vk.WaitForFences(dispositivo, 1, &cerca, true, ulong.MaxValue);

            // ---- modo prévia animada: um fotograma por passagem, a câmara a orbitar ----
            if (fotograma is not null)
            {
                var imagem = new byte[largura * altura * 4];
                int n = 0;
                var relogioFotograma = Stopwatch.StartNew();
                var limite = Stopwatch.StartNew();
                while (!ct.IsCancellationRequested && limite.Elapsed.TotalMinutes < 30)
                {
                    var pars = new Parametros
                    {
                        Largura = (uint)largura, Altura = (uint)altura, Amostras = (uint)amostras,
                        Semente = 7, Angulo = AnguloDaVez(limite)
                    };
                    Despachar(cmd, cerca, conjunto, pars);
                    vk.WaitForFences(dispositivo, 1, &cerca, true, ulong.MaxValue);
                    Marshal.Copy((IntPtr)mapeado, imagem, 0, imagem.Length);
                    fotograma((byte[])imagem.Clone(), n++);
                    if (relogioFotograma.Elapsed.TotalMilliseconds < 15) Thread.Sleep(1);
                    relogioFotograma.Restart();
                }
                return null;   // é prévia: não produz pontuação
            }

            var parametros = new Parametros
            {
                Largura = (uint)largura,
                Altura = (uint)altura,
                Amostras = (uint)amostras,
                Semente = 7
            };

            var cronometro = Stopwatch.StartNew();
            int passagens = 0;
            do
            {
                Despachar(cmd, cerca, conjunto, parametros);
                vk.WaitForFences(dispositivo, 1, &cerca, true, ulong.MaxValue);
                passagens++;
            }
            while (cronometro.Elapsed.TotalSeconds < AlvoSegundos);
            cronometro.Stop();

            var rgba = new byte[largura * altura * 4];
            Marshal.Copy((IntPtr)mapeado, rgba, 0, rgba.Length);

            double segundos = cronometro.Elapsed.TotalSeconds;
            double amostrasTotais = (double)largura * altura * amostras * passagens;
            long pontos = segundos > 0 ? (long)(amostrasTotais / segundos / 1000.0) : 0;
            return new ResultadoGpu(nome, segundos, pontos, largura, altura, amostras, passagens, rgba);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (cerca.Handle != 0) vk.DestroyFence(dispositivo, cerca, null);
            if (poolComandos.Handle != 0) vk.DestroyCommandPool(dispositivo, poolComandos, null);
            if (pipeline.Handle != 0) vk.DestroyPipeline(dispositivo, pipeline, null);
            if (layoutPipeline.Handle != 0) vk.DestroyPipelineLayout(dispositivo, layoutPipeline, null);
            if (pool.Handle != 0) vk.DestroyDescriptorPool(dispositivo, pool, null);
            if (layoutConjunto.Handle != 0) vk.DestroyDescriptorSetLayout(dispositivo, layoutConjunto, null);
            if (modulo.Handle != 0) vk.DestroyShaderModule(dispositivo, modulo, null);
            if (buffer.Handle != 0) vk.DestroyBuffer(dispositivo, buffer, null);
            if (memoria.Handle != 0) vk.FreeMemory(dispositivo, memoria, null);
            if (dispositivo.Handle != 0) vk.DestroyDevice(dispositivo, null);
            vk.DestroyInstance(inst, null);
            vk.Dispose();
        }
    }
}
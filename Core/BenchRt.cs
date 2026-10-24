using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace LinuxExplode.Core;

public sealed record ResultadoRt(string Nome, double Segundos, long Pontos, int Largura, int Altura,
    int Amostras, int Passagens, int Esferas, byte[] Rgba);

/// <summary>
/// Benchmark de ray tracing por hardware: o mesmo path tracer, mas com a travessia do BVH
/// feita pelas unidades de RT (VK_KHR_ray_query sobre uma TLAS com um BLAS de AABBs).
/// A interseção exata das esferas continua analítica no shader, como nos motores reais.
/// </summary>
public static unsafe class BenchRt
{
    /// <summary>Ângulo da câmara na prévia animada: uma volta completa a cada ~18 s.</summary>
    internal static float AnguloDaVez(Stopwatch relogio) => (float)(relogio.Elapsed.TotalSeconds * 0.35);

    public const int LarguraPadrao = 1280;
    public const int AlturaPadrao = 720;
    public const int AmostrasPadrao = 64;
    public const double AlvoSegundos = 1.5;
    private const uint Grupo = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct Parametros
    {
        public uint Largura, Altura, Amostras, Semente, Total, TrisPorEsfera;
        public float Angulo;   // 0 = câmara do benchmark; != 0 orbita a cena (prévia animada)
    }

    private sealed record Esfera(float X, float Y, float Z, float R, float Cr, float Cg, float Cb, float E);

    /// <summary>Cena: 6 esferas protagonistas + grelha 8×8 no chão + sol emissivo.</summary>
    private static List<Esfera> Cena()
    {
        var l = new List<Esfera>
        {
            new(-1.60f, -0.35f,  0.00f, 0.65f, 0.92f, 0.26f, 0.28f, 0f),
            new( 0.00f, -0.45f, -0.35f, 0.55f, 0.25f, 0.55f, 0.92f, 0f),
            new( 1.55f, -0.30f,  0.10f, 0.70f, 0.90f, 0.85f, 0.75f, 0f),
            new( 0.75f, -0.75f,  1.10f, 0.25f, 0.95f, 0.72f, 0.25f, 0f),
            new(-0.75f, -0.78f,  0.90f, 0.22f, 0.30f, 0.85f, 0.55f, 0f),
            new( 3.10f,  1.60f, -1.20f, 0.90f, 1.00f, 0.96f, 0.88f, 1f)
        };

        for (int gx = 0; gx < 8; gx++)
        {
            for (int gz = 0; gz < 8; gz++)
            {
                float x = -5.95f + gx * 1.70f;
                float z = -1.10f + gz * 1.70f;
                const float r = 0.18f;
                if (l.Take(6).Any(h => (h.X - x) * (h.X - x) + (h.Z - z) * (h.Z - z) < (h.R + 0.55f) * (h.R + 0.55f)))
                    continue;
                int semente = gx * 37 + gz * 91;
                l.Add(new Esfera(x, -1.0f + r, z, r,
                    0.25f + (semente % 70) / 100f,
                    0.20f + (semente % 53) / 100f,
                    0.30f + (semente % 41) / 100f, 0f));
            }
        }
        return l;
    }

    /// <summary>Icosferas subdivididas duas vezes: 162 vértices e 320 triângulos por esfera.</summary>
    private static (float[] Vertices, uint[] Indices, int TrisPorEsfera) Malha(List<Esfera> esferas)
    {
        float t = (1f + MathF.Sqrt(5f)) / 2f;
        var v = new List<(float X, float Y, float Z)>
        {
            (-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0),
            (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
            (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)
        };
        // os 12 vértices do icosaedro não têm norma 1: normalizar antes de subdividir
        for (int i = 0; i < v.Count; i++)
        {
            var p0 = v[i];
            float l0 = MathF.Sqrt(p0.X * p0.X + p0.Y * p0.Y + p0.Z * p0.Z);
            v[i] = (p0.X / l0, p0.Y / l0, p0.Z / l0);
        }

        var f = new List<int[]>
        {
            new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
            new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
            new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
        };

        for (int s = 0; s < 2; s++)
        {
            var novos = new List<int[]>();
            var cache = new Dictionary<(int, int), int>();

            int Meio(int a, int b)
            {
                var chave = a < b ? (a, b) : (b, a);
                if (cache.TryGetValue(chave, out var existente)) return existente;
                var pa = v[a];
                var pb = v[b];
                float mx = pa.X + pb.X, my = pa.Y + pb.Y, mz = pa.Z + pb.Z;
                float len = MathF.Sqrt(mx * mx + my * my + mz * mz);
                v.Add((mx / len, my / len, mz / len));
                cache[chave] = v.Count - 1;
                return v.Count - 1;
            }

            foreach (var tri in f)
            {
                int ab = Meio(tri[0], tri[1]);
                int bc = Meio(tri[1], tri[2]);
                int ca = Meio(tri[2], tri[0]);
                novos.Add(new[] { tri[0], ab, ca });
                novos.Add(new[] { tri[1], bc, ab });
                novos.Add(new[] { tri[2], ca, bc });
                novos.Add(new[] { ab, bc, ca });
            }
            f = novos;
        }

        int nv = v.Count, nf = f.Count;
        var vertices = new float[esferas.Count * nv * 3];
        var indices = new uint[esferas.Count * nf * 3];

        for (int s = 0; s < esferas.Count; s++)
        {
            var e = esferas[s];
            for (int i = 0; i < nv; i++)
            {
                int baseIdx = (s * nv + i) * 3;
                vertices[baseIdx + 0] = e.X + v[i].X * e.R;
                vertices[baseIdx + 1] = e.Y + v[i].Y * e.R;
                vertices[baseIdx + 2] = e.Z + v[i].Z * e.R;
            }
            for (int k = 0; k < nf; k++)
            {
                int baseIdx = (s * nf + k) * 3;
                indices[baseIdx + 0] = (uint)(s * nv + f[k][0]);
                indices[baseIdx + 1] = (uint)(s * nv + f[k][1]);
                indices[baseIdx + 2] = (uint)(s * nv + f[k][2]);
            }
        }
        return (vertices, indices, nf);
    }

    private static byte[] Spirv()
    {
        var asm = Assembly.GetExecutingAssembly();
        var nome = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("rt.comp.spv", StringComparison.Ordinal))
                   ?? throw new InvalidOperationException("shader rt.comp.spv não embutido");
        using var s = asm.GetManifestResourceStream(nome)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static bool TemExtensao(Vk vk, PhysicalDevice pd, string nome)
    {
        uint n = 0;
        vk.EnumerateDeviceExtensionProperties(pd, (byte*)null, ref n, null);
        if (n == 0) return false;
        var arr = new ExtensionProperties[n];
        fixed (ExtensionProperties* p = arr) vk.EnumerateDeviceExtensionProperties(pd, (byte*)null, ref n, p);
        foreach (var e in arr)
        {
            if (Marshal.PtrToStringAnsi((IntPtr)e.ExtensionName) == nome) return true;
        }
        return false;
    }

    /// <summary>GPUs com VK_KHR_ray_query + VK_KHR_acceleration_structure.</summary>
    public static List<InfoGpu> Dispositivos()
    {
        var lista = new List<InfoGpu>();
        Vk vk;
        Instance inst;
        try { (vk, inst) = CriarInstancia(); } catch { return lista; }
        try
        {
            uint n = 0;
            vk.EnumeratePhysicalDevices(inst, ref n, null);
            if (n == 0) return lista;
            var pds = new PhysicalDevice[n];
            fixed (PhysicalDevice* p = pds) vk.EnumeratePhysicalDevices(inst, ref n, p);

            var vistos = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < pds.Length; i++)
            {
                if (!TemExtensao(vk, pds[i], "VK_KHR_acceleration_structure") ||
                    !TemExtensao(vk, pds[i], "VK_KHR_ray_query")) continue;

                PhysicalDeviceProperties props;
                vk.GetPhysicalDeviceProperties(pds[i], &props);
                if (props.DeviceType == PhysicalDeviceType.Cpu) continue;

                var nome = Marshal.PtrToStringAnsi((IntPtr)props.DeviceName) ?? "GPU";
                if (!vistos.Add(nome)) continue;
                lista.Add(new InfoGpu(i, nome, props.DeviceType == PhysicalDeviceType.DiscreteGpu ? "dedicada" : "integrada"));
            }
        }
        catch { }
        finally { vk.DestroyInstance(inst, null); vk.Dispose(); }

        return lista.OrderBy(g => g.Tipo == "dedicada" ? 0 : 1).ToList();
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
            ApiVersion = Vk.Version12
        };
        var ci = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &app };

        // diagnóstico: EXPLODE_RT_VALIDATE=1 liga as camadas de validação
        var pCamada = Marshal.StringToHGlobalAnsi("VK_LAYER_KHRONOS_validation");
        if (Environment.GetEnvironmentVariable("EXPLODE_RT_VALIDATE") == "1")
        {
            ci.EnabledLayerCount = 1;
            ci.PpEnabledLayerNames = (byte**)&pCamada;
            var pCamadaPtr = (byte*)pCamada;
            ci.PpEnabledLayerNames = &pCamadaPtr;
        }

        var r = vk.CreateInstance(&ci, null, out var inst);
        Marshal.FreeHGlobal(pNome);
        Marshal.FreeHGlobal(pCamada);
        if (r != Result.Success) throw new InvalidOperationException($"vkCreateInstance: {r}");
        return (vk, inst);
    }

    private static void Diag(string msg)
    {
        if (Environment.GetEnvironmentVariable("EXPLODE_RT_DEBUG") == "1")
            Console.Error.WriteLine("[rt] " + msg);
    }

    private static ulong Endereco(Vk vk, Device dev, VkBuffer buf)
    {
        var info = new BufferDeviceAddressInfo { SType = StructureType.BufferDeviceAddressInfo, Buffer = buf };
        return vk.GetBufferDeviceAddress(dev, &info);
    }

    private static (VkBuffer Buf, DeviceMemory Mem) CriarBuffer(Vk vk, Device dev, PhysicalDevice pd,
        ulong tamanho, BufferUsageFlags uso, bool visivelHost)
    {
        var ci = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = tamanho,
            Usage = uso,
            SharingMode = SharingMode.Exclusive
        };
        if (vk.CreateBuffer(dev, &ci, null, out var buf) != Result.Success)
            throw new InvalidOperationException("vkCreateBuffer falhou");

        vk.GetBufferMemoryRequirements(dev, buf, out var req);
        vk.GetPhysicalDeviceMemoryProperties(pd, out var mp);

        var desejado = visivelHost
            ? MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            : MemoryPropertyFlags.DeviceLocalBit;

        uint tipo = uint.MaxValue;
        for (uint i = 0; i < mp.MemoryTypeCount; i++)
        {
            if ((req.MemoryTypeBits & (1u << (int)i)) == 0) continue;
            if ((mp.MemoryTypes[(int)i].PropertyFlags & desejado) == desejado) { tipo = i; break; }
        }
        if (tipo == uint.MaxValue) throw new InvalidOperationException("sem tipo de memória para o buffer");

        var aloc = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = req.Size,
            MemoryTypeIndex = tipo
        };

        // obrigatório em buffers com SHADER_DEVICE_ADDRESS: a memória tem de declarar o flag
        var flagsAloc = new MemoryAllocateFlagsInfo
        {
            SType = StructureType.MemoryAllocateFlagsInfo,
            Flags = MemoryAllocateFlags.DeviceAddressBit
        };
        if ((uso & BufferUsageFlags.ShaderDeviceAddressBitKhr) != 0 ||
            (uso & BufferUsageFlags.AccelerationStructureStorageBitKhr) != 0)
            aloc.PNext = &flagsAloc;

        if (vk.AllocateMemory(dev, &aloc, null, out var mem) != Result.Success)
            throw new InvalidOperationException("vkAllocateMemory falhou");
        if (vk.BindBufferMemory(dev, buf, mem, 0) != Result.Success)
            throw new InvalidOperationException("vkBindBufferMemory falhou");
        return (buf, mem);
    }

    /// <summary>Corre o path tracer com ray query. Devolve null se a GPU não suportar ray tracing.</summary>
    public static ResultadoRt? Correr(int indice, int largura = LarguraPadrao, int altura = AlturaPadrao,
        int amostras = AmostrasPadrao, Action<byte[], int>? fotograma = null, CancellationToken ct = default)
    {
        if (largura <= 0 || altura <= 0 || amostras <= 0) throw new ArgumentOutOfRangeException(nameof(largura));

        Vk vk;
        Instance inst;
        try { (vk, inst) = CriarInstancia(); } catch { return null; }

        Device dev = default;
        KhrAccelerationStructure? khr = null;
        AccelerationStructureKHR blasKhr = default, tlasKhr = default;
        VkBuffer bufEsferas = default, bufSaida = default, bufVertices = default, bufIndices = default, bufInst = default;
        DeviceMemory memEsferas = default, memSaida = default, memVertices = default, memIndices = default, memInst = default,
                     memBlasScratch = default, memBlasAs = default, memTlasScratch = default, memTlasAs = default;
        VkBuffer bufBlasScratch = default, bufBlasAs = default, bufTlasScratch = default, bufTlasAs = default;
        ShaderModule modulo = default;
        DescriptorSetLayout layoutConjunto = default;
        DescriptorPool pool = default;
        PipelineLayout layoutPipeline = default;
        Pipeline pipeline = default;
        CommandPool poolComandos = default;
        Fence cerca = default;
        var nome = "GPU";

        try
        {
            uint n = 0;
            vk.EnumeratePhysicalDevices(inst, ref n, null);
            if (n == 0) return null;
            var pds = new PhysicalDevice[n];
            fixed (PhysicalDevice* p = pds) vk.EnumeratePhysicalDevices(inst, ref n, p);
            var pd = indice >= 0 && indice < pds.Length ? pds[indice] : pds[0];

            if (!TemExtensao(vk, pd, "VK_KHR_acceleration_structure") || !TemExtensao(vk, pd, "VK_KHR_ray_query"))
                return null;

            PhysicalDeviceProperties props;
            vk.GetPhysicalDeviceProperties(pd, &props);
            nome = Marshal.PtrToStringAnsi((IntPtr)props.DeviceName) ?? "GPU";

            // fila de computação
            uint nf = 0;
            vk.GetPhysicalDeviceQueueFamilyProperties(pd, ref nf, null);
            var fams = new QueueFamilyProperties[nf];
            fixed (QueueFamilyProperties* p = fams) vk.GetPhysicalDeviceQueueFamilyProperties(pd, ref nf, p);
            int familia = -1;
            for (int i = 0; i < fams.Length; i++)
                if ((fams[i].QueueFlags & QueueFlags.ComputeBit) != 0) { familia = i; break; }
            if (familia < 0) return null;

            // ---- dispositivo com as features de ray tracing ----
            var f12 = new PhysicalDeviceVulkan12Features
            {
                SType = StructureType.PhysicalDeviceVulkan12Features,
                BufferDeviceAddress = true
            };
            var fRq = new PhysicalDeviceRayQueryFeaturesKHR
            {
                SType = StructureType.PhysicalDeviceRayQueryFeaturesKhr,
                PNext = &f12,
                RayQuery = true
            };
            var fAs = new PhysicalDeviceAccelerationStructureFeaturesKHR
            {
                SType = StructureType.PhysicalDeviceAccelerationStructureFeaturesKhr,
                PNext = &fRq,
                AccelerationStructure = true
            };
            var f2 = new PhysicalDeviceFeatures2
            {
                SType = StructureType.PhysicalDeviceFeatures2,
                PNext = &fAs
            };

            var nomes = new[] { "VK_KHR_acceleration_structure", "VK_KHR_ray_query", "VK_KHR_deferred_host_operations" };
            var ptrs = new IntPtr[nomes.Length];
            for (int i = 0; i < nomes.Length; i++) ptrs[i] = Marshal.StringToHGlobalAnsi(nomes[i]);

            float prioridade = 1f;
            var filaInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = (uint)familia,
                QueueCount = 1,
                PQueuePriorities = &prioridade
            };

            fixed (byte** ppNomes = new byte*[nomes.Length])
            {
                for (int i = 0; i < nomes.Length; i++) ppNomes[i] = (byte*)ptrs[i];

                var devInfo = new DeviceCreateInfo
                {
                    SType = StructureType.DeviceCreateInfo,
                    PNext = &f2,
                    QueueCreateInfoCount = 1,
                    PQueueCreateInfos = &filaInfo,
                    EnabledExtensionCount = (uint)nomes.Length,
                    PpEnabledExtensionNames = ppNomes
                };
                var r = vk.CreateDevice(pd, &devInfo, null, out dev);
                if (r != Result.Success) return null;
            }
            foreach (var p in ptrs) Marshal.FreeHGlobal(p);

            vk.GetDeviceQueue(dev, (uint)familia, 0, out var fila);
            khr = new KhrAccelerationStructure(vk.Context);

            // ---- cena ----
            var esferas = Cena();
            int total = esferas.Count;

            // buffer de esferas: 2 vec4 por esfera
            (bufEsferas, memEsferas) = CriarBuffer(vk, dev, pd, (ulong)(total * 32), BufferUsageFlags.StorageBufferBit, true);
            void* pEsferas;
            vk.MapMemory(dev, memEsferas, 0, (ulong)(total * 32), 0, &pEsferas);
            var fl = (float*)pEsferas;
            for (int i = 0; i < total; i++)
            {
                var e = esferas[i];
                fl[i * 8 + 0] = e.X; fl[i * 8 + 1] = e.Y; fl[i * 8 + 2] = e.Z; fl[i * 8 + 3] = e.R;
                fl[i * 8 + 4] = e.Cr; fl[i * 8 + 5] = e.Cg; fl[i * 8 + 6] = e.Cb; fl[i * 8 + 7] = e.E;
            }
            vk.UnmapMemory(dev, memEsferas);

            // malha de triângulos: cada esfera é uma icosesfera com 320 triângulos
            var (vertices, indices, trisPorEsfera) = Malha(esferas);
            int numVertices = vertices.Length / 3;
            int numTriangulos = indices.Length / 3;

            ulong tamVert = (ulong)(vertices.Length * 4);
            (bufVertices, memVertices) = CriarBuffer(vk, dev, pd, tamVert,
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBitKhr |
                BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr, true);
            void* pVert;
            vk.MapMemory(dev, memVertices, 0, tamVert, 0, &pVert);
            fixed (float* origem = vertices) System.Buffer.MemoryCopy(origem, pVert, (long)tamVert, vertices.Length * 4);
            vk.UnmapMemory(dev, memVertices);

            ulong tamIdx = (ulong)(indices.Length * 4);
            (bufIndices, memIndices) = CriarBuffer(vk, dev, pd, tamIdx,
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBitKhr |
                BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr, true);
            void* pIdx;
            vk.MapMemory(dev, memIndices, 0, tamIdx, 0, &pIdx);
            fixed (uint* origemIdx = indices) System.Buffer.MemoryCopy(origemIdx, pIdx, (long)tamIdx, indices.Length * 4);
            vk.UnmapMemory(dev, memIndices);

            ulong vertAddr = Endereco(vk, dev, bufVertices);
            ulong idxAddr = Endereco(vk, dev, bufIndices);
            Diag($"malha: {numVertices} vértices, {numTriangulos} triângulos ({trisPorEsfera}/esfera) vert=0x{vertAddr:x} idx=0x{idxAddr:x}");
            if (Environment.GetEnvironmentVariable("EXPLODE_RT_DEBUG") == "1")
            {
                int nvPorEsfera = numVertices / total;
                float rMin = float.MaxValue, rMax = 0;
                for (int i = 0; i < nvPorEsfera; i++)
                {
                    float dx = vertices[i * 3] - esferas[0].X, dy = vertices[i * 3 + 1] - esferas[0].Y, dz = vertices[i * 3 + 2] - esferas[0].Z;
                    float r = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    rMin = MathF.Min(rMin, r); rMax = MathF.Max(rMax, r);
                }
                uint maxIdx = 0;
                for (int i = 0; i < trisPorEsfera * 3; i++) maxIdx = Math.Max(maxIdx, indices[i]);
                Diag($"esfera0: {nvPorEsfera} vértices, raio min={rMin:0.0000} max={rMax:0.0000} (esperado {esferas[0].R}) · índice máximo={maxIdx}");
            }

            // ---- BLAS ----
            var geoAabb = new AccelerationStructureGeometryKHR
            {
                SType = StructureType.AccelerationStructureGeometryKhr,
                GeometryType = GeometryTypeKHR.TrianglesKhr,
                Flags = GeometryFlagsKHR.OpaqueBitKhr,
                Geometry = new AccelerationStructureGeometryDataKHR
                {
                    Triangles = new AccelerationStructureGeometryTrianglesDataKHR
                    {
                        SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
                        VertexFormat = Format.R32G32B32Sfloat,
                        VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = vertAddr },
                        VertexStride = 12,
                        MaxVertex = (uint)(numVertices - 1),
                        IndexType = IndexType.Uint32,
                        IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = idxAddr }
                    }
                }
            };

            var buildBlas = new AccelerationStructureBuildGeometryInfoKHR
            {
                SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
                Type = AccelerationStructureTypeKHR.AccelerationStructureTypeBottomLevelKhr,
                Flags = 0,
                Mode = BuildAccelerationStructureModeKHR.BuildKhr,
                GeometryCount = 1,
                PGeometries = &geoAabb
            };

            uint maxPrim = (uint)numTriangulos;
            khr.GetAccelerationStructureBuildSizes(dev, AccelerationStructureBuildTypeKHR.DeviceKhr,
                &buildBlas, &maxPrim, out var tamBlas);

            (bufBlasAs, memBlasAs) = CriarBuffer(vk, dev, pd, tamBlas.AccelerationStructureSize,
                BufferUsageFlags.AccelerationStructureStorageBitKhr | BufferUsageFlags.ShaderDeviceAddressBitKhr, false);
            var ciBlas = new AccelerationStructureCreateInfoKHR
            {
                SType = StructureType.AccelerationStructureCreateInfoKhr,
                Buffer = bufBlasAs,
                Offset = 0,
                Size = tamBlas.AccelerationStructureSize,
                Type = AccelerationStructureTypeKHR.AccelerationStructureTypeBottomLevelKhr
            };
            if (khr.CreateAccelerationStructure(dev, &ciBlas, null, out blasKhr) != Result.Success) return null;

            (bufBlasScratch, memBlasScratch) = CriarBuffer(vk, dev, pd, tamBlas.BuildScratchSize,
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBitKhr, false);

            buildBlas.DstAccelerationStructure = blasKhr;
            var scratchBlasAddr = Endereco(vk, dev, bufBlasScratch);
            buildBlas.ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = scratchBlasAddr };
            Diag($"scratchBlas=0x{scratchBlasAddr:x} tamanho={tamBlas.AccelerationStructureSize} scratchSz={tamBlas.BuildScratchSize} maxPrim={maxPrim}");

            // ---- instância + TLAS ----
            ulong blasAddr = khr.GetAccelerationStructureDeviceAddress(dev,
                new AccelerationStructureDeviceAddressInfoKHR
                {
                    SType = StructureType.AccelerationStructureDeviceAddressInfoKhr,
                    AccelerationStructure = blasKhr
                });

            (bufInst, memInst) = CriarBuffer(vk, dev, pd, 64,
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBitKhr |
                BufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKhr, true);
            void* pInst;
            vk.MapMemory(dev, memInst, 0, 64, 0, &pInst);
            var instBytes = (byte*)pInst;
            for (int i = 0; i < 64; i++) instBytes[i] = 0;
            float* m = (float*)instBytes;
            m[0] = 1f; m[5] = 1f; m[10] = 1f;      // matriz identidade 3×4
            *(uint*)(instBytes + 48) = 0xFF000000u;  // instanceCustomIndex=0 (bits 0-23) | mask=0xFF (bits 24-31)
            *(uint*)(instBytes + 52) = 0u;          // sbtOffset=0 | flags=0
            *(ulong*)(instBytes + 56) = blasAddr;
            if (Environment.GetEnvironmentVariable("EXPLODE_RT_DEBUG") == "1")
            {
                var hex = new System.Text.StringBuilder();
                for (int i = 0; i < 64; i++) { hex.Append(instBytes[i].ToString("x2")); if (i % 16 == 15) hex.Append(' '); }
                Diag("instancia: " + hex);
            }
            {
                var faixaFlush = new MappedMemoryRange { SType = StructureType.MappedMemoryRange, Memory = memInst, Offset = 0, Size = Vk.WholeSize };
                vk.FlushMappedMemoryRanges(dev, 1, &faixaFlush);
            }
            vk.UnmapMemory(dev, memInst);
            ulong instAddr = Endereco(vk, dev, bufInst);

            var geoInst = new AccelerationStructureGeometryKHR
            {
                SType = StructureType.AccelerationStructureGeometryKhr,
                GeometryType = GeometryTypeKHR.GeometryTypeInstancesKhr,
                Flags = GeometryFlagsKHR.OpaqueBitKhr,
                Geometry = new AccelerationStructureGeometryDataKHR
                {
                    Instances = new AccelerationStructureGeometryInstancesDataKHR
                    {
                        SType = StructureType.AccelerationStructureGeometryInstancesDataKhr,
                        ArrayOfPointers = false,
                        Data = new DeviceOrHostAddressConstKHR { DeviceAddress = instAddr }
                    }
                }
            };

            var buildTlas = new AccelerationStructureBuildGeometryInfoKHR
            {
                SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
                Type = AccelerationStructureTypeKHR.AccelerationStructureTypeTopLevelKhr,
                Flags = 0,
                Mode = BuildAccelerationStructureModeKHR.BuildKhr,
                GeometryCount = 1,
                PGeometries = &geoInst
            };

            uint maxInst = 1;
            khr.GetAccelerationStructureBuildSizes(dev, AccelerationStructureBuildTypeKHR.DeviceKhr,
                &buildTlas, &maxInst, out var tamTlas);

            (bufTlasAs, memTlasAs) = CriarBuffer(vk, dev, pd, tamTlas.AccelerationStructureSize,
                BufferUsageFlags.AccelerationStructureStorageBitKhr | BufferUsageFlags.ShaderDeviceAddressBitKhr, false);
            var ciTlas = new AccelerationStructureCreateInfoKHR
            {
                SType = StructureType.AccelerationStructureCreateInfoKhr,
                Buffer = bufTlasAs,
                Offset = 0,
                Size = tamTlas.AccelerationStructureSize,
                Type = AccelerationStructureTypeKHR.AccelerationStructureTypeTopLevelKhr
            };
            if (khr.CreateAccelerationStructure(dev, &ciTlas, null, out tlasKhr) != Result.Success) return null;

            (bufTlasScratch, memTlasScratch) = CriarBuffer(vk, dev, pd, tamTlas.BuildScratchSize,
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBitKhr, false);

            buildTlas.DstAccelerationStructure = tlasKhr;
            var scratchTlasAddr = Endereco(vk, dev, bufTlasScratch);
            buildTlas.ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = scratchTlasAddr };
            Diag($"blas=0x{blasAddr:x} inst=0x{instAddr:x} scratchTlas=0x{scratchTlasAddr:x} tlasSz={tamTlas.AccelerationStructureSize} maxInst={maxInst}");

            var cpPool = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = (uint)familia,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };
            if (vk.CreateCommandPool(dev, &cpPool, null, out poolComandos) != Result.Success) return null;

            var cercaInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
            if (vk.CreateFence(dev, &cercaInfo, null, out cerca) != Result.Success) return null;

            // ---- construir BLAS e TLAS ----
            {
                var alocaCmd = new CommandBufferAllocateInfo
                {
                    SType = StructureType.CommandBufferAllocateInfo,
                    CommandPool = poolComandos,
                    Level = CommandBufferLevel.Primary,
                    CommandBufferCount = 1
                };
                vk.AllocateCommandBuffers(dev, &alocaCmd, out var cmdBuild);
                var inicio = new CommandBufferBeginInfo
                {
                    SType = StructureType.CommandBufferBeginInfo,
                    Flags = CommandBufferUsageFlags.OneTimeSubmitBit
                };
                vk.BeginCommandBuffer(cmdBuild, &inicio);

                // BLAS
                var rBlas = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = (uint)numTriangulos };
                var pRBlas = &rBlas;
                var bBlas = buildBlas;
                khr.CmdBuildAccelerationStructures(cmdBuild, 1, &bBlas, &pRBlas);
                var barreiraBlas = new MemoryBarrier
                {
                    SType = StructureType.MemoryBarrier,
                    SrcAccessMask = AccessFlags.AccelerationStructureWriteBitKhr,
                    DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr
                };
                vk.CmdPipelineBarrier(cmdBuild, PipelineStageFlags.AccelerationStructureBuildBitKhr,
                    PipelineStageFlags.AccelerationStructureBuildBitKhr, 0, 1, &barreiraBlas, 0, null, 0, null);
                vk.EndCommandBuffer(cmdBuild);

                var subBlas = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cmdBuild };
                var rq1 = vk.QueueSubmit(fila, 1, &subBlas, cerca);
                var rw1 = vk.WaitForFences(dev, 1, &cerca, true, ulong.MaxValue);
                vk.ResetFences(dev, 1, &cerca);
                Diag($"build BLAS submit={rq1} wait={rw1}");

                // TLAS: submissão separada, para garantir que o BLAS já está construído
                vk.ResetCommandBuffer(cmdBuild, 0);
                var ini2 = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
                vk.BeginCommandBuffer(cmdBuild, &ini2);
                var rTlas = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = 1 };
                var pRTlas = &rTlas;
                var bTlas = buildTlas;
                khr.CmdBuildAccelerationStructures(cmdBuild, 1, &bTlas, &pRTlas);
                var barreira = new MemoryBarrier
                {
                    SType = StructureType.MemoryBarrier,
                    SrcAccessMask = AccessFlags.AccelerationStructureWriteBitKhr,
                    DstAccessMask = AccessFlags.AccelerationStructureReadBitKhr | AccessFlags.ShaderReadBit
                };
                vk.CmdPipelineBarrier(cmdBuild, PipelineStageFlags.AccelerationStructureBuildBitKhr,
                    PipelineStageFlags.ComputeShaderBit, 0, 1, &barreira, 0, null, 0, null);
                vk.EndCommandBuffer(cmdBuild);
                var sub = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cmdBuild };
                var rq = vk.QueueSubmit(fila, 1, &sub, cerca);
                var rw = vk.WaitForFences(dev, 1, &cerca, true, ulong.MaxValue);
                vk.ResetFences(dev, 1, &cerca);
                Diag($"build TLAS submit={rq} wait={rw}");
            }

            // ---- saída + descritores + pipeline ----
            ulong tamSaida = (ulong)(largura * altura * 4);
            (bufSaida, memSaida) = CriarBuffer(vk, dev, pd, tamSaida, BufferUsageFlags.StorageBufferBit, true);
            void* pSaida;
            vk.MapMemory(dev, memSaida, 0, tamSaida, 0, &pSaida);

            var ligacoes = stackalloc DescriptorSetLayoutBinding[3];
            ligacoes[0] = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.AccelerationStructureKhr, DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit };
            ligacoes[1] = new DescriptorSetLayoutBinding { Binding = 1, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit };
            ligacoes[2] = new DescriptorSetLayoutBinding { Binding = 2, DescriptorType = DescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit };

            var dsl = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 3,
                PBindings = ligacoes
            };
            if (vk.CreateDescriptorSetLayout(dev, &dsl, null, out layoutConjunto) != Result.Success) return null;

            var tamanhos = stackalloc DescriptorPoolSize[2];
            tamanhos[0] = new DescriptorPoolSize { Type = DescriptorType.AccelerationStructureKhr, DescriptorCount = 1 };
            tamanhos[1] = new DescriptorPoolSize { Type = DescriptorType.StorageBuffer, DescriptorCount = 2 };
            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = 1,
                PoolSizeCount = 2,
                PPoolSizes = tamanhos
            };
            if (vk.CreateDescriptorPool(dev, &poolInfo, null, out pool) != Result.Success) return null;

            var alocaSet = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = pool,
                DescriptorSetCount = 1,
                PSetLayouts = &layoutConjunto
            };
            if (vk.AllocateDescriptorSets(dev, &alocaSet, out var conjunto) != Result.Success) return null;

            var infoAs = new WriteDescriptorSetAccelerationStructureKHR
            {
                SType = StructureType.WriteDescriptorSetAccelerationStructureKhr,
                AccelerationStructureCount = 1,
                PAccelerationStructures = &tlasKhr
            };
            var infoSaida = new DescriptorBufferInfo { Buffer = bufSaida, Offset = 0, Range = tamSaida };
            var infoEsferas = new DescriptorBufferInfo { Buffer = bufEsferas, Offset = 0, Range = (ulong)(total * 32) };

            var escritas = stackalloc WriteDescriptorSet[3];
            escritas[0] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                PNext = &infoAs,
                DstSet = conjunto,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.AccelerationStructureKhr
            };
            escritas[1] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = conjunto,
                DstBinding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &infoSaida
            };
            escritas[2] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = conjunto,
                DstBinding = 2,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &infoEsferas
            };
            vk.UpdateDescriptorSets(dev, 3, escritas, 0, null);

            var codigo = Spirv();
            fixed (byte* pc = codigo)
            {
                var smInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)codigo.Length,
                    PCode = (uint*)pc
                };
                if (vk.CreateShaderModule(dev, &smInfo, null, out modulo) != Result.Success) return null;
            }

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
            if (vk.CreatePipelineLayout(dev, &plInfo, null, out layoutPipeline) != Result.Success) return null;

            var pMain = (byte*)Marshal.StringToHGlobalAnsi("main");
            try
            {
                var estagio = new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ComputeBit,
                    Module = modulo,
                    PName = pMain
                };
                var cpInfo = new ComputePipelineCreateInfo
                {
                    SType = StructureType.ComputePipelineCreateInfo,
                    Stage = estagio,
                    Layout = layoutPipeline
                };
                if (vk.CreateComputePipelines(dev, default, 1, &cpInfo, null, out pipeline) != Result.Success) return null;
            }
            finally { Marshal.FreeHGlobal((IntPtr)pMain); }

            var alocaCmd2 = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = poolComandos,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };
            vk.AllocateCommandBuffers(dev, &alocaCmd2, out var cmd);

            uint gx = (uint)((largura + Grupo - 1) / Grupo);
            uint gy = (uint)((altura + Grupo - 1) / Grupo);

            void Despachar(CommandBuffer c, Fence f, DescriptorSet s, Parametros pars)
            {
                vk.ResetCommandBuffer(c, 0);
                var ini = new CommandBufferBeginInfo
                {
                    SType = StructureType.CommandBufferBeginInfo,
                    Flags = CommandBufferUsageFlags.OneTimeSubmitBit
                };
                vk.BeginCommandBuffer(c, &ini);
                vk.CmdBindPipeline(c, PipelineBindPoint.Compute, pipeline);
                vk.CmdBindDescriptorSets(c, PipelineBindPoint.Compute, layoutPipeline, 0, 1, &s, 0, null);
                vk.CmdPushConstants(c, layoutPipeline, ShaderStageFlags.ComputeBit, 0, (uint)sizeof(Parametros), &pars);
                vk.CmdDispatch(c, gx, gy, 1);

                var bar = new MemoryBarrier
                {
                    SType = StructureType.MemoryBarrier,
                    SrcAccessMask = AccessFlags.ShaderWriteBit,
                    DstAccessMask = AccessFlags.HostReadBit
                };
                vk.CmdPipelineBarrier(c, PipelineStageFlags.ComputeShaderBit, PipelineStageFlags.HostBit,
                    0, 1, &bar, 0, null, 0, null);
                vk.EndCommandBuffer(c);

                var sub = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &c };
                vk.ResetFences(dev, 1, &f);
                vk.QueueSubmit(fila, 1, &sub, f);
            }

            Despachar(cmd, cerca, conjunto, new Parametros
            {
                Largura = (uint)largura, Altura = (uint)altura, Amostras = 1, Semente = 1,
                Total = (uint)total, TrisPorEsfera = (uint)trisPorEsfera
            });
            vk.WaitForFences(dev, 1, &cerca, true, ulong.MaxValue);

            // ---- modo prévia animada: um fotograma por passagem, a câmara a orbitar ----
            if (fotograma is not null)
            {
                var imagem = new byte[largura * altura * 4];
                int fotogramaN = 0;
                var relogioFotograma = Stopwatch.StartNew();
                var limite = Stopwatch.StartNew();
                while (!ct.IsCancellationRequested && limite.Elapsed.TotalMinutes < 30)
                {
                    var pv = new Parametros
                    {
                        Largura = (uint)largura, Altura = (uint)altura, Amostras = (uint)amostras,
                        Semente = (uint)(11 + fotogramaN), Total = (uint)total, TrisPorEsfera = (uint)trisPorEsfera,
                        Angulo = AnguloDaVez(limite)
                    };
                    Despachar(cmd, cerca, conjunto, pv);
                    vk.WaitForFences(dev, 1, &cerca, true, ulong.MaxValue);
                    Marshal.Copy((IntPtr)pSaida, imagem, 0, imagem.Length);
                    fotograma((byte[])imagem.Clone(), fotogramaN++);
                    if (relogioFotograma.Elapsed.TotalMilliseconds < 15) Thread.Sleep(1);
                    relogioFotograma.Restart();
                }
                return null;   // é prévia: não produz pontuação
            }

            var pars = new Parametros
            {
                Largura = (uint)largura, Altura = (uint)altura, Amostras = (uint)amostras,
                Semente = 11, Total = (uint)total, TrisPorEsfera = (uint)trisPorEsfera
            };

            var cronometro = Stopwatch.StartNew();
            int passagens = 0;
            do
            {
                Despachar(cmd, cerca, conjunto, pars);
                vk.WaitForFences(dev, 1, &cerca, true, ulong.MaxValue);
                passagens++;
            }
            while (cronometro.Elapsed.TotalSeconds < AlvoSegundos);
            cronometro.Stop();

            var rgba = new byte[largura * altura * 4];
            Marshal.Copy((IntPtr)pSaida, rgba, 0, rgba.Length);

            double segundos = cronometro.Elapsed.TotalSeconds;
            double amostrasTotais = (double)largura * altura * amostras * passagens;
            long pontos = segundos > 0 ? (long)(amostrasTotais / segundos / 1000.0) : 0;
            return new ResultadoRt(nome, segundos, pontos, largura, altura, amostras, passagens, total, rgba);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (cerca.Handle != 0) vk.DestroyFence(dev, cerca, null);
            if (poolComandos.Handle != 0) vk.DestroyCommandPool(dev, poolComandos, null);
            if (pipeline.Handle != 0) vk.DestroyPipeline(dev, pipeline, null);
            if (layoutPipeline.Handle != 0) vk.DestroyPipelineLayout(dev, layoutPipeline, null);
            if (pool.Handle != 0) vk.DestroyDescriptorPool(dev, pool, null);
            if (layoutConjunto.Handle != 0) vk.DestroyDescriptorSetLayout(dev, layoutConjunto, null);
            if (modulo.Handle != 0) vk.DestroyShaderModule(dev, modulo, null);
            if (khr is not null)
            {
                if (tlasKhr.Handle != 0) khr.DestroyAccelerationStructure(dev, tlasKhr, null);
                if (blasKhr.Handle != 0) khr.DestroyAccelerationStructure(dev, blasKhr, null);
            }
            foreach (var b in new[] { bufEsferas, bufSaida, bufVertices, bufIndices, bufInst, bufBlasScratch, bufBlasAs, bufTlasScratch, bufTlasAs })
                if (b.Handle != 0) vk.DestroyBuffer(dev, b, null);
            foreach (var m2 in new[] { memEsferas, memSaida, memVertices, memIndices, memInst, memBlasScratch, memBlasAs, memTlasScratch, memTlasAs })
                if (m2.Handle != 0) vk.FreeMemory(dev, m2, null);
            if (dev.Handle != 0) vk.DestroyDevice(dev, null);
            vk.DestroyInstance(inst, null);
            vk.Dispose();
        }
    }
}
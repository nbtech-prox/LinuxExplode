# LINUX EXPLODE

Reescrita da app original de Windows em **Avalonia 11 + .NET 8**, para correr nativamente
em Linux (e empacotada em Flatpak). Toda a informação de hardware é lida diretamente de
`/proc`, `/sys` e DMI — sem WMI, sem registo, sem root para as funcionalidades principais.

![estado](https://img.shields.io/badge/estado-funcional-9BE8A8)
[![CI](https://github.com/nbtech-prox/LinuxExplode/actions/workflows/ci.yml/badge.svg)](https://github.com/nbtech-prox/LinuxExplode/actions/workflows/ci.yml)

## Descarregar

Última versão: **[v0.2.0](https://github.com/nbtech-prox/LinuxExplode/releases/latest)**

| Ficheiro | Para quê |
|---|---|
| `linux-explode.flatpak` (30 MB) | Flatpak x86_64 — `flatpak install --user linux-explode.flatpak` |
| `linux-explode-aarch64.flatpak` (27 MB) | Flatpak ARM64 (precisa do runtime aarch64) |
| `linux-explode-0.1.0-linux-arm64.tar.gz` (38 MB) | ARM64 com `install.sh` sem root (`~/.local`) |
| `SHA256SUMS` | `sha256sum -c SHA256SUMS` |

## O que faz

| Módulo | O que mostra | Fonte |
|---|---|---|
| **Sistema** | distribuição, kernel, arquitetura, máquina, placa-mãe, BIOS, CPU (núcleos/threads/frequências/cache L3), RAM e módulos (fabricante, part number, velocidade), GPUs | `/etc/os-release`, `/proc/cpuinfo`, `/proc/meminfo`, `/sys/class/dmi/id`, `/sys/class/drm`, `/sys/bus/nvmem` (SPD) |
| **Sensores** | temperatura, ventoinhas, potência, tensão, corrente e frequência, ao vivo (1 s), com filtro por tipo — no filtro **Frequência** os núcleos da CPU vêm primeiro e a GPU depois; no filtro **Potência** abre com o cartão **Consumo do PC** (RAPL + GPUs) antes da lista de hwmon | `/sys/class/hwmon`, `/sys/devices/system/cpu`, `/sys/class/powercap` (RAPL) |

| **Discos** | modelo, tamanho, SSD/HDD, partições com ocupação, temperatura, saúde S.M.A.R.T. | `/sys/block`, `/proc/mounts`, `smartctl` |
| **EXPLODE BENCH** | nota de CPU (path tracer determinístico 960×540, 1024 spp), nota de GPU (o mesmo path tracer em compute shader Vulkan 1280×720), banda e latência de RAM, disco sequencial e 4K | CPU do próprio processo, GPU via Vulkan + SPIR-V embutido, `O_DIRECT` |
| **Rede** | latência TCP (router e internet), jitter, perda, DNS, download/upload | sockets + Cloudflare |
| **Relatório** | exporta tudo em HTML (com a marca EXPLODE) ou JSON | — |

### Frequência — na aba Frequência de Sensores (núcleos primeiro, GPU depois)

A frequência individual por núcleo vive **só** na página **Sensores**, no botão **Frequência** —
não há painel de frequência na página Sistema. A ordem é fixa:

1. **CPU — frequência por núcleo** (primeiro cartão): uma linha por núcleo físico, com os valores
   de cada thread (`Núcleo 0 → T0 5.50 GHz · T8 0.61 GHz`) e uma nota com núcleos/threads,
   driver/governador e mín/média/pico/turbo.
2. **GPU** (depois): os sensores de frequência que o hwmon expõe por placa — `sclk` no núcleo e
   `mclk` na memória, com o endereço PCI no título.

A página actualiza a cada segundo enquanto está visível. O `Sensores` esconde leituras a zero
(clock gating), por isso uma GPU em repouso pode aparecer só com `mclk`; para ver núcleo e
memória em simultâneo, com máximos, usar `LinuxExplode --cli freq` ou o relatório.

Fontes da CPU, por ordem: `cpufreq/scaling_cur_freq` → `cpuinfo_cur_freq` → `cpuinfo_avg_freq` →
`cpu MHz` de `/proc/cpuinfo`. Limites de `cpuinfo_min_freq`/`cpuinfo_max_freq`. O núcleo físico
vem de `topology/core_id` + `physical_package_id` (com `/proc/cpuinfo` como alternativa).

Fontes da GPU: `amdgpu` publica `hwmon/freq1_input` (sclk) e `freq2_input` (mclk) em Hz e as
tabelas `pp_dpm_sclk`/`pp_dpm_mclk` (a linha com `*` é o nível activo). Na CLI e no relatório os
máximos saem da tabela `pp_dpm` (ex.: 2855 MHz no núcleo, 1000 MHz na memória da RX 6700 XT) e
o valor em repouso é dito como tal. Intel usa `gt_cur_freq_mhz`/`gt_max_freq_mhz`. A NVIDIA não
publica relógios em sysfs — nesse caso diz-se que não há leitura, não se inventa.

Sem nenhuma destas fontes — comum em ARM sem driver de escala e em VMs — a secção apresenta
"—" e diz porquê, em vez de inventar valores.

### Consumo medido (CPU + GPUs) — na aba Potência de Sensores

O botão **Potência** da página Sensores abre com o cartão **Consumo medido — CPU (socket) e
GPUs**, actualizado a cada segundo, antes da lista de sensores que o hwmon expõe. Mede o que os
sensores conseguem medir — o socket da CPU e as GPUs — e diz na nota que **não** é o consumo da
tomada:

```
CONSUMO MEDIDO — CPU (SOCKET) E GPUS
CPU — socket (RAPL)                            23.5 W
CPU — núcleos (RAPL)                            1.7 W
GPU 03:00.0 · AMD                              11.0 W
GPU 7b:00.0 · AMD (integrada, dentro do pacote)  26 mW
Total medido                           ≈ 34.5 W   (CPU + GPU dedicada)
Energia desde o arranque                    0.009 kWh
```

**De onde vem cada número**

- **Pacote e núcleos da CPU** — domínios `package-N` e `core` do RAPL em
  `/sys/class/powercap/intel-rapl*/`. O ficheiro `energy_uj` é um contador **cumulativo** em
  microjoules; a potência sai da diferença entre duas leituras (`µJ/ns × 1000 = W`) e passa
  por uma média móvel (α = 0,4) porque o valor instantâneo oscila muito. Quando existe o
  domínio `psys` (plataforma completa), é ele que dá o total em vez da soma.
- **Contadores de 32 bits** — ao dar a volta em `max_energy_range_uj` (65,5 kJ nesta máquina,
  ≈ 45 min a 24 W) o delta ficaria negativo; nesse caso soma-se o intervalo máximo em vez de
  mostrar um pico falso.
- **GPUs** — `power1_average` (ou `power1_input`) de cada `amdgpu`/`i915`/`xe`/`nouveau` em
  `/sys/class/hwmon`, em microwatts. O endereço PCI vem do caminho real do dispositivo.
- **GPU integrada vs dedicada** — a integrada pendura directamente num bridge da barra 0 da CPU
  (aqui `7b:00.0` atrás de `00:08.1`), enquanto uma dedicada fica atrás de bridges próprios
  (`03:00.0` atrás de `02:00.0`). A potência da integrada **já está dentro do pacote** do RAPL,
  por isso a linha é marcada como tal e o total não a soma outra vez.
- **Energia desde o arranque** — o contador absoluto do pacote convertido em kWh (3,6×10¹² µJ
  por kWh), não um integral calculado pela app.

**Porque é preciso um passo de root (uma vez)**

Desde a CVE-2020-8694 o kernel publica `energy_uj` com modo `0400 root` — os contadores de
energia servem de canal lateral. Sem permissão a app mostra a potência das GPUs, escreve o
motivo e o comando a correr; não inventa valores.

```bash
sudo ./permitir-energia.sh            # instala a unidade e aplica já
sudo ./permitir-energia.sh --estado   # mostra permissões e leitura actual
sudo ./permitir-energia.sh --remover  # desfaz (volta a 0400)
```

O script instala `/etc/systemd/system/linuxexplode-rapl.service` (oneshot,
`RemainAfterExit=yes`, activa em `multi-user.target`), que em cada arranque faz `chmod 0444` nos
`energy_uj` existentes. No Flatpak **não é preciso mais nada**: o `/sys` do anfitrião já está
montado só-de-leitura dentro da sandbox e o `--filesystem` do manifesto não é preciso mexer.

**O que este número não é**

Não é o consumo da tomada. Esta placa (NUVOTON NCT6687D via `nct6683`) expõe tensões, ventoinhas
e temperaturas, mas **nenhum canal de potência total**, e nenhuma fonte ATX reporta watts por
software. O total é, portanto, `pacote da CPU + GPUs dedicadas` e deixa de fora placa-mãe,
discos, ventoinhas, controladores e as perdas da fonte — na prática o consumo na tomada é mais
alto. O cartão diz isto na nota em rodapé, para o número não ser lido como o total da máquina.

**Diagnóstico**

```bash
LinuxExplode --cli potencia     # mesmo cálculo, sem abrir janela
LINUXEXPLODE_POWERCAP=<dir> LinuxExplode --cli potencia   # lê contadores falsos (testes)
```

### Memória — módulos individuais, sem root

O cartão **Memória** lista cada módulo com capacidade, geração, velocidade, fabricante, part
number e data de fabrico. Três fontes, por ordem:

1. **SPD do módulo** — DDR5 via `spd5118`
   (`/sys/bus/nvmem/devices/<bus>-<addr>/nvmem`, 1024 bytes, JESD400-5); DDR4 via `ee1004`
   (`/sys/bus/i2c/devices/<bus>-005x/eeprom` ou o mesmo nvmem, 512 bytes, JESD21-C Annex L).
   A velocidade sai de tCK (`scaling` em ns→MT/s); se houver XMP, mostra a velocidade do perfil
   e a JEDEC base entre parênteses quando diferem.
2. **SMBIOS** — `/sys/firmware/dmi/entries/17-*/raw` (tipo 17). É 0400 root em muitos kernels,
   mas há distribuições onde é legível; serve de alternativa e ainda traz o *slot* (DIMM_A1…).
3. Nada legível → a app escreve que não há dados, em vez de estimar.

A capacidade não vem de `/proc/meminfo` (esse desconta o que o firmware reserva): é calculada da
densidade por die, largura do bus e ranks do próprio SPD, e batida com o total do sistema.

Diagnóstico noutra máquina: `LINUXEXPLODE_MEMORIA_TESTE=<ficheiro|diretório>` descodifica dumps
(SPD em bruto ou estruturas SMBIOS guardadas como `17-N.raw`) em vez de ler `/sys`. Útil para
confirmar um portátil sem root e para testar DDR4 sem ter DDR4 à mão.

### Super I/O da placa-mãe (ventoinhas e tensões)

Os sensores da placa (ventoinhas, tensões, temperaturas dos termistores) só aparecem se o
Super I/O tiver driver carregado. Nesta máquina (MSI MAG X870E TOMAHAWK) o chip é um
**Nuvoton NCT6687D**, que é do driver `nct6683` — não do `nct6775`:

```
sudo modprobe nct6683 force=1          # o ID desta placa não está na tabela do driver
```

Para ficar permanente (já instalado nesta máquina):

```
echo nct6683 | sudo tee /etc/modules-load.d/nct6683.conf
printf 'options nct6683 force=1\n' | sudo tee /etc/modprobe.d/nct6683.conf
```

Sem isto sobram 14 temperaturas e 1 ventoinha (a do GPU); com isto passam a 44 leituras,
incluindo 6 temperaturas da placa, as ventoinhas do CPU/bomba/caixa e 14 tensões.

### Linha de comandos

Útil para scripts e servidores (não abre janela):

```bash
LinuxExplode --cli info                    # resumo do sistema
LinuxExplode --cli freq                    # frequência por núcleo/thread e relógios da GPU
LinuxExplode --cli sensores                # todos os sensores hwmon
LinuxExplode --cli potencia                # consumo do PC: CPU (RAPL) + GPUs, total estimado
LinuxExplode --cli fps [ficheiro]          # sessão do MangoHud: FPS médio, 1% e 0,1% low
LinuxExplode --cli discos                  # discos, partições e SMART
LinuxExplode --cli rede                    # teste de rede (~15 s)
LinuxExplode --cli bench multi ram disco   # benchmark de CPU + RAM + disco
LinuxExplode --cli bench single            # uma thread
LinuxExplode --cli bench gpu               # path tracer Vulkan (GPU dedicada)
LinuxExplode --cli bench gpu 1             # escolhe a GPU pelo índice listado
LinuxExplode --cli bench rt                # path tracer com ray tracing por hardware
LinuxExplode --cli bench rt 2              # ray tracing na GPU integrada
LinuxExplode --cli relatorio html          # grava o relatório no home
```

## O que mudou em relação à versão Windows

O original era WPF (`net8.0-windows`, 13.971 linhas de C# + 3.641 de XAML). WPF não existe
em Linux, por isso a camada de interface foi reescrita e cada subsistema Windows substituído
pelo equivalente do kernel.

| Original (Windows) | Aqui (Linux) | Estado |
|---|---|---|
| WPF + XAML | Avalonia 11 + XAML/C# | reescrito |
| WMI (`System.Management`) | `/sys`, `/proc`, DMI | substituído |
| LibreHardwareMonitorLib | `hwmon` (k10temp, amdgpu, nvme, nct6775…) | substituído |
| NVML (NVIDIA) | `amdgpu` sysfs (`gpu_busy_percent`, `mem_info_vram_*`, hwmon) | substituído (AMD) |
| DXGI / Direct3D 11 / D3D12 + DXR | Vulkan compute (`Core/BenchGpu.cs`) e `VK_KHR_ray_query` (`Core/BenchRt.cs`) | substituído |
| ETW / PresentMon (FPS de jogos) | CSV do MangoHud lidos por `Core/FpsMangohud.cs` (`--cli fps`) — leitura feita, UI por fazer | parcial |
| `FILE_FLAG_NO_BUFFERING` | `O_DIRECT` via `open(2)` | substituído |
| Registo do Windows (tweaks, arranque) | — | não existe |
| `winget` / instalar apps | — | não existe |
| Instalação de drivers `.inf`/`.sys`, portas COM | — | não existe |
| iCUE SDK (Corsair) | — (no original já havia ponte OpenRGB) | não existe |

Resumo honesto: **60 % do código original era portável, 24 % reescrevível, 14 % impossível**
(as funcionalidades que só fazem sentido em Windows). O que ficou de fora está identificado
acima — não há funcionalidades "fingidas".

O que continua em falta face ao original está avaliado item a item (meio, esforço e riscos) em
[`docs/portar-em-falta.md`](docs/portar-em-falta.md).

## FPS por aplicação — cartão do MangoHud

No fim da página **EXPLODE BENCH** há um cartão que lê as sessões que o
[MangoHud](https://github.com/flightlessmango/MangoHud) grava em CSV. Em Wayland o compositor não
expõe os frames de outras aplicações, por isso esta é a única forma honesta de ter FPS de jogos —
e a app diz isso em vez de inventar números.

```bash
sudo pacman -S mangohud          # ou o equivalente da tua distro
mkdir -p ~/mangologs             # a pasta tem de existir antes de abrir a app

# Steam: opções de lançamento do jogo
MANGOHUD=1 %command%
```

Cada sessão grava um CSV em `~/mangologs` (a config em `~/.config/MangoHud/MangoHud.conf` define
`output_folder`, `autostart_log=15` e `log_duration=0`). O cartão mostra **FPS médio**, **1% low**,
**0,1% low** e **frametime médio**, com uma sparkline dos frametimes, e passa sozinho para a
sessão mais recente quando fechas o jogo. Na CLI: `LinuxExplode --cli fps [ficheiro]`.

O Flatpak expõe `~/mangologs`, `~/.config/MangoHud` e `~/.local/share/MangoHud` **só de leitura**
para a sandbox — sem isso o cartão não veria os registos.

## Benchmark de GPU (Vulkan)

A GPU é medida a sério, não por um número sintético: o mesmo path tracer do benchmark de CPU
foi escrito em GLSL (`Assets/Shaders/path.comp`), compilado para SPIR-V com `glslangValidator`
e executado num **compute shader Vulkan** com timestamps de CPU à volta do fence.

- 1280×720, 64 amostras por pixel, 3 salto(s) de profundidade, cena idêntica à da CPU
- o dispatch repete-se até 1,5 s para a nota não depender do ruído de medição
- nota = amostras totais ÷ segundos ÷ 1000 — sobe com a GPU, não com o CPU
- funcionam as duas GPUs: dedicada e integrada (selector na página; `--cli bench gpu <índice>`)
- os dispositivos de software (llvmpipe) são filtrados: medir GPU em CPU não diz nada

Valores medidos nesta máquina (Ryzen 7 9700X, RX 6700M Navi 22):

| modo | GPU | passagens em 1,5 s | nota |
|---|---|---|---|
| compute | Radeon RX 6700M (RADV NAVI22) | 331 | ~13 000 000 |
| compute | Radeon 610M integrada (RAPHAEL) | 15 | ~585 000 |
| ray tracing | Radeon RX 6700M | 105 | ~4 100 000 |
| ray tracing | Radeon 610M integrada | 5 | ~186 000 |

### Modo ray tracing (VK_KHR_ray_query)

O segundo botão do cartão GPU corre o mesmo path tracer com a travessia do BVH feita pelas
unidades de RT: BLAS com 64 esferas em geometria de triângulos (icosesferas de 320 triângulos
cada, 20 480 no total), uma TLAS com uma instância, e `rayQueryConfirmIntersectionEXT` no shader.
A nota RT não é comparável com a de compute porque a cena é maior (64 esferas contra 6) — é
comparável com outra máquina a correr o mesmo modo.

Se a GPU não expuser `VK_KHR_ray_query`, o botão responde "indisponível" em vez de inventar
um número. No Flatpak funciona igual: o runtime traz Mesa/RADV e basta `--device=dri`.

O render produzido é mostrado na própria página (a mesma imagem que o shader escreveu),
o que serve de prova visual de que o trabalho foi mesmo feito na GPU.

Requisitos: driver Vulkan (`vulkan-radeon` no Arch/CachyOS). Sem ele a app diz que não
encontrou GPU em vez de inventar uma nota.

## Portabilidade (outro Linux, outro hardware)

O binário é **self-contained**: leva o runtime .NET 8 dentro, não precisa de nada instalado
além do driver de vídeo. Tudo o resto é lido de `/proc`, `/sys` e DMI, sem root.
Há dois alvos publicados: **linux-x64** (`linux-explode.flatpak`) e **linux-arm64**
(`linux-explode-aarch64.flatpak` + `linux-explode-0.1.0-linux-arm64.tar.gz`).

| Situação | O que acontece |
|---|---|
| Outra distro (Debian, Fedora, openSUSE, NixOS) | funciona; não há nada específico do Arch |
| GPU AMD, Intel ou NVIDIA | Vulkan é genérico; a lista de GPUs é enumerada em runtime |
| GPU sem `VK_KHR_ray_query` (Polaris, GTX 10xx) | o botão de ray tracing responde "indisponível" em vez de inventar nota |
| Sem driver Vulkan (VM, servidor) | "nenhuma GPU Vulkan com fila de computação" |
| Sem `smartctl` | discos continuam a aparecer; saúde fica "—" com o comando de instalação |
| `smartctl` sem regra sudoers | mostra o comando com o **caminho real** (`/usr/bin` ou `/usr/sbin` conforme a distro) |
| Sistema de ficheiros sem `O_DIRECT` (ZFS, tmpfs) | cai para escrita `WriteThrough` e avisa |
| Sem rede | a página de rede abre com os alvos em falha |
| Locale com vírgula decimal | o JSON do relatório continua válido (testado com `de_DE.UTF-8`) |
| Fontes em falta | Inter vai embutida; mono cai para DejaVu/Cascadia/monospace |

O que **não** é portável:

- **arquitetura**: há build para x86_64 e para aarch64, mas são binários distintos — um bundle
  x86_64 não instala em ARM. Para ARM usa `linux-explode-aarch64.flatpak` ou o `.tar.gz`
  (ver secção ARM64).
- **S.M.A.R.T.**: precisa de root em qualquer máquina; a regra sudoers é por máquina.
- **Super I/O da placa**: depende do chip (nesta é `nct6683`, noutra pode ser `it87`, `nct6775`,
  `f71882fg` ou não existir, como nos portáteis). A app mostra o que o kernel expuser — não carrega módulos.

## Integração contínua

`.github/workflows/ci.yml` corre em cada push para `main`, em pull requests e a pedido
(`workflow_dispatch`):

| Job | O que faz |
|---|---|
| **Compilar e testar a CLI** | `dotnet restore` + `dotnet build -c Release`; corre o modo `--cli` (`info`, `sensores`, `potencia`, `freq`) no runner — o `--cli` sai antes de inicializar o Avalonia, por isso não precisa de ecrã; publica o binário x64 self-contained e anexa-o como artefacto de 7 dias |
| **Publicar binário ARM64** | cross-publish `-r linux-arm64` a partir do anfitrião x64, sem emulação (só compila, não executa) |

O build é feito sem `TreatWarningsAsErrors` porque os avisos CS0618/CS9193 do Silk.NET em
`Core/BenchRt.cs` são conhecidos. Num runner sem `intel_rapl_msr` o cartão de potência mostra a
nota de "sem RAPL" — o CI serve também para provar que a app degrada em vez de falhar.

O `.github/workflows/release.yml` trata dos artefactos de publicação. Corre em push de tag `v*`
e, à mão (`workflow_dispatch` com a tag), para reconstruir os assets de um release existente:

| Job | O que faz |
|---|---|
| **preparar** | cria o release da tag, se ainda não existir |
| **Flatpak x86_64** | instala `flatpak` + `org.flatpak.Builder` + Platform/SDK 25.08 e corre `./empacotar.sh --bundle` |
| **ARM64** | corre `./empacotar.sh --arm64` num runner limpo (cross-publish aarch64, sem emulação) |
| **SHA256SUMS** | descarrega os assets já publicados, gera o `SHA256SUMS` e volta a anexá-lo |

Os dois builds correm em jobs separados de propósito: na mesma directoria, depois do
`flatpak-builder`, o glob `**/*.axaml` do Avalonia apanhava `build-dir/` e o publish arm64 falhava
com *Could not find file …/\*\*/\*.axaml*. O `.csproj` exclui esses artefactos via
`DefaultItemExcludes`, o que também acelera o build local a seguir a um `empacotar.sh`.

## Compilar e correr

Requisitos: .NET SDK 8 (`dotnet --version` ≥ 8.0) e, para o Flatpak, `flatpak` + `org.flatpak.Builder`.

```bash
# desenvolvimento
dotnet build -c Release
./bin/Release/net8.0/LinuxExplode

# executável autónomo (não precisa de .NET instalado)
dotnet publish LinuxExplode.csproj -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o dist
./dist/LinuxExplode
```

## Flatpak

```bash
flatpak install --user -y flathub org.flatpak.Builder org.freedesktop.Sdk//25.08 \
  org.freedesktop.Sdk.Extension.dotnet8//25.08

flatpak run org.flatpak.Builder --user --install --force-clean build-dir \
  flatpak/pt.linuxexplode.LinuxExplode.json

flatpak run pt.linuxexplode.LinuxExplode
```

Para gerar um ficheiro instalável noutra máquina:

```bash
flatpak run org.flatpak.Builder --force-clean build-dir flatpak/pt.linuxexplode.LinuxExplode.json
flatpak build-bundle ~/.local/share/flatpak/repo linux-explode.flatpak pt.linuxexplode.LinuxExplode
flatpak install --user linux-explode.flatpak
```

## ARM64 (aarch64)

`./empacotar.sh --arm64` gera tudo sem emulação e sem `flatpak-builder`:

```bash
./empacotar.sh --arm64
#   linux-explode-0.1.0-linux-arm64.tar.gz   38 MB  instalador sem root
#   linux-explode-aarch64.flatpak            27 MB  precisa do runtime aarch64
```

No destino ARM, a via mais simples (não precisa de Flatpak nem de root):

```bash
tar -xzf linux-explode-0.1.0-linux-arm64.tar.gz
cd linux-explode-0.1.0-linux-arm64 && ./install.sh
# ~/.local/share/linux-explode + ~/.local/bin/linux-explode + entrada de menu
# outro prefixo:  PREFIXO=/usr/local sudo ./install.sh
```

Ou, com Flatpak instalado (o runtime vem do Flathub):

```bash
flatpak install --user flathub org.freedesktop.Platform/aarch64/25.08
flatpak install --user ./linux-explode-aarch64.flatpak
flatpak run pt.linuxexplode.LinuxExplode
```

Como o bundle é construído sem correr nada em ARM: o build dir é montado à mão
(`files/bin/linuxexplode`, `.desktop`, metainfo, ícone) e tem de ser finalizado com
`flatpak build-finish` — que **não** tem `--arch`; a arquitectura vem do runtime escrito no
`metadata` (`org.freedesktop.Platform/aarch64/25.08`). Depois
`flatpak build-export --arch=aarch64` e `flatpak build-bundle --arch=aarch64`.
O bundle foi validado importando-o de volta para um repo `ostree`:
`app/pt.linuxexplode.LinuxExplode/aarch64/stable`, 222 ficheiros, `LinuxExplode` e as `.so` nativas
confirmadas como `ELF ARM aarch64`.

**Não executado em hardware ARM.** O que está provado: o payload é ARM válido, o bundle e o
`.tar.gz` são bem formados e o instalador cria a árvore correcta numa instalação isolada.
O que não está: arranque real em Raspberry Pi/Asahi/Snapdragon — em especial Vulkan, que na
maioria dessas máquinas não tem fila de computação (a página de benchmark diz "indisponível",
a benchmark de CPU e disco funciona).

## Estrutura

```
LinuxExplode/
├── Core/                 # tudo o que é lógica, sem UI
│   ├── Sistema.cs        # CPU, RAM, DMI, GPUs
│   ├── MemoriaModulos.cs # módulos de RAM: SPD DDR5/DDR4 e SMBIOS, sem root
│   ├── Sensores.cs       # hwmon
│   ├── Potencia.cs       # potência da CPU (contadores RAPL) + power1_* das GPUs
│   ├── Discos.cs         # /sys/block + smartctl
│   ├── BenchCpu.cs       # path tracer (portado do original, inalterado)
│   ├── BenchDisco.cs     # RAM + disco com O_DIRECT
│   ├── Rede.cs           # latência, DNS, velocidade
│   ├── Relatorio.cs      # HTML + JSON
│   ├── Cli.cs            # modo sem interface
│   └── Util.cs
├── Vistas/               # UI Avalonia
│   ├── MainWindow.axaml  # barra lateral + área de conteúdo
│   ├── Pagina*.cs        # uma classe por página
│   └── UI.cs             # cartões, linhas, métricas
├── Tema/Tema.axaml       # paleta e estilos (monocromático, blocos)
├── flatpak/              # manifest, .desktop, metainfo
├── permitir-energia.sh   # (root, uma vez) torna os contadores RAPL legíveis
└── packaging/arm64/      # install.sh, uninstall.sh e LEIAME do pacote ARM64
```

## Limitações conhecidas

- **S.M.A.R.T. detalhado** precisa de `smartmontools` e root. Dentro do Flatpak o acesso a
  `/dev` é limitado: a app diz isso na própria página, em vez de inventar um veredito.
- **Benchmarks de GPU** do original (stress test D3D11, ray tracing D3D12) foram substituídos por
  equivalentes em Vulkan (`Core/BenchGpu.cs`, `Core/BenchRt.cs`); o modo ray tracing exige
  `VK_KHR_ray_query` no driver.
- **Overlay de FPS** não existe como overlay global: em Wayland nenhuma app vê os frames de outra.
  A via suportada é o MangoHud (layer Vulkan/OpenGL); a app já lê e resume os CSV dele (`--cli fps`),
  faltando a interface e o lançador.
- **Memória por módulo**: lida do SPD do próprio módulo, sem root. DDR5 usa o `spd5118` (expõe
  `/sys/bus/nvmem/devices/<bus>-<addr>/nvmem`, legível por todos); DDR4 usa o `ee1004`
  (`/sys/bus/i2c/devices/<bus>-005x/eeprom` ou o mesmo nvmem). Sem SPD, tenta o SMBIOS
  (`/sys/firmware/dmi/entries/17-*/raw`, legível em várias distribuições mas 0400 root noutras).
  Se nenhuma fonte existir, a app diz isso em vez de aproximar.
- Em portáteis, alguns sensores só aparecem depois de carregar o módulo do kernel
  (ex.: `sudo modprobe nct6775`).
- **Potência total da máquina** não existe por software: o RAPL só cobre o socket da CPU e
  nenhuma fonte ATX publica watts. O total do cartão é `pacote da CPU + GPUs dedicadas`; o
  RAPL exige ainda `sudo ./permitir-energia.sh` uma vez (o kernel expõe `energy_uj` como 0400).

## Licença

MIT — como o projeto original.

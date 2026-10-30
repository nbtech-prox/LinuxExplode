# LINUX EXPLODE

Reescrita da app original de Windows em **Avalonia 11 + .NET 10**, para correr nativamente
em Linux (e empacotada em Flatpak). Toda a informação de hardware é lida diretamente de
`/proc`, `/sys` e DMI — sem WMI, sem registo, sem root para as funcionalidades principais.

![estado](https://img.shields.io/badge/estado-funcional-9BE8A8)
[![CI](https://github.com/nbtech-prox/LinuxExplode/actions/workflows/ci.yml/badge.svg)](https://github.com/nbtech-prox/LinuxExplode/actions/workflows/ci.yml)

O plano de evolução (o que vem a seguir, por versão) está em **[ROADMAP.md](ROADMAP.md)**.

## Descarregar e actualizar

Última versão: **[v0.20.15](https://github.com/nbtech-prox/LinuxExplode/releases/latest)** — interface em
português, espanhol e inglês.

### Pelo gestor de aplicações (recomendado)

O projecto publica um **repositório Flatpak** em `https://nbtech-prox.github.io/LinuxExplode/`.
Acrescenta-se uma vez e a partir daí as actualizações aparecem no gestor de aplicações:

```bash
curl -fsSLO https://nbtech-prox.github.io/LinuxExplode/linux-explode-release.gpg
flatpak remote-add --user --gpg-import=linux-explode-release.gpg \
  linuxexplode https://nbtech-prox.github.io/LinuxExplode/
flatpak install --user linuxexplode pt.linuxexplode.LinuxExplode
flatpak update --user
```

O repositório é **assinado com GPG**: o `summary` e os commits levam assinatura e o Flatpak recusa
um update com assinatura inválida. A chave de release é
`Linux Explode Releases <nbtech-prox@users.noreply.github.com>`, ed25519,
fingerprint `81F5CFA1CE36B58DD8C4308424CD0B309D2F80AC`; a pública está em
`docs/linux-explode-release.gpg` (e no próprio gh-pages).

**Quem instalou antes da 0.20.13** (com `--no-gpg-verify`) passa a verificar sem reinstalar a app:

```bash
curl -fsSLO https://nbtech-prox.github.io/LinuxExplode/linux-explode-release.gpg
flatpak remote-modify --user --gpg-import=linux-explode-release.gpg linuxexplode
flatpak remote-modify --user --gpg-verify linuxexplode
flatpak update --user
```

A chave privada vive nos segredos do repositório (`FLATPAK_GPG_PRIVATE_KEY`) e numa cópia local em
`~/.config/linux-explode/` (chmod 600), ao lado do certificado de revogação. Se for preciso rodar a
chave, publica-se a nova em `docs/` com outro nome — a antiga fica no repositório para os clientes
que ainda a tenham — e repete-se o pedido de importação.

### Pelos ficheiros

| Ficheiro | Para quê |
|---|---|
| `linux-explode.flatpak` (33 MB) | Flatpak x86_64 — `flatpak install --user linux-explode.flatpak` |
| `linux-explode-aarch64.flatpak` (29 MB) | Flatpak ARM64 (precisa do runtime aarch64) |
| `SHA256SUMS` | `sha256sum -c SHA256SUMS` |

A distribuição é **só Flatpak**: não há instalador solto nem `.tar.gz`. O runtime traz glibc, ICU e o
loader Vulkan, por isso o mesmo pacote corre em qualquer distro — em x86_64 e em aarch64.

Um bundle instalado deste modo fica ligado a uma origem local **sem URL** — o gestor de aplicações
chega a anunciar uma actualização que falha. Para actualizar por aí, usa o repositório acima.

As versões sobem de **2 em 2** no último número: 0.20.1 → 0.20.3 → 0.20.5 …

## O que faz

| Módulo | O que mostra | Fonte |
|---|---|---|
| **Sistema** | distribuição, kernel, arquitetura, máquina, placa-mãe, BIOS, CPU (núcleos/threads/frequências/cache L3), RAM e módulos (fabricante, part number, velocidade), GPUs e **teste de suspender/retomar** | `/etc/os-release`, `/proc/cpuinfo`, `/proc/meminfo`, `/sys/class/dmi/id`, `/sys/class/drm`, `/sys/bus/nvmem` (SPD) |
| **Sensores** | temperatura, ventoinhas, potência, tensão, corrente e frequência, ao vivo (1 s), com filtro por tipo — no filtro **Frequência** os núcleos da CPU vêm primeiro e a GPU depois; no filtro **Potência** abre com o cartão **Consumo do PC** (RAPL + GPUs) antes da lista de hwmon | `/sys/class/hwmon`, `/sys/devices/system/cpu`, `/sys/class/powercap` (RAPL) |
| **Discos** | modelo, tamanho, SSD/HDD, partições com ocupação, temperatura, saúde S.M.A.R.T. e **auto-teste curto/longo com progresso e histórico** | `/sys/block`, `/proc/mounts`, `smartctl`, `smartctl -t` |
| **Benchmark** | nota de CPU (path tracer determinístico 960×540, 1024 spp), nota de GPU (o mesmo path tracer em compute shader Vulkan 1280×720), banda e latência de RAM, **curva de latência por tamanho de bloco**, **tarefa real (descomprimir + compilar)**, **ranking local com mediana de 3 corridas**, disco sequencial e 4K | CPU do próprio processo, GPU via Vulkan + SPIR-V embutido, `O_DIRECT`, `gzip` do .NET e `cc` do sistema |
| **Dispositivos** | links PCI Express (geração, largura em uso vs máximo, débito útil teórico), USB (o que está ligado, velocidade negociada, classes, driver, potência) e áudio (placas, fluxos PCM, latência de período/buffer e xruns do PipeWire) | `/sys/bus/pci/devices`, `/sys/bus/usb/devices`, `/proc/asound`, `pw-top`, `pw-metadata` |
| **Rede** | latência TCP (router e internet), jitter, perda, DNS, download/upload e **débito real cliente/servidor** entre dois PC com a aplicação | sockets + Cloudflare; TCP próprio no débito real |
| **Relatório** | um HTML autónomo com sistema, sensores, discos, ranking de benchmarks, último stress/ciclos/VRAM e a última sessão gravada (com gráficos), ou JSON para automação | `/sys`, `/proc`, DMI e as medições guardadas |

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

### Alertas

No topo da aba Sensores, o cartão **Alertas** diz o que está fora dos limites — e não só quando alguém
está a olhar:

| Regra | Dispara quando | Limite (editável) |
|---|---|---|
| CPU quente | temperatura do pacote ≥ limite | 90 °C |
| GPU quente | temperatura da GPU ≥ limite | 90 °C |
| Ventoinhas a zero | todas as ventoinhas a 0 RPM com a CPU a ≥ 65 °C | — |
| Pouco espaço | partição real (ext4/xfs/btrfs/…) com menos de X GB livres | 10 GB |

As notificações do ambiente (`notify-send`) são opcionais e têm um aviso por regra a cada 15 minutos.
Dentro do Flatpak o `notify-send` normalmente não existe: o cartão desactiva a opção e di-lo, em vez de
falhar em silêncio.

Os limites ficam em `~/.config/linux-explode/config.json` e há o mesmo resultado na linha de comandos:

```bash
LinuxExplode --cli alertas
LinuxExplode --cli alertas --json
```

### Ao vivo, throttling e energia

No topo da aba **Sensores** há três cartões que respondem às perguntas do dia-a-dia:

- **Ao vivo** — quatro gráficos de 60 s (temperatura da CPU, da GPU, potência e frequência média),
  com uma amostra por segundo. A linha da temperatura passa a vermelho a partir dos 90 °C.
- **Throttling** — frequência efectiva contra o máximo do processador (`scaling_cur_freq` vs
  `cpuinfo_max_freq`). Conta **episódios** de queda (frequência 15 % abaixo do máximo com o CPU
  ocupado), guarda o mínimo visto e mostra o tempo total em queda. Em AMD o firmware não expõe
  contadores de throttle e o cartão di-lo em vez de inventar um número.
- **Energia e custo** — energia da sessão (integrada a partir do RAPL), média em watts e projecção de
  custo a partir da tarifa que escreveres (€/kWh, guardada em `~/.config/linux-explode/config.json`).

O contador `energy_uj` desta plataforma dá a volta aos ~65,5 kJ (poucos minutos a 200 W), por isso a
energia é **integrada no tempo** pela aplicação e não copiada de uma leitura única — o rótulo diz
"nesta sessão" porque é isso que está medido.

### Linha de comandos

Útil para scripts e servidores (não abre janela):

```bash
LinuxExplode --cli info                    # resumo do sistema
LinuxExplode --cli freq                    # frequência por núcleo/thread e relógios da GPU
LinuxExplode --cli sensores                # todos os sensores hwmon
LinuxExplode --cli potencia                # consumo do PC: CPU (RAPL) + GPUs, total estimado
LinuxExplode --cli fps [ficheiro]          # sessão do MangoHud: FPS médio, 1% e 0,1% low
LinuxExplode --cli discos                  # discos, partições e SMART
LinuxExplode --cli placa                   # links PCIe + temperaturas de chipset/memória/rede
LinuxExplode --cli usb                     # dispositivos USB, velocidade negociada e drivers
LinuxExplode --cli audio                   # placas de som, fluxos PCM e xruns do PipeWire
LinuxExplode --cli rede                    # teste de rede (~15 s)
LinuxExplode --cli bench multi ram disco   # benchmark de CPU + RAM + disco
LinuxExplode --cli bench multi --repeticoes 3   # 3 corridas: mediana e variação
LinuxExplode --cli bench latmem            # latência de memória de 4 KB a 256 MB
LinuxExplode --cli bench real              # descomprimir 48 MB + compilar 1 500 funções C
LinuxExplode --cli ranking                 # mediana das últimas 3 corridas, por alvo
LinuxExplode --cli gravar 60               # grava uma sessão de 60 s em CSV
LinuxExplode --cli bench single            # uma thread
LinuxExplode --cli bench gpu               # path tracer Vulkan (GPU dedicada)
LinuxExplode --cli bench gpu 1             # escolhe a GPU pelo índice listado
LinuxExplode --cli bench rt                # path tracer com ray tracing por hardware
LinuxExplode --cli bench rt 2              # ray tracing na GPU integrada
LinuxExplode --cli relatorio html          # grava o relatório no home
```

Todos os comandos aceitam **`--json`** (ou `-j`), que devolve um envelope com `aplicacao`, `versao`,
`comando`, `quando` e `dados` — nada de texto solto no stdout:

```bash
LinuxExplode --cli potencia --json | jq .dados.TotalW
LinuxExplode --cli bench multi --json | jq .dados.resultado.Score
LinuxExplode --cli sensores --json | jq '[.dados[] | select(.Tipo=="Temperatura")] | length'
LinuxExplode --cli placa --json | jq '.dados.reduzidos'          # links abaixo do máximo anunciado
LinuxExplode --cli usb --json | jq '[.dados[] | select(.TemAudio)]'
LinuxExplode --cli audio --json | jq '.dados.Xruns'
LinuxExplode --cli stress cpu 60 --json > ~/stress.json
LinuxExplode --cli relatorio --json       # o relatório completo, em JSON
```

## Interface — tema, escala e atalhos

- **Tema claro/escuro** no botão da barra lateral; a escolha fica guardada em
  `~/.config/linux-explode/config.json`. As cores vivem em recursos (`DynamicResource`), por isso a
  troca é imediata, sem reiniciar. A cor de destaque do sistema (azul do Fluent) é substituída por um
  cinza da paleta para a interface continuar monocromática.
- **Escala da interface** no cursor da barra lateral (85 % a 135 %). Aplica-se ao conteúdo com
  `LayoutTransformControl`, por isso o texto e os gráficos escalam juntos e as barras de deslocamento
  continuam certas.
- **Atalhos**: `Ctrl+1`…`Ctrl+8` mudam de página (Sistema, Sensores, Discos, Bench, Stress, Rede,
  Dispositivos, Relatório), `Ctrl+=` / `Ctrl+-` mudam a escala, `Ctrl+0` repõe e `Ctrl+Q` fecha.
- **Idiomas**: o selector na barra lateral (abaixo do tema) muda entre **Português**, **Español** e
  **English** sem reiniciar. **O português é o idioma principal**: a aplicação abre em pt-PT mesmo
  com `LANG=en_US`; só muda quando escolhes outro idioma, e a escolha fica em
  `~/.config/linux-explode/config.json`. A cultura acompanha o idioma, por isso os números e as datas
  mudam com ele. `LINUXEXPLODE_IDIOMA=pt|es|en` força o idioma (capturas e testes).

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
| ETW / PresentMon (FPS de jogos) | CSV do MangoHud lidos por `Core/FpsMangohud.cs`, com cartão na aba Benchmark e `--cli fps` — falta só o lançador/overlay | substituído (leitura e cartão) |
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

## O pop do benchmark — animação, resolução e cores

`Testar GPU`, `Testar ray tracing`, `Multi-core` e `Single-core` abrem a cena num **pop-up** que a
renderiza ao vivo, com a câmara a orbitar (uma volta a cada ~18 s), e medem ao mesmo tempo. Não há
botões de prévia à parte: o teste é o pop.

- **GPU e ray tracing** — um seletor de resolução vai de **320×180 a 3840×2160 (4K)**. A medição
  mantém as 64 amostras por pixel do costume, repetidas até 1,5 s.
- **CPU** — a imagem constrói-se bloco a bloco, com uma thread por core (`Multi-core`) ou uma só
  (`Single-core`), e há uma barra por core com a ocupação real lida de `/proc/stat`.

### Qualidade da animação: orçamento de amostras, não spp fixas

A janela mostra o quadro a ~1000 px de largura, por isso o que conta é o número de **amostras por
fotograma** — e esse é constante (265 M no compute, 130 M no ray tracing). Daí saem as amostras por
pixel de cada resolução:

| Resolução | Amostras/pixel | Fotogramas/s medidos |
|---|---|---|
| 320×180 | 256 | ~195 |
| 640×360 | 256 | ~87 |
| 1280×720 (padrão) | 256 | ~29 |
| 1920×1080 | 128 | ~18 |
| 2560×1440 | 72 | ~13 |
| 3840×2160 (4K) | 32 | ~10 |
| Ray tracing 1920×1080 | 63 | ~15 |

Um número fixo de amostras por pixel fazia o contrário: 4K ficava com 4 spp (grão grosso) e 320×180
com 32 spp ampliadas 3× (grão ainda mais visível) — só o 1280×720 calhava bem, que era o ponto onde
a escala do ecrã e o orçamento coincidiam. A semente do ruído é fixa durante a animação, para o grão
não "ferver" de fotograma para fotograma.

### Histórico comparado — melhor, mediana e variação

No Flatpak esta pasta é persistida (`--persist=.local/share/linux-explode`) e vive em
`~/.var/app/pt.linuxexplode.LinuxExplode/.local/share/linux-explode` — não se perde ao fechar.

Cada nota fica guardada em `~/.local/share/linux-explode/historico.db` (SQLite) com a data e a
versão do programa — o `resultados.json` antigo é migrado uma vez (ver *Histórico em SQLite*).
Por baixo da nota, o cartão mostra o melhor resultado, a mediana e a variação da última
corrida em relação à mediana:

```
melhor 13 355 364 · mediana 13 090 807 · esta −1,2 % (mediana de 7 corrida(s))
```

Uma medição igual à última não é gravada (o histórico não engorda com repetições). O cartão
**Histórico de resultados** exporta tudo para CSV ou JSON e importa ficheiros de outra máquina — é
assim que se comparam dois PCs sem base de dados central.

### A nota e a resolução

A nota do GPU é **amostras por segundo ÷ 1000**: **8,59 M** a 320×180, **12,96 M** a 1280×720 e
**13,36 M** a 4K nesta máquina (RX 6700 XT). No extremo pequeno cai porque o custo de cada passagem
(dispatch e sincronização) pesa mais do que os 57 mil píxeis a desenhar; a partir de 720p estabiliza.
Na CPU a nota não depende da resolução (o teste é sempre 960×540).

Ganchos de teste (abrem o pop directamente, sem clicar):

```bash
LINUXEXPLODE_PAGINA=bench LINUXEXPLODE_PREVIA=gpu LINUXEXPLODE_RESOLUCAO=3840x2160 ./LinuxExplode
LINUXEXPLODE_PAGINA=bench LINUXEXPLODE_PREVIA=cpu LINUXEXPLODE_THREADS=1 ./LinuxExplode
```

## Benchmarks com mais sinal — ranking, latência, tarefa real e sessões

Quatro adições da 0.20.7, todas com a mesma regra: um número sozinho não é um resultado.

### Ranking local (mediana de 3 corridas)

Cada alvo do histórico passa a ter uma linha com a **mediana das últimas 3 corridas**, a **variação**
entre elas e a diferença da última face à mediana:

```
CPU · 16 threads
mediana 12 981 000 · últimas 3 variam 1,2 % · última +0,4 %
```

- A variação é a **amplitude relativa** (`(máx − mín) / máx`). Acima de **3 %** o cartão avisa que a
  máquina não estava estável e que a nota vale pouco — é diferente de "o teste correu mal".
- O aviso só existe a partir de 3 corridas: com duas não há como distinguir ruído de um problema.
- `--cli bench multi --repeticoes 3` corre o teste três vezes, mostra cada nota, e regista **só a
  mediana** no histórico (senão o ficheiro enchia-se de quase-repetições).
- `--cli ranking` (texto e `--json`) dá a mesma informação sem abrir a janela.

### Latência de memória por tamanho de bloco

4 KB a 256 MB, em nanossegundos por acesso, com *pointer chasing* sobre uma permutação de blocos de
64 B (uma linha de cache) com semente fixa:

| Conjunto de trabalho | Fase esperada | Medido nesta máquina |
|---|---|---|
| 4 KB – 16 KB | L1 | 1,1 ns |
| 64 KB – 1 MB | L2 | 2,9 ns (1 MB: 5,3–5,6 ns) |
| 4 MB – 16 MB | L3 | 9,0–9,4 ns |
| 64 MB – 256 MB | RAM | 77–79 ns (256 MB: 88–89 ns) |

Duas decisões que fazem a diferença entre medir latência e medir nada:

- **O percurso é aleatório.** Um acesso sequencial deixa o pré-buscador esconder a latência e o valor
  sai a banda, não o tempo de resposta.
- **16 M de acessos por tamanho, com aquecimento.** Com poucos acessos os tamanhos pequenos mediam
  ruído de arranque e a curva saía não-monótona (4 KB mais lento do que a RAM) — o teste
  `A_latencia_cresce_com_o_conjunto_de_trabalho` existe para isso não voltar a passar despercebido.

A curva é desenhada com eixo X logarítmico (`Vistas/CurvaLatencia.cs`), sem bibliotecas; `--cli bench
latmem` devolve os nove pontos em texto ou JSON.

### Tarefa do mundo real — descomprimir e compilar

Duas tarefas fixas, repetíveis e **sem rede**:

1. **Descomprimir 48 MB** de dados pseudo-aleatórios determinísticos e **verificar o SHA-256** do que
   saiu. Se o resultado não for igual ao original, o número é descartado em vez de reportado.
2. **Compilar um ficheiro C com 1 500 funções**, gerado na hora, entregue ao compilador por *stdin*
   (`cc -O2 -S -o /dev/stdout -xc -`) — sem ficheiros temporários e sem caminhos que o sandbox não vê.
   Dentro do Flatpak usa o compilador do anfitrião por `flatpak-spawn --host`.

Nesta máquina: descompressão **1 432 MB/s** dentro do Flatpak instalado e **515 MB/s** com o `zlib` do
anfitrião — a diferença é o binário que corre, e é por isso que o número aparece sempre ao lado do
compilador usado; compilação **3,69 s** (102 kB/s de fonte), 4,18 s no total.
Sem `cc`/`gcc`/`clang` o cartão diz "a tarefa de compilação fica de fora" em vez de inventar um tempo.

### Sessões de monitorização gravadas

O cartão *Sessão gravada* (aba Sensores) grava **uma amostra por segundo** — CPU, GPU, potência e
frequência média — em `~/.local/share/linux-explode/sessoes/sessao-AAAAMMDD-HHMMSS.csv`:

```
quando;cpu_c;gpu_c;watts;freq_mhz
2026-10-25 13:30:19;49.875;39;;2123.225
2026-10-25 13:30:20;48.75;40;19.621;1984.118
```

- **Escrita incremental**: fechar a aplicação a meio não perde o que já foi medido.
- **Célula vazia ≠ zero**: a primeira amostra de potência vem vazia porque o RAPL precisa de duas
  leituras. Um zero ali seria uma mentira.
- No fim aparecem o resumo (média, mínimo e máximo por série) e quatro gráficos da sessão inteira.
- `--cli gravar [segundos]` faz o mesmo sem interface, com `--json` para o resumo.

## Dispositivos — PCI Express, USB e áudio

A aba **Dispositivos** (`Ctrl+7`) responde a três perguntas que antes só se respondiam com `lspci`,
`lsusb` e uma ida ao terminal: *o link desta placa está a correr no máximo?*, *a que velocidade é que
este periférico negociou?* e *o áudio está a falhar em algum lado?* Tudo lido do kernel, sem root e
sem ferramentas externas — o que importa porque dentro do Flatpak não há `lspci` nem `lsusb`.

### PCI Express

Para cada dispositivo em `/sys/bus/pci/devices` a aplicação lê `current_link_speed`,
`current_link_width`, `max_link_speed` e `max_link_width` (o `LnkSta`/`LnkCap` do kernel):

| Coluna | De onde vem | Nota |
|---|---|---|
| Endereço | nome do directório (`0000:03:00.0` → `03:00.0`) | — |
| Link em uso | `current_link_speed` + `current_link_width` | `16.0 GT/s` = PCIe 4.0; `x16` = 16 lanes |
| Classe | `class` (0x03…, 0x01…, 0x0c…) | traduzida em pt-PT |
| Nome | `vendor`+`device` no `pci.ids` | prefixo do fabricante encurtado (`AMD/ATI`, `Phison`) |
| Débito útil | GT/s × lanes × codificação ÷ 8 | 8b/10b nas gerações 1–2, 128b/130b da 3 em diante |

Nesta máquina: GPU `03:00.0` a **PCIe 4.0 x16 ≈ 31,5 GB/s**, NVMe `04:00.0` a **PCIe 3.0 x4 ≈ 3,9 GB/s**,
NIC a **3.0 x1**, Wi-Fi a **3.0 x2**. Nenhum dispositivo final abaixo do máximo que anuncia.

Duas decisões que evitam mentira:

- **Pontes não são avaliadas.** Numa ponte (`class` 0x0604) o "actual" é o link que ela está a servir,
  não uma capacidade: compará-lo com o máximo dava 12 "reduções" falsas nesta placa. Ficam escondidas
  atrás da opção *Mostrar as pontes*.
- **O máximo é o do dispositivo**, não o do slot (é o que o kernel expõe). Quando um dispositivo final
  negocia menos do que anuncia, a linha é assinalada com o valor que ele suportava — a causa habitual
  é o slot/riser ou a poupança de energia do driver, e é isso que o texto diz.

### USB

Cada dispositivo em `/sys/bus/usb/devices/*/idVendor` aparece com fabricante, produto, ID, versão,
**velocidade negociada** (1,5 Mbps, 12, 480, 5 Gbps, 10 Gbps, 20 Gbps), classes das interfaces
(`áudio`, `vídeo`, `HID`, `hub`, `sem fios`…), driver (`snd-usb-audio`, `uvcvideo`, `usbhid`, `btusb`)
e a potência que pede (`bMaxPower`). A velocidade é a que o kernel reporta em `speed` — **não é um
teste de débito** e está dito no cartão. Sem nada legível (`/sys` não enumerável, contentor), o
comando imprime "sem dispositivos USB legíveis" e sai com 0: é uma resposta, não uma falha.

Dispositivos sem descritores de texto (acontece em dongles baratos) não ficam "Desconhecido": o nome é
deduzido do driver — `btusb` → *Adaptador Bluetooth*, `usbhid` → *Dispositivo HID*.

### Áudio

- **Placas** de `/proc/asound/cards` (índice, id, nome, driver e o barramento PCI/USB).
- **Fluxos PCM** de `/proc/asound/card*/pcm*/sub*/status` + `hw_params`: estado (`running`/`closed`),
  taxa, canais, formato, **período** e **buffer** convertidos em milissegundos. Com um PCM aberto, o
  cartão mostra por exemplo `Portal · pcm0p (USB Audio) — reprodução, 48000 Hz, 2 canais, S16_LE ·
  período 2,67 ms · buffer 170,7 ms`.
- **Xruns** da coluna `ERR` do `pw-top`, por nó do PipeWire. É um contador do servidor: acumula desde
  que o nó arrancou e volta a zero quando o PipeWire (ou o cliente) reinicia.
- **Quantum** do servidor via `pw-metadata -n settings` (1024 frames a 48 kHz = **21,3 ms** nesta
  máquina) quando não há nó activo para o `pw-top` mostrar.

Se o PipeWire não responder, o cartão diz "xruns não medidos" em vez de mostrar 0 — a diferença entre
"não há xruns" e "não consegui perguntar" é a diferença entre informação e ruído.

### Linha de comandos

```bash
LinuxExplode --cli placa      # link de cada dispositivo + temperaturas da placa
LinuxExplode --cli usb        # dispositivos e concentradores, com a velocidade negociada
LinuxExplode --cli audio      # placas, fluxos e xruns (corre o pw-top: ~2 s)
LinuxExplode --cli placa --json | jq '.dados.reduzidos'
```

## Ícone na bandeja

Com `StatusNotifierItem`/AppIndicator disponível (KDE, niri+noctalia, GNOME com a extensão), a
aplicação mostra um ícone com as temperaturas da CPU e da GPU e a potência medida no tooltip, e um
menu com **Abrir janela**, **Sensores**, **Dispositivos**, **Parar todo o stress**, **Fechar para a
bandeja** e **Sair**.

- Fechar a janela esconde para a bandeja (é o comportamento normal de um monitor); `Ctrl+Q` e *Sair*
  fecham de vez. A opção *Fechar para a bandeja* guarda a escolha em `~/.config/linux-explode/config.json`.
- **Parar todo o stress** usa o registo de sessões abertas (`SessaoStress.PararTodas()`): pára todas as
  sessões em curso, mesmo as que ficaram por trás de outra aba. Só está activo quando há alguma a correr.
- Sem bandeja no ambiente, a aplicação arranca como antes: o ícone é criado dentro de `try/catch` e a
  falha não impede a janela de abrir. `LINUXEXPLODE_SEM_BANDEJA=1` força o arranque sem ícone.
- **Dentro do Flatpak** o ícone precisa de três `finish-args` (já no manifest): falar com o
  `org.kde.StatusNotifierWatcher` e poder possuir o nome do item. O sandbox põe a aplicação no pid 2,
  por isso o nome é sempre `org.kde.StatusNotifierItem-2-0` — e é esse (mais `-2-1`) que está
  autorizado; `--own-name` não aceita wildcards. Verificado dentro do sandbox: o item aparece em
  `RegisteredStatusNotifierItems` e o tooltip lido por DBus traz valores medidos.

Verificação feita nesta máquina: `org.kde.StatusNotifierItem-<pid>-0` registado no
`org.kde.StatusNotifierWatcher`, tooltip lido por DBus `LINUX EXPLODE · CPU 43 °C · GPU 38 °C · 24 W`
e menu exposto em `com.canonical.dbusmenu` com os itens acima. Fechar a janela foi testado por
programa (`LINUXEXPLODE_TESTE_BANDEJA=1`): depois do `Close()` a janela fica invisível e o processo
continua vivo; `MostrarEm()` volta a mostrá-la; `Sair()` termina o processo.

## FPS por aplicação — cartão do MangoHud

No fim da página **Benchmark** há um cartão que lê as sessões que o
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

Valores medidos nesta máquina (Ryzen 7 9700X, RX 6700 XT Navi 22):

| modo | GPU | passagens em 1,5 s | nota |
|---|---|---|---|
| compute | Radeon RX 6700 XT (RADV NAVI22) | 331 | ~13 000 000 |
| compute | Radeon 610M integrada (RAPHAEL) | 15 | ~585 000 |
| ray tracing | Radeon RX 6700 XT | 105 | ~4 100 000 |
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

## Stress — até onde a máquina aguenta

A aba **Stress** aquece tudo o que se pode aquecer num PC e mostra o que está a acontecer em tempo
real (temperaturas, potência, ritmo) com uma guarda térmica que corta o teste antes de ser tarde.

| Alvo | O que faz | O que mede |
|---|---|---|
| **CPU** | 16 threads (ou o número que escolheres) com quatro cargas: FMA vetorial (SIMD), inteiro, vírgula flutuante e tráfego de memória | unidades/s, GFLOP/s, GB/s de tráfego, temperatura, potência |
| **RAM** | Enche 25/50/75 % da memória, escreve um padrão, relê e **verifica cada palavra** em passagens contínuas | GB/s de escrita e leitura, palavras verificadas, erros |
| **GPU** | Path tracer Vulkan em ciclo (o mesmo shader do benchmark) | milhões de pontos/s, temperatura e potência da GPU |
| **Disco** | Escreve e relê a verificar (sequencial) e escreve blocos de 4 KB aleatórios | MB/s de escrita e leitura, IOPS, bytes diferentes na releitura |
| **Tudo** | Os quatro em paralelo — o teste de estabilidade a sério | tudo o que está acima, mais os picos de temperatura |

### Guarda térmica

Lê a CPU (k10temp) e a GPU (amdgpu) **quatro vezes por segundo** e pára tudo quando um limite é
passado (85, 90, 95 ou 100 °C, à escolha). O resultado diz qual sensor disparou e a que temperatura
(`parado — guarda térmica: CPU a 96 °C (limite 95 °C)`), e os picos ficam registados na mesma.

### Valores medidos nesta máquina (Ryzen 7 9700X, RX 6700 XT, NVMe PCIe 4)

| Teste | Duração | Resultado |
|---|---|---|
| CPU · FMA vetorial · 16 threads | 15 s | 82 GFLOP/s (5 126 M FMA/s) |
| CPU · inteiro · 8 threads | 6 s | 6 255 M/s |
| CPU · vírgula flutuante · 8 threads | 6 s | 604 M/s (sin + cos + sqrt) |
| CPU · memória · 8 threads | 6 s | 5 241 M/s · 83,8 GB/s de tráfego |
| RAM · 50 % (30,2 GB) | 12 s | escrita 25,1 GB/s · leitura 25,4 GB/s · 0 erros em 16,2 G palavras |
| Disco · 512 MB por passagem | 12 s | escrita 1 082 MB/s · leitura 1 109 MB/s · 4 KB 20 096 IOPS |
| GPU · compute 640×360×128 spp | 12 s | 7,9 M pontos/s |
| Tudo (burn-in) | 20 s | 68,5 GFLOP/s + 19,4 GB × 4 de RAM + disco a 735 MB/s · pico CPU 68 °C |

O teste "Tudo" é o mais duro: as temperaturas sobem 10-20 °C acima do teste isolado e a potência da
plataforma passa dos 200 W. Serve para validar overclock, pasta térmica e estabilidade de memória.
Num teste de 20 s o CPU rende menos do que isolado (68 em vez de 82 GFLOP/s) — é o que acontece
quando a RAM, o disco e a GPU lhe roubam largura de banda. Esse é o resultado honesto.

### Ciclos agendados — o mesmo teste N vezes

Um teste longo diz que a máquina aguenta; **N ciclos iguais dizem se ela repete o resultado**. O
cartão *Ciclos agendados* corre o mesmo alvo (CPU, RAM, GPU, disco ou *tudo*) N vezes com uma pausa
entre ciclos e no fim apresenta, por métrica, a **mediana, o mínimo, o máximo e a variação**.

Os números são os do ranking: a mediana é o resultado, uma corrida isolada não é, e a variação só é
afirmada a partir de 3 ciclos — com 2 o cartão diz "amostra pequena (2)" em vez de gritar
instabilidade. Se a variação passar os 3 % o resumo avisa que a máquina não repetiu o resultado e que
a causa (térmica, turbo, outro processo) tem de ser vista.

Nesta máquina, dois ciclos de 3 s com FMA vetorial em 16 threads:

| Métrica | Mediana | Mínimo | Máximo |
|---|---|---|---|
| CPU (M/s) | 5 526,9 | 5 503,8 | 5 549,9 |
| CPU (GFLOP/s) | 88,4 | 88,1 | 88,8 |

Picos de 61 °C na CPU e 42 °C na GPU, com cada ciclo a fechar em 3 s. A guarda térmica continua
activa: um ciclo que dispare o limite termina a série e o resumo diz qual foi o motivo.

```bash
linux-explode --cli ciclos cpu 60 --ciclos 5 --pausa 30     # 5 × 1 min com 30 s de pausa
linux-explode --cli ciclos tudo 120 --ciclos 3 --limite 90   # burn-in em série
linux-explode --cli ciclos cpu 30 --ciclos 3 --json          # agregado para script
```

### Memória da GPU — encher a VRAM até o driver recusar

O stress normal aquece a GPU mas não mexe na memória de vídeo. O cartão *Memória da GPU (Vulkan)*
aloca blocos de VRAM (64 MB a 1 GB) até um limite escolhido — ou até o driver recusar —, **escreve um
padrão em cada bloco por inteiro** e relê duas amostras de 4 KB de cada um, comparando-as com o que
foi escrito. Quando um bloco não cabe, o resultado diz qual foi a resposta do driver
(`ErrorOutOfDeviceMemory`) e o teste termina com o que já tinha alocado e verificado.

O total alocado é o que o driver aceitou; o **pico e o uso em `/sys/class/drm/card*/device/mem_info_vram_used`**
são uma fonte independente da aplicação. Nesta máquina (RX 6700 XT, 11,98 GB de heap): 2 GB em 16
blocos de 128 MB em 0,7 s, uso a subir de 737,59 MB para **2,72 GB** de pico, 128 KB relidos sem
nenhuma diferença.

O valor "depois" do `--cli` é lido ainda dentro do processo — o driver só devolve o heap quando o
processo termina (2,72 GB durante o teste, 702 MB depois de sair). Encher até o driver recusar pode
deixar o ambiente gráfico sem memória: com o desktop a correr, 2 GB é o limite seguro nestas placas.

```bash
linux-explode --cli vram                       # 4 GB por omissão, GPU 0
linux-explode --cli vram --indice 1 --limite 512 --bloco 128
linux-explode --cli vram --limite 0            # até o driver recusar
```

### Segurança

- O ficheiro do teste de disco é criado em `~/.cache/linux-explode/stress/` e **apagado no fim**.
- A RAM deixa sempre 1 GB livre; se não houver memória suficiente, o teste recusa arrancar em vez de
  provocar o OOM killer.
- Sair da aba pára o teste (não fica a máquina a arder noutra página).
- Não há escrita em discos do sistema nem alteração de definições — é só carga.

### Registo e relatório

O cartão **Registo** guarda as últimas 12 entradas (início, fim, paragens, relatório). O botão
**Guardar relatório** escreve um JSON em `~/linux-explode-relatorios/stress-<data>.json` com a
configuração, as linhas do resultado, os picos e a energia medida.

### Linha de comandos

```bash
linux-explode --cli stress cpu 60                      # 1 minuto, todos os cores, FMA vetorial
linux-explode --cli stress cpu 120 --threads 4 --carga inteiro
linux-explode --cli stress ram 60
linux-explode --cli stress disco 120
linux-explode --cli stress gpu 60
linux-explode --cli stress tudo 300                     # burn-in de 5 minutos
linux-explode --cli stress cpu 30 --limite 80           # guarda térmica mais apertada
linux-explode --cli ciclos cpu 60 --ciclos 5 --pausa 30 # série de ciclos com agregado
linux-explode --cli vram --limite 2048 --bloco 128      # encher a VRAM e verificar
```

### Limitações (honestas)

- O stress de GPU usa o path tracer do projecto; não substitui ferramentas dedicadas (gpu-burn,
  Furmark). A VRAM passou a ser testada à parte (cartão *Memória da GPU*), mas alocar até à falha
  pode afectar o ambiente gráfico — o limite é uma escolha do utilizador.
- O stress de disco corre num ficheiro dentro do teu home — não testa o disco inteiro nem SMART
  durante a carga (a temperatura do disco aparece na aba Discos).
- A carga de CPU é sintética: aquece e mede, mas não prevê o desempenho de um jogo ou de uma
  compilação específicos.

## Discos: auto-teste S.M.A.R.T.

A saúde S.M.A.R.T. diz o que o disco reporta; o **auto-teste** é o controlador a exercitar o disco a
sério. Cada disco na aba **Discos** tem agora os botões *Teste curto* (≈2 min num NVMe, 1–2 min num
HDD) e *Teste longo* (varre a superfície inteira — pode demorar horas), o estado a correr com a
percentagem, *Abortar*, e o histórico dos últimos testes com as horas de funcionamento em que
correram.

Duas famílias de log, lidas do mesmo JSON do `smartctl -a`:

- **NVMe** — `nvme_self_test_log`: operação em curso, percentagem de conclusão e tabela de resultados.
- **ATA** — `ata_smart_data.self_test.status` (com `remaining_percent`) e `ata_smart_self_test_log`,
  de onde a linha "in progress" é excluída do histórico (é o teste a correr, não um resultado).

Nesta máquina, o cartão mostra o histórico real dos dois discos — no PNY CS1030, dois testes curtos
concluídos sem erros às 9 642 h e um abortado; no ST1000DM010, seis testes curtos concluídos, o mais
antigo às 34 264 h. O progresso actualiza-se sozinho de 4 em 4 segundos enquanto algum teste corre
(o `smartctl -t` devolve logo o controlo; o disco continua o teste sozinho).

```bash
linux-explode --cli autoteste                       # estado e histórico do primeiro disco
linux-explode --cli autoteste curto nvme0n1         # dispara um teste curto
linux-explode --cli autoteste longo sda --json      # teste longo, saída JSON
linux-explode --cli autoteste parar nvme0n1         # aborta
```

Precisa do `smartctl` com a regra NOPASSWD (a mesma que a saúde já usa); sem ela o cartão diz o
comando exacto para a conceder, em vez de inventar um resultado.

## Suspender e retomar

Num portátil é aqui que tudo se parte: sensores deixam de responder, o RAPL perde a permissão, o
relógio salta. O cartão *Suspender e retomar* (aba **Sistema**) mostra primeiro o que o sistema
anuncia — `/sys/power/state`, `/sys/power/mem_sleep` — e se há permissão para suspender e para
acordar sozinho. Depois suspende a sério e mede o tempo dormido por **três relógios independentes**:

- **parede − monotónico**: o relógio monotónico pára durante o suspend, a diferença é o tempo dormido;
- **uptime (CLOCK_BOOTTIME)**: confirma o mesmo número por outra via;
- no fim compara temperaturas e potência antes/depois e diz se os sensores voltaram a responder.

O despertar automático usa `rtcwake` (agenda a hora no relógio da máquina); sem essa regra no sudoers
cai para `systemctl suspend` e a máquina acorda quando lhe tocas — o cartão di-lo em vez de falhar em
silêncio. O botão *Simular (sem dormir)* corre a mesma medição sem suspender e serve de prova de que a
conta dá zero: nesta máquina 1,59 s de relógio de parede, **0,00 s** de suspend e 0,01 s de diferença
de uptime.

```bash
linux-explode --cli suspender                    # suporte, permissões e último teste
linux-explode --cli suspender testar --segundos 15
linux-explode --cli suspender testar --simular   # prova a conta sem dormir
```

Para o despertar sozinho, acrescenta o `rtcwake` ao mesmo ficheiro do smartctl:

```bash
echo "$USER ALL=(root) NOPASSWD: /usr/bin/smartctl, /usr/bin/rtcwake" | sudo tee /etc/sudoers.d/smartctl
sudo chmod 440 /etc/sudoers.d/smartctl
```

## Relatório completo — um ficheiro HTML

O botão *Gerar HTML completo* (aba **Relatório**) escreve um único ficheiro autónomo: CSS embutido,
gráficos SVG desenhados a partir das amostras medidas, **zero pedidos de rede e zero script**. Abre em
qualquer browser, arquiva-se e partilha-se sem levar mais nada atrás. O mesmo relatório sai da linha de
comandos:

```bash
linux-explode --cli relatorio html                 # ~/linux-explode-<data>.html
linux-explode --cli relatorio html /caminho/rel.html
linux-explode --cli relatorio json                 # para automação
```

O que vai lá dentro:

| Secção | Conteúdo |
|---|---|
| Destaques | CPU, memória, GPU, discos, corridas no histórico e o último stress |
| Sistema | distribuição, kernel, arquitetura, máquina, placa-mãe, BIOS, uptime, CPU e RAM |
| Placas de vídeo | modelo, PCI, driver, relógio, VRAM, temperatura |
| Discos e saúde | modelo, tamanho, veredito S.M.A.R.T., horas, desgaste, **auto-teste** e partições |
| Sensores | todas as leituras de hwmon, agrupadas por chip |
| Benchmarks | ranking local (mediana das últimas 3, melhor, última, variação) com barras por tipo e as últimas 15 corridas |
| Stress, ciclos e VRAM | o último resultado de cada um, com métricas agregadas e as linhas medidas |
| Sessão gravada | resumo da última sessão CSV e quatro gráficos (CPU, GPU, potência, frequência) |

Para o relatório não depender de se ter clicado em *Guardar relatório*, o último resultado de cada
teste passa a ser guardado sozinho em `~/.local/share/linux-explode/` (`ultimo-stress.json`,
`ultimo-ciclos.json`, `ultimo-vram.json`) — tanto pela interface como pelo `--cli`. Sem stress, sem
sessões ou sem benchmarks, a secção di-lo em vez de aparecer vazia.

Nesta máquina o ficheiro sai com 16,5 KB, sete secções e quatro gráficos, a partir de 10 corridas de
benchmark, 3 ciclos de stress, um stress simples e uma sessão gravada.

## Rede — diagnóstico e débito real

O cartão **Diagnóstico de rede** mede latência TCP até ao router e à internet (min/média/máx, jitter,
perda), o tempo de DNS e a velocidade de download/upload pelos servidores da Cloudflare — é o teste
rápido de "a internet está bem?".

O cartão **Débito real — cliente/servidor** mede outra coisa: o débito entre dois PC que correm a
própria aplicação, sem servidores de terceiros. Um lado fica à escuta, o outro envia N MB e recebe os
mesmos N MB de volta:

```
PC A: linux-explode --cli netbench servidor            # fica à escuta na porta 47820
PC B: linux-explode --cli netbench cliente 192.168.1.10 --mb 64
```

O protocolo é mínimo — 20 idas e voltas de 8 bytes para a latência, o tamanho do corpo e os bytes —
e é o mesmo código nos dois lados, por isso o teste local prova o caminho todo. O resultado mostra
subida, descida, latência média/mín/máx e jitter; o servidor regista o que recebeu e a que ritmo.

```bash
linux-explode --cli netbench cliente 127.0.0.1 --porta 47820 --mb 32
linux-explode --cli netbench servidor --uma-vez --json   # o modo servidor não devolve JSON
```

No loopback desta máquina (Ryzen 7 9700X), 32 MB em cada sentido: **11 325,8 MB/s a subir**,
**15 054,6 MB/s a descer** e **0,11 ms** de latência. São valores de memória do kernel, não de uma
rede — servem para provar que o cliente e o servidor funcionam, não para medir a LAN. Numa rede real o
número é o que a rede (e o disco, se o outro lado for lento) permitirem.

## Histórico em SQLite

O histórico de resultados deixou de ser um JSON relido por inteiro: passou a `historico.db`
(SQLite, `~/.local/share/linux-explode/`), com índices por `(tipo, alvo)` e por data. O ficheiro
`resultados.json` antigo é **migrado uma única vez** no primeiro arranque — a marca fica na tabela
`migracoes` — e renomeado para `resultados.json.migrado`, pelo que nada se perde e nada é importado
duas vezes.

| Operação | Como |
|---|---|
| Guardar | `Historico.Registar` → `Banco.Registar`; notas repetidas do mesmo tipo/alvo não entram |
| Ler | `Banco.Todos()` (ordem cronológica, tecto de 500 entradas) |
| Pesquisar | `--cli historico gzip` ou `Banco.Pesquisar("gzip")` — procura em tipo, alvo e detalhe |
| Período | `--cli historico --desde 2026-10-01 --ate 2026-10-31` |
| Exportar/importar | os mesmos CSV/JSON de sempre; a importação junta sem duplicar |

Na interface, o cartão *Histórico de resultados* (aba Benchmark) tem a caixa **Procurar**, o
filtro **Período** (tudo, 7, 30 ou 90 dias), a lista filtrada e um **gráfico** da série tipo/alvo mais
recente com pelo menos duas corridas — sem normalizar notas de tipos diferentes, que não seriam
comparáveis.

```bash
linux-explode --cli historico                       # 10 de 10 resultado(s)
linux-explode --cli historico real --limite 2       # 2 de 10 · procura "real"
linux-explode --cli historico --desde 2026-10-25 --json
```

Uma armadilha que custou testes: o `Microsoft.Data.Sqlite` faz pooling de ligações por omissão. Com o
pool ligado, apagar `historico.db` por fora e voltar a abrir devolvia a ligação em cache (a apontar
para o inode apagado) e a importação contava zero. A ligação é aberta com `Pooling=false`.

## Portabilidade (outro Linux, outro hardware)

O binário é **self-contained**: leva o runtime .NET 10 dentro, não precisa de nada instalado
além do driver de vídeo. Tudo o resto é lido de `/proc`, `/sys` e DMI, sem root.
Há dois alvos publicados, ambos Flatpak: **linux-x64** (`linux-explode.flatpak`) e **linux-arm64**
(`linux-explode-aarch64.flatpak`).

| Situação | O que acontece |
|---|---|
| Outra distro (Debian, Fedora, openSUSE, NixOS) | funciona; não há nada específico do Arch |
| Flatpak em qualquer distro | o runtime `org.freedesktop.Platform` 25.08 traz glibc, ICU, X11/Wayland e Vulkan loader — é a única via de distribuição, por isso não há dependências da distro |
| GPU AMD, Intel ou NVIDIA | Vulkan é genérico; a lista de GPUs é enumerada em runtime |
| GPU NVIDIA ou Intel | temperatura e nome vêm da hwmon/pci.ids; **uso (%) e VRAM só no amdgpu** (`gpu_busy_percent`, `mem_info_vram_*`), porque é o único driver que os expõe em sysfs |
| CPU ARM (SoC, Raspberry Pi) | sem `k10temp`/`coretemp` a app não elege uma temperatura de CPU; lista todos os canais hwmon que o kernel expuser |
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
  x86_64 não instala em ARM. Para ARM usa `linux-explode-aarch64.flatpak` (ver secção ARM64).
- **S.M.A.R.T.**: precisa de root em qualquer máquina; a regra sudoers é por máquina.
- **Super I/O da placa**: depende do chip (nesta é `nct6683`, noutra pode ser `it87`, `nct6775`,
  `f71882fg` ou não existir, como nos portáteis). A app mostra o que o kernel expuser — não carrega módulos.

## Testes

`tests/LinuxExplode.Tests` (xUnit, **130 testes**) cobre as regras que a aplicação promete: histórico
sem repetições, mediana e variação, exportar/importar sem duplicar, configuração que sobrevive a um
reinício, medição de energia, alertas, cargas de CPU do stress, e — desde a 0.20.5 — os parsers de
dispositivos: GT/s → geração PCIe, débito útil com a codificação certa, classificação de links
finais vs pontes, códigos de classe PCI, nomes de fabricante, velocidades e classes USB, bytes por
amostra de cada formato ALSA, latência de período/buffer, o parser do `pw-top` e a leitura de
`/proc/asound` a partir de uma árvore temporária; na 0.20.7 juntaram-se a mediana/variância do
ranking, a curva de latência (monótona, L1 muito abaixo da RAM), a fonte C determinística da tarefa
real, o CSV da sessão (célula vazia em vez de zero) e o resumo por série; na 0.20.11 juntou-se o
agregado dos ciclos (mediana, contadores somados, amostras reais, variação só a partir de 3), o parser
do auto-teste S.M.A.R.T. nas duas famílias de log (NVMe parado e a correr, ATA a correr com conversão
do restante, linha "in progress" fora do histórico, JSON inválido) e a simulação da suspensão — que
falha se o relógio de parede e o monotónico não andarem juntos; na 0.20.13 juntou-se o relatório
completo (secções, escape do hardware, CSV da sessão, gráficos SVG), o banco (migração única do JSON,
pesquisa por texto e período, dedupe e o teste da base apagada por fora) e a rede (cliente e servidor
no loopback, com a recusa de ligação a sair em pt-PT); na 0.20.15 juntaram-se os idiomas (os dois
mapas têm exatamente as mesmas chaves, nenhuma tradução vazia, a cultura acompanha o idioma e os
moldes `TF` substituem valores). Os testes correm no CI a cada push:

```bash
dotnet test tests/LinuxExplode.Tests/LinuxExplode.Tests.csproj -c Release
```

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

Requisitos: .NET SDK 10 (`dotnet --version` ≥ 10.0) e, para o Flatpak, `flatpak` + `org.flatpak.Builder`.

```bash
# desenvolvimento
dotnet build -c Release
./bin/Release/net10.0/LinuxExplode

# executável autónomo (não precisa de .NET instalado)
dotnet publish LinuxExplode.csproj -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o dist
./dist/LinuxExplode
```

## Flatpak

```bash
flatpak install --user -y flathub org.flatpak.Builder org.freedesktop.Sdk//25.08

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
#   linux-explode-aarch64.flatpak  29 MB  precisa do runtime aarch64
```

No destino ARM, instala-se o bundle (o runtime vem do Flathub):

```bash
flatpak install --user flathub org.freedesktop.Platform/aarch64/25.08
flatpak install --user ./linux-explode-aarch64.flatpak
flatpak run pt.linuxexplode.LinuxExplode
```

O bundle traz as mesmas finish-args do manifesto x86_64 — incluindo
`--persist=.local/share/linux-explode` (sem ela o histórico e as sessões perdiam-se a cada arranque) e
as pastas do MangoHud.

Como o bundle é construído sem correr nada em ARM: o build dir é montado à mão
(`files/bin/linuxexplode`, `.desktop`, metainfo, ícone) e tem de ser finalizado com
`flatpak build-finish` — que **não** tem `--arch`; a arquitectura vem do runtime escrito no
`metadata` (`org.freedesktop.Platform/aarch64/25.08`). Depois
`flatpak build-export --arch=aarch64` e `flatpak build-bundle --arch=aarch64`.
O bundle foi validado importando-o de volta para um repo `ostree`:
`app/pt.linuxexplode.LinuxExplode/aarch64/stable`, 222 ficheiros, `LinuxExplode` e as `.so` nativas
confirmadas como `ELF ARM aarch64`.

**Não executado em hardware ARM.** O que está provado: o payload é ARM válido, o bundle é bem
formado e, importado de volta num repo `ostree`, tem a árvore correcta.
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
│   ├── Discos.cs         # /sys/block + smartctl (saúde e auto-teste curto/longo)
│   ├── BenchMemoria.cs   # latência por tamanho de bloco (pointer chasing, 4 KB → 256 MB)
│   ├── TarefaReal.cs     # descomprimir (SHA-256) + compilar C, sem rede
│   ├── GravadorSessao.cs # sessão de monitorização em CSV, com resumo e gráfico
│   ├── Placa.cs          # links PCIe (/sys/bus/pci) e temperaturas de chipset/memória/rede
│   ├── Usb.cs            # dispositivos USB: velocidade negociada, classes, drivers
│   ├── Audio.cs          # /proc/asound + xruns (pw-top) e quantum (pw-metadata)
│   ├── Alertas.cs        # regras de aviso (temperatura, ventoinhas, espaço)
│   ├── CiclosStress.cs   # série de N ciclos do mesmo stress, com agregado por métrica
│   ├── StressVram.cs     # enchimento de VRAM em Vulkan, com escrita e verificação
│   ├── Suspender.cs      # suspender/retomar: três relógios e sensores depois do wake
│   ├── BenchCpu.cs       # path tracer (portado do original, inalterado)
│   ├── BenchDisco.cs     # RAM + disco com O_DIRECT
│   ├── Rede.cs           # latência, DNS, velocidade
│   ├── Relatorio.cs      # fachada do relatório (HTML completo + JSON)
│   ├── RelatorioCompleto.cs # junta sistema, sensores, benchmarks, stress e sessão num HTML autónomo
│   ├── Banco.cs          # histórico em SQLite: esquema, migração do JSON, pesquisa e período
│   ├── BenchRede.cs      # débito real cliente/servidor (TCP próprio, latência e jitter)
│   ├── Cli.cs            # modo sem interface
│   └── Util.cs
├── Vistas/               # UI Avalonia
│   ├── MainWindow.axaml  # barra lateral + área de conteúdo
│   ├── CurvaLatencia.cs  # gráfico da curva de latência (eixo X logarítmico)
│   ├── Pagina*.cs        # uma classe por página
│   ├── Bandeja.cs        # ícone na bandeja (StatusNotifierItem) e menu
│   └── UI.cs             # cartões, linhas, métricas
├── Tema/Tema.axaml       # paleta e estilos (monocromático, blocos)
├── flatpak/              # manifest, .desktop, metainfo
├── tests/                # xUnit (130 testes, correm no CI)
├── docs/                 # capturas reais e a chave pública do repositório (`linux-explode-release.gpg`)
├── permitir-energia.sh   # (root, uma vez) torna os contadores RAPL legíveis
└── packaging/arm64/      # scripts antigos do pacote ARM64 (já não publicados — a via é o Flatpak)
```

## Limitações conhecidas

- **S.M.A.R.T. detalhado** precisa de `smartmontools` e root. Dentro do Flatpak o acesso a
  `/dev` é limitado: a app diz isso na própria página, em vez de inventar um veredito.
- **Auto-teste S.M.A.R.T.** é assíncrono por natureza: a app dispara e acompanha, mas quem corre o
  teste é o controlador do disco. Num HDD, o teste longo pode demorar horas e degradar o desempenho
  do disco enquanto corre (podes abortá-lo no cartão).
- **Suspender/retomar** só é testável a sério com permissão para suspender; o despertar automático
  exige o `rtcwake` no sudoers. Sem ele o teste suspende na mesma, mas fica à espera de uma tecla —
  o cartão avisa antes. A simulação prova a aritmética dos relógios sem dormir nada.
- **Benchmarks de GPU** do original (stress test D3D11, ray tracing D3D12) foram substituídos por
  equivalentes em Vulkan (`Core/BenchGpu.cs`, `Core/BenchRt.cs`); o modo ray tracing exige
  `VK_KHR_ray_query` no driver.
- **Overlay de FPS** não existe como overlay global: em Wayland nenhuma app vê os frames de outra.
  A via suportada é o MangoHud (layer Vulkan/OpenGL); a app lê os CSV dele, mostra o cartão *FPS ·
  sessão do MangoHud* na aba Benchmark e o `--cli fps`. Falta só o lançador/overlay.
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
- **VRM / fases de alimentação** não têm sensor nesta placa: o `nct6687` expõe termistores sem
  etiqueta (`Thermistor 0/1/15`) e chamar-lhes VRM seria adivinhar. O cartão da placa-mãe diz
  "sem sensor nesta máquina" em vez de mostrar um número inventado.
- **Link PCIe reduzido** só é assinalado em dispositivos finais, e o máximo é o que o próprio
  dispositivo anuncia — não o do slot. Uma ponte não é classificada: o valor actual é o link que
  ela serve. Se um controlador aparece a Gen1 x1 é porque o kernel reporta isso em `LnkSta`
  (acontece nas funções internas do chipset desta placa).
- **Xruns de áudio** são o contador do servidor PipeWire (coluna `ERR` do `pw-top`), acumulado
  desde que o nó arrancou — e não existem se o servidor não responder. Sem `pw-top` acessível
  (sandbox sem socket do PipeWire e sem `flatpak-spawn`), o cartão diz "xruns não medidos".
- **Ícone na bandeja** precisa de `StatusNotifierItem`/AppIndicator no ambiente (KDE, niri+noctalia,
  GNOME com extensão). Sem isso a aplicação arranca normalmente, só não há ícone.
- **Tarefa real**: a parte de compilação precisa de um compilador C no sistema (ou no anfitrião, dentro
  do Flatpak). Sem ele a nota da descompressão continua a valer e o motivo é dito.
- **Tarefa real / descompressão**: o valor depende do `zlib` ligado ao binário (o do sistema anfitrião
  ou o do runtime do Flatpak) — compare corridas do mesmo binário, não uma do `--cli` com outra da app.
- **Latência de memória** mede o que a máquina faz naquele momento: turbo, afinidade de threads e
  outras cargas mudam o valor. Para comparar, corre com a máquina em repouso e usa a mediana.
- **Sessões** têm resolução de 1 s: picos mais curtos do que isso não aparecem no CSV. No Flatpak o
  CSV fica em `~/.var/app/pt.linuxexplode.LinuxExplode/.local/share/linux-explode/sessoes/`.

## Licença

MIT — como o projeto original.

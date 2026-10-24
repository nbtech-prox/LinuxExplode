# Portar o que falta — avaliação

Branch: `portar-restantes` (não toca em `main`).
Objectivo: para cada funcionalidade da app Windows que ficou de fora, dizer se **dá** para portar,
por que meio, com que esforço e com que riscos — e provar a parte viável com código a correr.

## 1. Veredicto por item

| Original (Windows) | Equivalente em Linux | Veredicto | Esforço | Notas |
|---|---|---|---|---|
| DXGI / Direct3D 11 / D3D12 + DXR | `BenchGpu` (compute Vulkan + SPIR-V) e `BenchRt` (`VK_KHR_ray_query`) | **já portado** | — | O README estava desactualizado a dizer "em falta". Corrigido nesta branch. |
| ETW / PresentMon (FPS de jogos) | CSV do **MangoHud** (Vulkan/OpenGL layer) | **parcial, viável** | baixo (leitura) / médio (UI) | Wayland não expõe frames à app; sem layer próprio, o FPS por aplicação tem de vir do MangoHud. Leitura implementada nesta branch. |
| Overlay dentro do jogo | `wlr-layer-shell` + layer Vulkan próprio, ou `gamescope` | **difícil** | alto | O Avalonia não suporta layer-shell nativamente; um overlay global em Wayland exige código Wayland nativo. Alternativa honesta: lançar o jogo com MangoHud. |
| Registo do Windows (tweaks, arranque) | `systemd` (units, `systemd-analyze`), `.desktop` em `~/.config/autostart`, `sysctl` | **viável** | médio | v1 só leitura (listar serviços/autostart e tempos de boot); escrever exige polkit/root. |
| `winget` / instalar apps | `pacman`/`apt`/`flatpak` por distro | **não faz sentido** | — | Cada distro tem o seu; embutir um gestor de pacotes seria frágil e perigoso. |
| Drivers `.inf`/`.sys`, portas COM | — | **não aplicável** | — | Não existe o conceito em Linux (drivers no kernel, séries em `/dev/tty{USB,ACM}`). |
| iCUE SDK (Corsair) | **OpenRGB SDK** (TCP 6742) | **viável** | médio | Depende de terceiro instalado (`openrgb --server`). Permissões de i2c/udev podem exigir root. |
| Potência total da máquina | — | **impossível por software** | — | Nenhum sensor de fonte/placa nesta máquina (NCT6687D só dá tensões). Já documentado. |

## 2. O que já está feito nesta branch

**`Core/FpsMangohud.cs` + `--cli fps [ficheiro]`** — leitura das sessões do MangoHud:

- Procura o CSV mais recente por esta ordem: `output_folder=` de `~/.config/MangoHud/MangoHud.conf`,
  `MANGOHUD_LOG_DIR`, `~/mangologs`, `~/.local/share/MangoHud`, `/tmp/MangoHud`.
- Faz o parse do bloco de sistema (`os,cpu,gpu,ram,kernel,driver,cpuscheduler`) e das colunas
  documentadas em `src/logging.cpp` do MangoHud
  (`fps,frametime,cpu_load,cpu_power,gpu_load,cpu_temp,gpu_temp,…,elapsed`).
- Calcula **FPS médio**, **1% low** e **0,1% low** (média dos piores frametimes, não o pior frame
  isolado), frametime médio e pior, GPU load/temperatura/potência e CPU load.

Exemplo real de saída — sessão de **4 s a 60 fps** capturada com o `vkcube` (Vulkan) e o MangoHud:

```
ficheiro   …/mo1/vkcube_2026-10-24_18-28-32.csv
aplicação  vkcube
sistema    CachyOS · AMD Ryzen 7 9700X 8-Core Processor · AMD Radeon RX 6700 XT (RADV NAVI22) · 7.3.0-5-cachyos
amostras   240 · 4.0 s
FPS        médio 60.0 · 1% low 58.2 · 0.1% low 57.9
frametime  médio 16.67 ms · pior 17.26 ms
GPU        0% · 40 °C · 4 W · CPU 1%
```

**Verificação cruzada contra o resumo que o próprio MangoHud escreve** (ficheiro `*_summary.csv`):

| Métrica | LinuxExplode | MangoHud |
|---|---|---|
| FPS médio | 60.0 | 60.0 |
| 1% low | 58.2 | 58.55 |
| 0,1% low | 57.9 | 57.94 |
| frametime médio | 16,67 ms | 16,7 ms |

As diferenças nos lows vêm da fórmula: aqui usa-se a média dos piores frametimes, o MangoHud usa
o percentil 0,01 da sua própria série. Se lhe passarmos directamente o `*_summary.csv`, ele é lido
como resumo (colunas `Average FPS`, `1% Min FPS`, `0.1% Min FPS`).

## 3. Fases propostas

| Fase | Conteúdo | Estado |
|---|---|---|
| 1 | Leitura das sessões do MangoHud e resumo na CLI | **feito nesta branch** |
| 2 | UI: cartão de FPS/frametime com sparkline (Avalonia) na página do bench, com escolha da sessão | **feito** (`Vistas/CartaoFps.cs`) |
| 3 | Lançador: escolher executável, correr com `MANGOHUD=1 MANGOHUD_CONFIG=output_folder=…`, duração fixa e abrir o resumo no fim | a fazer |
| 4 | (opcional) OpenRGB: página de RGB via SDK TCP — listar dispositivos, cor, modos | avaliar |
| 5 | (opcional) "Arranque": units systemd + autostart com tempos de `systemd-analyze` (só leitura) | avaliar |

## 4. Riscos e limites

- **MangoHud é dependência externa.** Sem ele não há FPS de jogos no Linux; o código diz isso em
  vez de apresentar números inventados. Nesta máquina não está instalado.
- **Sem overlay global.** Em Wayland um overlay sobre outro processo exige `wlr-layer-shell` (niri
  suporta) **e** um layer Vulkan/OpenGL injetado no jogo — muito mais do que a app faz hoje.
- **Flatpak:** a sandbox não via `~/mangologs`, `~/.config/MangoHud` nem `~/.local/share/MangoHud`.
  O manifesto passou a expor essas três pastas **só de leitura** (`--filesystem=…:ro`). Atenção:
  o flatpak ignora mounts para caminhos que não existem — a pasta tem de existir *antes* de a app
  arrancar, senão o cartão diz "sem sessões".
- **OpenRGB** fala com os LEDs por i2c/USB-HID: o servidor é que precisa de permissões; a app só
  conversa com o socket.
- **Escrita de estado** (activar/desactivar serviços, aplicar `sysctl`) exige polkit/root: a v1 do
  "Arranque" fica só de leitura, coerente com o resto da app.

## 5. Como testar o que existe

```bash
# resumo da sessão mais recente do MangoHud
LinuxExplode --cli fps

# ou de um CSV concreto
LinuxExplode --cli fps ~/mangologs/Cyberpunk2077_2026-10-24_18-40-12.csv
```

Instalar e capturar com qualquer aplicação Vulkan/OpenGL (nesta máquina: `sudo pacman -S mangohud`,
já instalado; o `vulkan-tools` traz o `vkcube`):

```bash
mkdir -p ~/mangologs ~/.config/MangoHud   # têm de existir antes de abrir a app (ver Flatpak)
MANGOHUD=1 MANGOHUD_CONFIG="output_folder=$HOME/mangologs,autostart_log=2,log_duration=10" \
  mangohud vkcube          # ou o jogo/programa que se quer medir

LinuxExplode --cli fps     # apanha o CSV mais recente
```

Duas armadilhas observadas com o MangoHud 0.8.4:

- **`no_display` desliga o logging** — a pasta fica vazia. Para esconder o HUD sem perder registos,
  usar `background_alpha=0` em vez de `no_display`.
- Sem `output_folder` não há CSV nenhum: o hotkey de logging não faz nada.

Teste de regressão sem depender do MangoHud: CSV sintético com 1000 amostras (990 a 16,667 ms e 10
a 33,333 ms) dá 59,4 fps de média, 30,0 de 1% low e 16,83 s — igual ao cálculo teórico.

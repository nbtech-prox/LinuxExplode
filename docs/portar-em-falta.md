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

Exemplo real de saída (CSV sintético, ver secção 5):

```
ficheiro   …/mangologs/Cyberpunk2077_2026-10-24_18-40-12.csv
aplicação  Cyberpunk2077
sistema    CachyOS Linux · AMD Ryzen 7 9700X 8-Core Processor · AMD Radeon RX 6700 XT (RADV NAVI22) · Mesa 25.2.0
amostras   1000 · 16.8 s
FPS        médio 59.4 · 1% low 30.0 · 0.1% low 30.0
frametime  médio 16.83 ms · pior 33.33 ms
GPU        88% · 64 °C · 42 W · CPU 22%
```

## 3. Fases propostas

| Fase | Conteúdo | Estado |
|---|---|---|
| 1 | Leitura das sessões do MangoHud e resumo na CLI | **feito nesta branch** |
| 2 | UI: cartão de FPS/frametime com sparkline (Avalonia) na página do bench ou página própria, escolha da sessão | a fazer |
| 3 | Lançador: escolher executável, correr com `MANGOHUD=1 MANGOHUD_CONFIG=output_folder=…`, duração fixa e abrir o resumo no fim | a fazer |
| 4 | (opcional) OpenRGB: página de RGB via SDK TCP — listar dispositivos, cor, modos | avaliar |
| 5 | (opcional) "Arranque": units systemd + autostart com tempos de `systemd-analyze` (só leitura) | avaliar |

## 4. Riscos e limites

- **MangoHud é dependência externa.** Sem ele não há FPS de jogos no Linux; o código diz isso em
  vez de apresentar números inventados. Nesta máquina não está instalado.
- **Sem overlay global.** Em Wayland um overlay sobre outro processo exige `wlr-layer-shell` (niri
  suporta) **e** um layer Vulkan/OpenGL injetado no jogo — muito mais do que a app faz hoje.
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

Sessão de teste usada nesta branch (sem MangoHud instalado): CSV com 1000 amostras, 990 a
16,667 ms e 10 a 33,333 ms. O resultado — 59,4 fps de média, 30,0 de 1% low, 16,83 s de duração —
coincide com o cálculo teórico, o que valida o parse e os percentis.

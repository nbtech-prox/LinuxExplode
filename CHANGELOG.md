# Histórico de alterações

Formato segundo [Keep a Changelog](https://keepachangelog.com/pt-PT/1.1.0/) e
[versão semântica](https://semver.org/lang/pt/).

## [0.3.0] — 2026-10-24

### Adicionado

- **Prévia animada do benchmark em pop-up**: as cenas do EXPLODE BENCH passam a ser renderizadas ao
  vivo numa janela própria, com a câmara a orbitar (volta completa em ~18 s) e contador de
  fotogramas por segundo. Três modalidades: compute Vulkan (960×540, 32 spp), ray tracing
  `VK_KHR_ray_query` (800×450, 16 spp) e path tracer de CPU (560×315, 6 spp).
- Botões `Ver a animar` / `Ver ray tracing a animar` nos cartões de GPU e CPU.
- Gancho de teste `LINUXEXPLODE_PREVIA=cpu|gpu|rt` para abrir a prévia directamente.
- Os shaders GLSL (`Assets/Shaders/*.comp`) passam a aceitar um ângulo de câmara nos push
  constants; com o ângulo a 0 o caminho é exactamente o antigo, pelo que as notas do benchmark
  não mudam.

### Alterado

- As imagens estáticas do resultado deixaram de estar embutidas nas cartas do bench — o resultado
  passou a ser visto na janela de prévia.

### Corrigido

- A versão no rodapé estava fixa no `MainWindow.axaml` (`v0.1.0`) e não acompanhava o `Relatorio`.
- Fechar a janela de prévia a meio de um fotograma podia rebentar a aplicação.

## [0.2.0] — 2026-10-24

### Adicionado

- **FPS por aplicação** (novo cartão na página EXPLODE BENCH): lê as sessões CSV do MangoHud, com
  FPS médio, 1% low, 0,1% low e frametime médio, sparkline dos frametimes, escolha da sessão e
  passagem automática para a mais recente. Lê também o `*_summary.csv` do MangoHud.
- `--cli fps [ficheiro]` para o mesmo resumo no terminal.
- `docs/portar-em-falta.md`: avaliação do que falta face à versão Windows (o que é viável, o que é
  difícil e o que não faz sentido portar).
- Flatpak: permissões de leitura para `~/mangologs`, `~/.config/MangoHud` e `~/.local/share/MangoHud`.

### Corrigido

- README afirmava que os benchmarks D3D11/D3D12 do original não tinham sido portados; existem os
  equivalentes em Vulkan (`BenchGpu`) e `VK_KHR_ray_query` (`BenchRt`).

## [0.1.0] — 2026-10-24

Primeiro release público.

### Adicionado

- **Sistema**: distro, kernel, arquitectura, máquina, placa-mãe, BIOS, CPU (núcleos, threads,
  frequências, cache L3), RAM por módulo (SPD DDR5 via `spd5118` e DDR4 via `ee1004`, com
  SMBIOS como alternativa) e GPUs.
- **Sensores** ao vivo a cada segundo (temperatura, ventoinhas, potência, tensão, corrente,
  frequência), com filtro por tipo. No filtro **Frequência**, cartão de frequência por
  núcleo/thread a partir de `cpufreq` e, depois, os relógios que o hwmon expõe por GPU.
- **Consumo medido** no filtro **Potência**: socket da CPU pelos contadores RAPL
  (`/sys/class/powercap`, convertidos em watts por diferença entre leituras, com média móvel e
  tratamento do wrap do contador), potência das GPUs por `power1_average`/`power1_input` do
  hwmon, total e energia acumulada desde o arranque. A GPU integrada é detectada e não é somada
  outra vez (já está dentro do pacote). O cartão declara que **não** é o consumo da tomada.
- **Discos**: modelo, SSD/HDD, partições com ocupação e saúde S.M.A.R.T. via `smartctl`.
- **Rede**: latência TCP (router e internet), jitter, perda, DNS, download e upload.
- **EXPLODE BENCH**: path tracer determinístico em CPU (960×540, 1024 spp), o mesmo em compute
  shader Vulkan (1280×720), modo ray tracing com `VK_KHR_ray_query`, banda/latência de RAM e
  disco sequencial e 4K com `O_DIRECT`.
- **Relatório** em HTML ou JSON e **modo CLI** (`info`, `freq`, `sensores`, `potencia`, `discos`,
  `rede`, `bench`, `relatorio`) que corre sem servidor gráfico.
- **Empacotamento**: Flatpak x86_64, Flatpak aarch64 (sem emulação) e instalador ARM64 sem root.
- **`permitir-energia.sh`**: unidade systemd que torna legíveis os contadores RAPL, que o kernel
  expõe como `0400 root` desde a CVE-2020-8694.
- **CI** (`.github/workflows/ci.yml`): build Release, smoke test da CLI e publish dos binários
  x64 e ARM64 em cada push/PR.

### Notas

- Sem `smartctl` com sudo e sem os módulos do Super I/O, as secções correspondentes dizem o que
  falta e como resolver, em vez de aproximar valores.
- A NVIDIA não publica relógios nem potência em sysfs: essas linhas ficam indisponíveis.
- O acesso a `/dev` dentro do Flatpak é limitado, o que afecta o S.M.A.R.T. detalhado.

[0.3.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.3.0
[0.2.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.2.0
[0.1.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.1.0

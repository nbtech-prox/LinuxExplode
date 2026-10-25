# Histórico de alterações

Formato segundo [Keep a Changelog](https://keepachangelog.com/pt-PT/1.1.0/) e
[versão semântica](https://semver.org/lang/pt/).

## [0.14.0] — 2026-10-25

### Alterado

- **Os testes de CPU correm em corridas**: cada passagem completa (1024 amostras/pixel) é um teste,
  que termina e recomeça sozinho, com a nota da corrida e o histórico a acumular no cartão. Antes o
  pop corria uma hora seguida e a nota era uma média que nunca "fechava".
- Ao fim de cada corrida o quadro **completo** fica 2,5 s à vista, com a nota final e uma contagem
  decrescente (`concluída; a recomeçar em 2.0 s`) — antes a imagem era limpa no instante seguinte e
  parecia um ciclo que nunca acabava.
- Durante a corrida o estado mostra o progresso (`corrida 2 em curso · 16 thread(s) · 58% · 6.1 s`) e
  a nota grande só muda quando uma corrida fecha, em vez de tremer com valores parciais.

### Corrigido

- **Actualização do Flatpak**: o pacote era instalado a partir de um bundle, com uma origem local
  (`linuxexplode-origin`) sem URL, que o gestor de pacotes apresentava como actualização e depois
  falhava. O `empacotar.sh` passa a publicar num repositório OSTree local
  (`flatpak/repo-x86_64`) exposto como remote `linuxexplode`, e a instalar a partir dele — o
  `flatpak update` passa a funcionar e a mostrar o que está instalado.
- O `flatpak info` mostrava `Version: 0.1.0` porque o `metainfo.xml` não era actualizado; passa a
  acompanhar a versão do projecto.
- O bundle x86_64 passa a ser exportado no ramo `stable` (era `master`), igual ao ARM64.
- `--cli relatorio` rebentava com uma excepção quando o argumento não era `html`/`json` (gerava um
  caminho absurdo a partir do argumento); passa a validar o formato, a aceitar um ficheiro de saída
  opcional e a criar as pastas que faltarem.

## [0.12.0] — 2026-10-24

### Corrigido

- **A qualidade da animação já não depende da resolução**: o orçamento passou a ser de amostras por
  fotograma (265 M no compute, 130 M no ray tracing), em vez de um máximo de 32 spp por pixel. Antes,
  4K levava 4 spp (grão grosso) e 320×180 levava 32 spp ampliadas 3× (grão ainda mais visível) — só o
  1280×720 calhava bem. Agora: 256 spp a 720p e abaixo, 128 a 1080p, 96 a 1440p, 32 a 4K.
- A semente do ruído é fixa durante a animação: o grão deixa de "ferver" de fotograma para fotograma.
- O estado do pop mostrava `0 fotogramas/s` no primeiro meio segundo; passa a `a contar fotogramas…`.

### Documentado

- A nota do GPU varia com a resolução (8,59 M a 320×180, 12,96 M a 1280×720, 13,36 M a 4K): no
  extremo pequeno o custo fixo de cada passagem pesa mais do que os píxeis desenhados.

## [0.10.0] — 2026-10-24

A numeração passa a subir de **2 em 2** na parte menor: 0.10.0, 0.12.0, 0.14.0, …

### Adicionado

- **O pop do benchmark substitui os botões de prévia**: `Testar GPU`, `Testar ray tracing`,
  `Multi-core` e `Single-core` abrem a cena animada e medem ao mesmo tempo. Saíram os botões
  `Ver a animar` / `Ver ray tracing a animar` e o `Cancelar` do cartão de CPU (o pop fecha-se e
  o teste pára).
- **Escolha de resolução no pop-up**, de 320×180 até **3840×2160 (4K)**, tanto no compute como no
  ray tracing. A medição continua a ser feita com 64 amostras por pixel repetidas até 1,5 s; a
  animação adapta as amostras por fotograma (4 a 32) para se manter fluida.
- **CPU com animação dos cores**: o pop-up mostra a imagem a construir-se bloco a bloco com uma
  thread por core (ou uma só, em single-core) e uma barra por core com a ocupação real
  (`/proc/stat`), além da nota ao vivo.
- A nota medida no pop-up volta para o cartão da página (GPU e CPU), incluindo o histórico de
  corridas do CPU.
- Ganchos de teste `LINUXEXPLODE_PREVIA`, `LINUXEXPLODE_RESOLUCAO` e `LINUXEXPLODE_THREADS`.

### Corrigido

- O `ComboBox` da resolução voltava ao primeiro item quando a janela era montada — a primeira
  medição chegava a correr a 320×180 mesmo com 1280×720 escolhido; a escolha é agora reaplicada
  antes de medir.
- O rodapé da página passou a mostrar a versão real (vinha fixa do `MainWindow.axaml` em v0.1.0).

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

[0.14.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.14.0
[0.12.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.12.0
[0.10.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.10.0
[0.3.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.3.0
[0.2.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.2.0
[0.1.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.1.0

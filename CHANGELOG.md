# Histórico de alterações

Formato segundo [Keep a Changelog](https://keepachangelog.com/pt-PT/1.1.0/) e
[versão semântica](https://semver.org/lang/pt/).

## [0.20.7] — 2026-10-25

### Adicionado

- **Ranking local** no EXPLODE BENCH: por cada alvo do histórico, a **mediana das últimas 3 corridas**,
  a variação entre elas e a diferença da última em relação à mediana. Quando a variação passa dos **3 %**
  o cartão avisa que a máquina não estava estável e que o número vale pouco — a nota de uma corrida
  isolada com turbo a arrefecer não é comparável com nada.
  - `--cli ranking` (texto e `--json`) e `--cli bench multi --repeticoes 3`, que corre o teste três
    vezes, devolve a mediana e a variação, e regista só a mediana no histórico.
- **Curva de latência de memória** (4 KB → 256 MB), com *pointer chasing* sobre uma permutação de
  blocos de 64 B: mostra os patamares L1/L2/L3 e o degrau da RAM, em nanossegundos por acesso.
  Gráfico com eixo X logarítmico desenhado à mão; `--cli bench latmem` dá a mesma série em texto/JSON.
  Nesta máquina (build Release): L1 1,1 ns · L2 2,9 ns · L3 9,0–9,4 ns · RAM 77–79 ns
  (256 MB: 88–89 ns).
- **Tarefa do mundo real** — descomprimir 48 MB com o resultado verificado por **SHA-256** e compilar
  um ficheiro C com 1 500 funções (fonte gerado, entra por stdin, compilador `cc`/`gcc`/`clang` do
  sistema, com `flatpak-spawn --host` dentro do sandbox). Sem rede e sem ficheiros temporários.
  `--cli bench real` devolve MB/s da descompressão e kB/s da compilação.
  Nesta máquina: **1 432 MB/s** dentro do Flatpak instalado e **515 MB/s** com o `zlib` do anfitrião —
  o número acompanha o binário que corre, não é uma constante da máquina; a compilação foi 3,69 s
  (102 kB/s de fonte).
- **Sessão gravada**: grava CPU, GPU, potência e frequência uma vez por segundo em
  `~/.local/share/linux-explode/sessoes/sessao-AAAAMMDD-HHMMSS.csv`, com escrita incremental, e no fim
  mostra o resumo (média, mínimo e máximo) e o gráfico da sessão inteira. `--cli gravar [segundos]`.

### Nota

- O CSV usa `;` e ponto decimal (`InvariantCulture`): é um ficheiro de dados, não a interface. Uma
  leitura que não exista fica com a célula **vazia**, nunca a zero — a primeira amostra de potência
  vem vazia porque o RAPL precisa de duas leituras.
- A compilação só entra na nota se houver compilador; sem ele o cartão diz porquê em vez de mostrar
  um tempo inventado.

## [0.20.5] — 2026-10-25

### Adicionado

- **Aba Dispositivos** (`Ctrl+7`; o Relatório passou para `Ctrl+8`), com três secções lidas do kernel:
  - **PCI Express** — velocidade e largura negociadas agora (`current_link_speed`/`current_link_width`)
    contra o máximo que o próprio dispositivo anuncia, geração (2,5 GT/s = 1.0 … 64 GT/s = 6.0) e
    débito útil teórico com a codificação certa (8b/10b até à geração 2, 128b/130b da 3 em diante).
    Só os dispositivos finais são avaliados: numa ponte o valor actual é o link que ela serve, e é
    normal não coincidir com o máximo — as pontes ficam escondidas atrás de uma opção.
  - **USB** — dispositivos com fabricante, produto, ID, versão, velocidade negociada (1,5 Mbps a
    20 Gbps), classes de interface, driver, potência pedida e porta. Sem descritores de texto, o nome
    é deduzido do driver (`btusb` → adaptador Bluetooth) em vez de ficar "Desconhecido".
  - **Áudio** — placas de som de `/proc/asound/cards`, fluxos PCM com estado, taxa, canais, formato,
    período e buffer (em ms), e os **xruns** da coluna `ERR` do `pw-top`, mais o quantum do servidor
    (`pw-metadata`) em milissegundos.
- **Cartão "Placa-mãe, chipset e memória"** na aba Sensores: temperatura do chipset, dos módulos de
  memória (SPD5118), do NVMe, da NIC e do Wi-Fi, com a origem identificada. O que não tem sensor
  (VRM/fases de alimentação) é dito por extenso em vez de aparecer a zero.
- **Ícone na bandeja** (`StatusNotifierItem`): tooltip com CPU, GPU e potência medidos, menu com
  *Abrir janela*, *Sensores*, *Dispositivos*, *Parar todo o stress*, *Fechar para a bandeja* e *Sair*.
  Fechar a janela passa a esconder para a bandeja; `Ctrl+Q` e *Sair* fecham de vez. Sem bandeja no
  ambiente, a aplicação arranca exactamente como antes.
- `--cli placa`, `--cli usb` e `--cli audio`, todos com `--json`.
- **2 capturas reais** novas em `docs/` (Dispositivos e placa-mãe) referenciadas no metainfo.

### Corrigido

- As temperaturas do chipset apareciam como se fossem da CPU: a lista de hwmon é a mesma, mas agora
  cada chip é atribuído à fonte certa (`prom21` → chipset, `spd5118` → memória, `r8169` → rede,
  `ath12k` → Wi-Fi) e a CPU/GPU ficam de fora do cartão da placa.
- `current_link_speed` é texto (`"16.0 GT/s PCIe"`), não um número: a leitura passou a extrair a taxa
  antes do `GT/s`, senão o cartão dizia "0 links".

### Nota

Os xruns são o contador acumulado do servidor PipeWire desde que cada nó arrancou — reiniciar o
PipeWire ou a aplicação cliente volta a zero. Numa ponte PCIe o "actual" não é comparável com o
máximo; por isso não é classificado como redução.

## [0.20.3] — 2026-10-25

### Adicionado

- **Cartão Alertas** na aba Sensores: CPU/GPU acima do limite, todas as ventoinhas a 0 RPM com a CPU
  quente e partições reais (ext4/xfs/btrfs/…) com menos de X GB livres. Limites editáveis no cartão e
  guardados em `~/.config/linux-explode/config.json`.
- **Notificações do sistema** opcionais (`notify-send`), com um aviso por regra a cada 15 minutos.
  Onde o `notify-send` não existe (Flatpak), a opção é desactivada e o cartão explica porquê — nada
  falha em silêncio.
- `--cli alertas` e `--cli alertas --json`.
- **Projecto de testes** `tests/LinuxExplode.Tests` (xUnit, 17 testes): histórico sem repetições,
  mediana e variação, exportar/importar sem duplicar, configuração persistida, medição de energia,
  regras de alerta e cargas de CPU. Passa a correr no CI a cada push.
- **4 capturas reais** em `docs/` referenciadas no metainfo (Sensores, Bench, Stress e tema claro).

### Corrigido

- Os alertas de disco não encontravam partições: `Util.Montagens()` está indexado por dispositivo e
  `Util.Espaco()` por ponto de montagem — o cruzamento passou a ser explícito.
- Os alertas de espaço ignoram agora `tmpfs`, `efivarfs`, `/run` e partições pequenas (`/boot`), que
  enchiam o cartão de ruído.

## [0.20.1] — 2026-10-25

### Adicionado

- **Ao vivo (60 s)** na aba Sensores: quatro gráficos de linha (temperatura da CPU, da GPU, potência e
  frequência média) com uma amostra por segundo; a linha passa a vermelho acima de 90 °C.
- **Throttling**: compara a frequência efectiva com o máximo do processador, conta episódios de queda
  sob carga, guarda o mínimo e o tempo total em queda. Quando o firmware não expõe contadores, o
  cartão di-lo em vez de mostrar zero.
- **Energia e custo**: energia da sessão (potência integrada no tempo), média em watts e projecção de
  €/dia e €/mês com tarifa editável, guardada em `~/.config/linux-explode/config.json`.
- **Histórico comparado** no bench: cada nota fica em `~/.local/share/linux-explode/resultados.json`
  com data e versão; os cartões mostram melhor, mediana e variação. Medições repetidas não são
  gravadas.
- **Exportar/importar resultados**: CSV e JSON a partir do cartão *Histórico de resultados*.
- **Tema claro/escuro**, **escala da interface** (85-135 %) e **atalhos** (`Ctrl+1…7`, `Ctrl+±`,
  `Ctrl+0`, `Ctrl+Q`). As cores passaram a `DynamicResource` e a cor de destaque do sistema foi
  substituída por um cinza da paleta.
- **`--json`** (ou `-j`) em todos os comandos da linha de comandos, com envelope
  `{aplicacao, versao, comando, quando, dados}`; `--cli relatorio --json` imprime o relatório completo.
- **ROADMAP.md** com as fases seguintes, critérios de aceitação e como retomar o trabalho.

### Corrigido

- O cartão de frequência média ficava a zero: o padrão de pesquisa `"cpu[0-9]*"` não é válido em .NET
  (só aceita `*` e `?`), pelo que nenhum núcleo era encontrado.
- A "energia desde o arranque" estava errada: o `energy_uj` desta plataforma dá a volta aos ~65,5 kJ
  (minutos a 200 W) e uma leitura única não serve. Passa a ser integrada no tempo e o rótulo diz
  "nesta sessão".

### Nota

O custo de energia é uma projecção ao preço que o utilizador indicar, sobre a média medida enquanto a
janela esteve aberta — não é uma factura. Está dito no cartão.

## [0.18.0] — 2026-10-25

### Adicionado

- **Aba STRESS**: aquece CPU, RAM, GPU e disco, isoladamente ou tudo em paralelo (burn-in), durante
  o tempo escolhido (1 a 30 minutos), com leitura ao vivo de temperatura, potência e ritmo.
  - CPU com quatro cargas: FMA vetorial (SIMD), inteiro, vírgula flutuante e tráfego de memória, com
    progresso incremental e resultado em unidades/s, GFLOP/s e GB/s.
  - RAM: enche 25/50/75 % da memória, escreve um padrão, relê e **verifica cada palavra**, em
    passagens contínuas, com as taxas de escrita e leitura medidas.
  - GPU: path tracer Vulkan em ciclo, com pontos por segundo.
  - Disco: escreve e relê a verificar (sequencial) e blocos de 4 KB aleatórios, com IOPS e contagem
    de bytes diferentes na releitura.
  - **Guarda térmica** (85/90/95/100 °C) lida quatro vezes por segundo; quando dispara, o teste pára
    e o resultado diz qual sensor e a que temperatura.
- **Registo** das últimas entradas e **Guardar relatório** em JSON (`~/linux-explode-relatorios/`).
- `--cli stress cpu|ram|gpu|disco|tudo [segundos] [--threads N] [--carga …] [--limite N]`.

### Corrigido

- O progresso da carga de CPU só aparecia no fim (as threads somavam o contador ao terminar); passa a
  publicar por lotes, com taxa em M/s calculada entre amostras.
- A carga vetorial gerava infinito ao fim de poucos lotes e a thread saía antes do tempo pedido; a
  cadeia FMA passou a ser estável e o acumulador é consumido (`GC.KeepAlive`) para o JIT não poder
  eliminar as contas. Ver nota abaixo.
- Chegar ao fim do tempo pedido conta como conclusão normal; só a paragem manual e a guarda térmica
  marcam o teste como interrompido.

### Nota sobre números

Os 207 GFLOP/s que esta carga mostrava antes eram falsos: o laço de FMA era eliminado pelo JIT (o
acumulador nunca era consumido) e o contador somava iterações que não aconteceram. O valor real é
**~82 GFLOP/s** (5 126 M FMA vetoriais/s, 16 threads). Números que se apresentam como medição têm de
ser medidos — foi corrigido.

## [0.16.0] — 2026-10-25

### Alterado

- **O histórico do cartão mostra um resultado por tipo de teste**, não uma lista: `Multi-core` e
  `Single-core` têm cada um a sua linha, com a nota, as threads e a hora da última medição. Deixa de
  acumular linhas a cada corrida (antes acrescentava até 6).
- Cada linha só é substituída quando o resultado é **diferente** (dois algarismos significativos:
  5 603 e 5 598 são o mesmo resultado). A nota grande acompanha a mesma regra e mostra sempre o
  último resultado distinto.
- As threads passam a ser normalizadas antes de abrir o pop: `0` significa todos os cores, e a linha
  do histórico indica sempre o número real usado (antes podia mostrar `0T`).
- `LINUXEXPLODE_AUTOTEST=cpu` abre a prévia de CPU pela página e respeita `LINUXEXPLODE_THREADS`, para
  se poder testar o cartão (multi e single) sem cliques.

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

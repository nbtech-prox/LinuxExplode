# Histórico de alterações

Formato segundo [Keep a Changelog](https://keepachangelog.com/pt-PT/1.1.0/) e
[versão semântica](https://semver.org/lang/pt/).

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

[0.1.0]: https://github.com/nbtech-prox/LinuxExplode/releases/tag/v0.1.0

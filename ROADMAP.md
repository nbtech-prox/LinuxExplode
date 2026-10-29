# ROADMAP — LINUX EXPLODE

Documento de trabalho: o que está feito, o que vem a seguir e como cada fase é dada por concluída.
As versões sobem de **2 em 2 no último número** (0.20.1 → 0.20.3 → 0.20.5 …) e cada uma sai com notas,
instaladores, repositório Flatpak actualizado e CI verde.

- **Estado actual:** 0.20.13 (relatório completo em HTML, histórico em SQLite com pesquisa/período e
  débito real cliente/servidor, para além dos ciclos de stress, auto-teste S.M.A.R.T., VRAM, suspender/
  retomar, ranking com mediana, latência de memória, tarefa real, sessões em CSV, Dispositivos,
  placa-mãe, bandeja, STRESS, gráficos ao vivo, throttling, energia, temas, `--json`, alertas e
  125 testes).

## Fase 1 — 0.20.1 (feito)

| # | O que entrou | Onde |
|---|---|---|
| 1 | Gráficos ao vivo de 60 s (temperaturas, potência, frequência) | Sensores → cartão *Ao vivo* |
| 2 | Deteção de throttling (frequência efectiva vs máxima, episódios, mínimo) | Sensores → cartão *Throttling* |
| 5 | Energia da sessão e custo estimado (€/dia, €/mês) com tarifa editável | Sensores → cartão *Energia e custo* |
| 8 | Histórico comparado por benchmark (melhor, mediana, variação) | Explode Bench, todos os cartões |
| 13 | Exportar CSV/JSON e importar resultados de outra máquina | Bench → cartão *Histórico de resultados* |
| 22 | Tema claro/escuro, escala da interface e atalhos | Barra lateral + `Ctrl+1…8`, `Ctrl+±`, `Ctrl+0`, `Ctrl+Q` |
| 23 | `--json` em todos os comandos da linha de comandos | `--cli … --json` |

**Critério de conclusão:** cada item verificado com valores reais nesta máquina e registado no
CHANGELOG; `--json` validado com `json.load` em todos os comandos.

---

## Fase 2 — 0.20.3 · Alertas e confiança nos dados (feito)

Objectivo: a aplicação avisa quando algo está mal, em vez de esperar que o utilizador olhe.

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 3 ✅ | Alertas por sensor (CPU/GPU quentes, ventoinhas a zero, pouco espaço) | Um sensor avariado só se nota a olhar | Limites na interface, valores reais dos sensores, `--cli alertas` | S |
| 24 ✅ | Notificações do sistema para limites | Avisa mesmo com a janela escondida | `notify-send` detectado, 1 aviso por regra a cada 15 min, desligado por omissão | S |
| 28 ✅ | `appstreamcli validate` limpo + 4 capturas reais | É o que o gestor de aplicações mostra | Validador sem avisos; capturas em `docs/` no metainfo | S |
| 26 ✅ | Testes automatizados (xUnit) | Antes o CI só compilava | 17 testes de `Historico`, `Config`, energia, alertas e `StressCpu`, a correr no CI | M |

---

## Fase 3 — 0.20.5 · Monitorização profunda (feito)

| # | Item | Aceitação | Como foi verificado nesta máquina |
|---|---|---|---|
| 4 ✅ | Temperatura de chipset/VRM e link PCIe | hwmon classificado + link actual/máximo | chipset `prom21_xhci` 52,8 e 60,1 °C; RAM SPD5118 33,3/33,8 °C; NIC 38,5 °C; GPU 03:00.0 a PCIe 4.0 x16 ≈ 31,5 GB/s úteis; **VRM não tem sensor** e o cartão di-lo (não se usa `lspci`: os mesmos dados estão em `/sys`, e o sandbox não tem pciutils) |
| 6 ✅ | USB e áudio (latência, xruns) | Lista USB com velocidade; xruns medidos | 7 dispositivos (1,5 Mbps → 480 Mbps) e 12 concentradores/controladores; com um PCM aberto: `Portal · pcm0p — running, 48000 Hz, 2 canais, S16_LE, período 2,7 ms, buffer 170,7 ms`; xruns da coluna ERR do `pw-top` (não de `/proc/asound`, que não tem contador) |
| 21 ✅ | Ícone na bandeja com temperaturas e "parar tudo" | TrayIcon + menu funcional | `StatusNotifierItem` registado no noctalia; tooltip lido por DBus: `LINUX EXPLODE · CPU 43 °C · GPU 38 °C · 24 W`; menu com *Parar todo o stress* (activo só com sessões a correr) e *Sair* |

**Critério de conclusão:** `--cli placa|usb|audio` (texto e `--json`) devolvem o mesmo que a interface,
capturas reais em `docs/captura-dispositivos.png` e `docs/captura-placa.png`, e 73 testes verdes.

---

## Fase 4 — 0.20.7 · Benchmarks com mais sinal (feito)

| # | Item | Aceitação | Como foi verificado |
|---|---|---|---|
| 9 ✅ | Ranking local com mediana de 3 corridas e aviso de variância | 3 corridas, mediana, desvio; aviso quando a variância > 3 % | cartão *Ranking local* + `--cli ranking` com a mediana das últimas 3 e a variação de cada alvo; `--cli bench multi --repeticoes 3` corre três vezes e regista a mediana. O aviso só aparece a partir de 3 corridas, senão seria ruído |
| 11 ✅ | Latência de memória por tamanho de bloco (curvas) | Série de 4 KB a 256 MB, ns por acesso, gráfico | 9 pontos em ~4 s: **L1 1,1 ns · L2 2,9 ns · L3 9,0–9,4 ns · RAM 77–79 ns** (256 MB 88–89 ns), monótona; gráfico com eixo X logarítmico e `--cli bench latmem` |
| 12 ✅ | Carga "do mundo real" (descomprimir + compilar) | Tempo de uma tarefa fixa, repetível, sem rede | 48 MB descomprimidos (**1 432 MB/s** no Flatpak, 515 MB/s no anfitrião) com SHA-256 igual ao original; 1 500 funções C compiladas com `cc` em **3,69 s** (102 kB/s de fonte). Sem compilador, o cartão diz o motivo |
| 9 ✅ | (verificação) mediana de 3 corridas | A mediana é o número que conta | `--cli bench multi --repeticoes 3`: 5 699 / 5 809 / 5 812 pts → **mediana 5 809, variação 1,9 %**; o alvo "compilar 1 500 funções" mostrou o aviso com 5,0 % de variação, como deve |
| 18 ✅ | Sessão de monitorização gravada em CSV com gráfico no fim | Botão gravar/parar; CSV com todas as amostras; gráfico no fim | cartão *Sessão gravada* em Sensores e `--cli gravar 6`: 6 amostras, CSV com cabeçalho e célula vazia onde não houve leitura, resumo (média/min/max) e quatro gráficos da sessão inteira |

---

## Fase 3.1 — 0.20.9 · Histórico que sobrevive no Flatpak (feito)

| Item | Porquê | Como foi verificado |
|---|---|---|
| Persistir `.local/share/linux-explode` no sandbox | O `$HOME` do Flatpak é temporário: histórico e sessões desapareciam a cada arranque | `--persist=.local/share/linux-explode` no manifesto; dois arranques seguidos dentro do Flatpak, o segundo `--cli ranking` mostra a corrida do primeiro. O CI passa a falhar se a linha desaparecer |

Esta correcção adiantou-se à Fase 5 (que passa a 0.20.11) por ser um defeito visível na versão
instalada: sem ela o ranking e o histórico comparado não funcionavam no Flatpak.
- **Regra de ouro:** nenhum valor apresentado é estimado. Se um sensor não existe, o cartão diz isso.
- **Idioma:** interface, documentação e notas em pt-PT.

---

## Fase 5 — 0.20.11 · Stress e discos (feito)

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 15 ✅ | Ciclos de stress agendados (N ciclos, pausa entre eles) | Testes de estabilidade longos sem estar à frente | Configuração de ciclos; relatório agregado (picos, falhas) — `--cli ciclos`, mediana/min/max por métrica | M |
| 16 ✅ | Auto-teste SMART (curto/longo) a partir da aba Discos | Diz se o disco está saudável a sério | `smartctl -t`; progresso e resultado do teste anterior — botões por disco, percentagem e histórico | S |
| 14 ✅ | Enchimento de VRAM (alocar até falhar, escrever e verificar) | O único subsistema sem stress hoje | Alocação em Vulkan com limite configurável, erro claro quando falha — `--cli vram`, pico confirmado em `/sys` | G |
| 17 ✅ | Teste de suspender/retomar | Portáteis falham aqui | Suspender, acordar, verificar sensores e serviços — três relógios, `--simular` para a conta zero | M |

**Critério de conclusão:** cada item verificado nesta máquina — ciclos com números reais e tabela na
interface, auto-teste com o histórico do NVMe e do HDD, VRAM com 2 GB alocados e relidos, suspensão
com a simulação coerente (o teste real precisa do `rtcwake` no sudoers para acordar sozinho) — e as
quatro capturas em `docs/`.

---

## Fase 6 — 0.20.13 · Dados e integração (feito em parte)

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 19 ✅ | Relatório único (sistema + sensores + benchmarks + stress) em HTML | Um ficheiro para arquivar ou partilhar | HTML autónomo com gráficos e data; botão na aba Relatório — feito, 16,9 KB com sete secções | M |
| 20 ✅ | Histórico em SQLite com pesquisa e gráficos | O JSON actual não escala para meses de amostras | Esquema + migração do JSON + consultas por período — feito, `historico.db` com procura e período; JSON migrado uma vez | M |
| 10 ✅ | Benchmark de rede real (cliente/servidor próprio) | Mede a rede a sério, não só latência | Modo servidor e cliente; relatório de débito — feito, `--cli netbench`, teste local no loopback | M |
| 25 | Assinar o repositório Flatpak com GPG | Deixa de precisar de `--no-gpg-verify` | Chave de release, `summary.sig`, cliente a validar sem flag — **passa para a 0.20.15** (precisa da chave do Nuno) | M |
| 27 | Publicar na Flathub | Instalação a um clique para qualquer pessoa | Revisão aceite; builds reprodutíveis; capturas e metainfo válidos — **passa para a 0.20.15** (revisão externa) | G |

---

## Fase 7 — 0.20.15 · Assinatura e distribuição (próxima)

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 25 | Assinar o repositório Flatpak com GPG | Deixa de precisar de `--no-gpg-verify` na instalação | Chave de release criada e guardada como segredo no CI; `summary.sig` e commits assinados; cliente a validar sem a flag | M |
| 27 | Publicar na Flathub | Instalação a um clique para qualquer pessoa | Metainfo e capturas válidos (já validados), builds reprodutíveis, pedido de revisão aceite | G |

A chave GPG é do dono do projecto: gerar, exportar a pública para `docs/` e guardar a privada nos
segredos do repositório são passos que exigem o aval do Nuno. A Flathub é uma revisão externa — o
trabalho aqui é preparar o pacote e responder aos revisores.

## Fora de âmbito (por agora)

- **Overclock/undervolt**: escrever em `/sys` ou MSR é risco de instabilidade — a aplicação mede e
  avisa, não altera tensões.
- **Escrita em discos do sistema** para testar saúde: o stress usa um ficheiro temporário próprio.
- **Telemetria ou contas**: nada sai da máquina; não há servidor nem recolha de dados.
- **Sensores de ECC** (item 7 da lista original): depende de hardware de servidor; só entra se houver
  máquina para testar.

---

## Como retomar numa sessão nova

1. `git log --oneline -5` e `git status -sb` — ver o que está commitado e por publicar.
2. Ler a secção da fase actual neste documento e o CHANGELOG da última versão.
3. `dotnet build -c Release` e `--cli info` devem dizer a versão do topo do CHANGELOG;
   `dotnet test tests/LinuxExplode.Tests -c Release` tem de passar (99 testes na 0.20.7).
4. Verificar o que está instalado: `flatpak info pt.linuxexplode.LinuxExplode` e
   `flatpak --user remote-info linuxexplode pt.linuxexplode.LinuxExplode`.
5. Antes de publicar: `dotnet build`, `--cli bench`, `--cli stress … --json`, e o pop a abrir; depois
   tag `vX.Y.0` (o workflow trata do release e do repositório Flatpak).

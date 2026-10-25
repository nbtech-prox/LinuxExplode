# ROADMAP — LINUX EXPLODE

Documento de trabalho: o que está feito, o que vem a seguir e como cada fase é dada por concluída.
As versões sobem de **2 em 2** na parte menor (0.18.0 → 0.20.0 → 0.22.0 …) e cada uma sai com notas,
instaladores, repositório Flatpak actualizado e CI verde.

- **Estado actual:** 0.20.0 (aba STRESS, gráficos ao vivo, throttling, energia, histórico comparado,
  temas, `--json`).
- **Regra de ouro:** nenhum valor apresentado é estimado. Se um sensor não existe, o cartão diz isso.
- **Idioma:** interface, documentação e notas em pt-PT.

---

## Fase 1 — 0.20.0 (feito)

| # | O que entrou | Onde |
|---|---|---|
| 1 | Gráficos ao vivo de 60 s (temperaturas, potência, frequência) | Sensores → cartão *Ao vivo* |
| 2 | Deteção de throttling (frequência efectiva vs máxima, episódios, mínimo) | Sensores → cartão *Throttling* |
| 5 | Energia da sessão e custo estimado (€/dia, €/mês) com tarifa editável | Sensores → cartão *Energia e custo* |
| 8 | Histórico comparado por benchmark (melhor, mediana, variação) | Explode Bench, todos os cartões |
| 13 | Exportar CSV/JSON e importar resultados de outra máquina | Bench → cartão *Histórico de resultados* |
| 22 | Tema claro/escuro, escala da interface e atalhos | Barra lateral + `Ctrl+1…7`, `Ctrl+±`, `Ctrl+0`, `Ctrl+Q` |
| 23 | `--json` em todos os comandos da linha de comandos | `--cli … --json` |

**Critério de conclusão:** cada item verificado com valores reais nesta máquina e registado no
CHANGELOG; `--json` validado com `json.load` em todos os comandos.

---

## Fase 2 — 0.22.0 · Alertas e confiança nos dados

Objectivo: a aplicação avisa quando algo está mal, em vez de esperar que o utilizador olhe.

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 3 | Alertas por sensor (ventoinha parada, tensão fora da faixa, temperatura acima do limite) | Um sensor avariado só se nota a olhar | Limite configurável por sensor; estado na interface sem notificações do sistema | S |
| 24 | Notificações do sistema para limites (temperatura, disco cheio, SMART) | Avisa mesmo com a janela escondida | `notify-send`/libnotify detectado; nada é enviado sem estar ligado nas definições | S |
| 28 | `appstreamcli validate` sem avisos + capturas reais por página | É o que o gestor de aplicações mostra | Validador limpo; 4+ capturas em `docs/` referenciadas no metainfo | S |
| 26 | Testes automatizados (xUnit) dos motores | Hoje o CI só compila | Testes de `ChaveDoResultado`, `Historico`, `MonitorThrottle`, `StressCpu` (durações curtas) no CI | M |

---

## Fase 3 — 0.24.0 · Monitorização profunda

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 4 | Temperatura de chipset/VRM e velocidade do link PCIe | Fecha o retrato térmico da placa | Leitura de hwmon + `lspci -vv` para link actual/máximo | S |
| 6 | Dispositivos USB e áudio (latência, xruns) | Diagnóstico de periféricos e de áudio em tempo real | Lista USB com velocidade; xruns de `/proc/asound/*` | M |
| 21 | Ícone na bandeja com temperaturas e "parar tudo" | Monitorização sem janela aberta | TrayIcon do Avalonia; menu com as temperaturas e paragem do stress | M |

---

## Fase 4 — 0.26.0 · Benchmarks com mais sinal

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 9 | Ranking local com mediana de 3 corridas e aviso de variância | Uma corrida isolada engana | 3 corridas, mediana, desvio; aviso quando a variância > 3 % | S |
| 11 | Latência de memória por tamanho de bloco (curvas) | Mostra o efeito das caches | Série de 4 KB a 256 MB, ns por acesso, gráfico | M |
| 12 | Carga "do mundo real" (descomprimir + compilar) | Aproxima o bench do uso diário | Tempo de uma tarefa fixa, repetível, sem rede | M |
| 18 | Sessão de monitorização gravada em CSV com gráfico no fim | Ver o que aconteceu durante um jogo ou uma compilação | Botão gravar/parar; CSV com todas as amostras; gráfico no fim | M |

---

## Fase 5 — 0.28.0 · Stress e discos

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 15 | Ciclos de stress agendados (N ciclos, pausa entre eles) | Testes de estabilidade longos sem estar à frente | Configuração de ciclos; relatório agregado (picos, falhas) | M |
| 16 | Auto-teste SMART (curto/longo) a partir da aba Discos | Diz se o disco está saudável a sério | `smartctl -t`; progresso e resultado do teste anterior | S |
| 14 | Enchimento de VRAM (alocar até falhar, escrever e verificar) | O único subsistema sem stress hoje | Alocação em Vulkan com limite configurável, erro claro quando falha | G |
| 17 | Teste de suspender/retomar | Portáteis falham aqui | Suspender, acordar, verificar sensores e serviços | M |

---

## Fase 6 — 0.30.0 · Dados e integração

| # | Item | Porquê | Aceitação | Esforço |
|---|---|---|---|---|
| 19 | Relatório único (sistema + sensores + benchmarks + stress) em HTML | Um ficheiro para arquivar ou partilhar | HTML autónomo com gráficos e data; botão na aba Relatório | M |
| 20 | Histórico em SQLite com pesquisa e gráficos | O JSON actual não escala para meses de amostras | Esquema + migração do JSON + consultas por período | M |
| 10 | Benchmark de rede real (cliente/servidor próprio) | Mede a rede a sério, não só latência | Modo servidor e cliente; relatório de débito | M |
| 25 | Assinar o repositório Flatpak com GPG | Deixa de precisar de `--no-gpg-verify` | Chave de release, `summary.sig`, cliente a validar sem flag | M |
| 27 | Publicar na Flathub | Instalação a um clique para qualquer pessoa | Revisão aceite; builds reprodutíveis; capturas e metainfo válidos | G |

---

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
3. `dotnet build -c Release` e `--cli info` devem dizer a versão do topo do CHANGELOG.
4. Verificar o que está instalado: `flatpak info pt.linuxexplode.LinuxExplode` e
   `flatpak --user remote-info linuxexplode pt.linuxexplode.LinuxExplode`.
5. Antes de publicar: `dotnet build`, `--cli bench`, `--cli stress … --json`, e o pop a abrir; depois
   tag `vX.Y.0` (o workflow trata do release e do repositório Flatpak).

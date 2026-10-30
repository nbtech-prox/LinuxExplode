# Glossário

Termos que aparecem na aplicação, explicados sem pressupor conhecimentos técnicos.

## Sistema e ficheiros

**Flatpak**
: Formato de aplicação empacotada para Linux. Traz as bibliotecas de que precisa dentro do pacote e
corre numa "sandbox" (uma caixa isolada), por isso funciona igual em qualquer distribuição.

**Sandbox**
: Caixa isolada onde a aplicação corre. Só vê o que lhe é permitido — no caso, `/sys` e `/proc` em
leitura, a pasta de dados própria e as pastas do MangoHud.

**`/sys` e `/proc`**
: Duas pastas especiais do Linux que não existem no disco: o kernel mostra lá o estado do hardware em
tempo real. É de lá que vem quase tudo o que a aplicação mostra.

**DMI / BIOS**
: Informação que a placa-mãe guarda sobre si própria: fabricante, modelo, versão da BIOS.

**Root**
: O utilizador com todos os poderes ("administrador"). A aplicação evita precisar dele.

## Energia e temperatura

**hwmon**
: O sistema do kernel que expõe sensores de temperatura, ventoinhas, tensões e potência.

**RAPL**
: Contadores de energia do processador (`intel-rapl` no Linux, também em AMD). Dão watts e energia
acumulada do socket da CPU. O kernel esconde-os por omissão (`0400 root`) — daí o script
`permitir-energia.sh`.

**Socket**
: A "tomada" onde o processador encaixa na placa-mãe. O RAPL mede o consumo do socket, não da tomada
da parede.

**Throttling**
: Quando o processador baixa a frequência de propósito — por temperatura, consumo ou limite do
fabricante — para não se estragar. Perde-se desempenho.

**Turbo**
: Frequência acima da nominal, usada durante pouco tempo quando há folga de temperatura e energia.

## Discos

**SSD / HDD / NVMe**
: SSD guarda em memória eletrónica (rápido, sem peças móveis); HDD guarda em pratos magnéticos (mais
lento, com peças móveis); NVMe é um SSD ligado directamente ao barramento PCIe (o mais rápido).

**S.M.A.R.T.**
: Auto-diagnóstico do disco. Conta setores maus, horas de funcionamento, temperatura e o resultado de
testes. Precisa do programa `smartctl`.

**Setor realocado**
: Um pedaço do disco que se estragou e foi substituído por outro de reserva. Zero é bom; a subir
depressa é sinal de fim de vida.

**Auto-teste**
: O disco a testar-se a si próprio. Curto = minutos; longo = horas, porque varre a superfície toda.

## Gráficos e desempenho

**Vulkan**
: A API moderna de acesso à placa gráfica no Linux. O benchmark usa-a para correr o mesmo
renderizador na GPU.

**Path tracing**
: Técnica de renderização que simula o caminho da luz. É pesada de propósito — serve para medir.

**Ray tracing**
: Cálculo de intersecções de raios feito por unidades próprias da placa (RT cores). Mais realista e
mais rápido do que fazer o mesmo no processador.

**Amostras por pixel (spp)**
: Quantos raios são lançados por cada pixel. Mais amostras = imagem mais limpa e mais lenta.

**FPS / 1 % low**
: Fotogramas por segundo. O **1 % low** é a velocidade nos 1 % piores momentos — é o que sentes como
engasgo.

**MangoHud**
: Camada que desenha informações em cima dos jogos e grava um CSV por sessão. A app lê esses CSV.

**GFLOP/s**
: Mil milhões de operações de vírgula flutuante por segundo. Medida de cálculo bruto.

**Mediana**
: O valor do meio de uma lista ordenada. Menos sensível a picos do que a média — por isso a app a usa
para comparar corridas.

**Variação**
: Quanto as últimas corridas diferem entre si. Pouca variação = resultado de confiança.

## Rede

**Latência**
: Tempo de ida e volta de um pacote, em milissegundos. Para jogos e chamadas, importa mais do que a
velocidade.

**Jitter**
: Quanto a latência oscila. Jitter alto = som aos cortes e jogos instáveis.

**Perda de pacotes**
: Percentagem de pacotes que não chegaram. Deve ser 0 %.

**Mbps / MB/s**
: Megabits por segundo (redes) vs megabytes por segundo (ficheiros). 1 MB/s ≈ 8 Mbps.

## Áudio

**PipeWire**
: O servidor de áudio moderno do Linux. Gere os fluxos de som e conta as falhas.

**Xrun**
: Uma falha de áudio: o servidor não entregou os dados a tempo e ouve-se um estalo.

**Quantum**
: O tamanho do bloco de processamento do PipeWire. Blocos pequenos = menos latência, mais risco de
xruns.

## Segurança e distribuição

**GPG**
: Sistema de assinatura digital. O repositório Flatpak é assinado: o teu sistema recusa uma
actualização com assinatura inválida.

**SHA-256**
: Impressão digital de um ficheiro. Se um byte mudar, o valor muda — serve para confirmar que o
download não veio corrompido.

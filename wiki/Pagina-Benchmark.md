# Página Benchmark

![Página Benchmark](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-benchmark.png)

Mede **quão rápido** é o computador — e guarda os resultados para comparares ao longo do tempo.

## A nota de GPU (Vulkan)

O primeiro cartão corre o mesmo "path tracer" (um renderizador 3D que simula a luz) na placa gráfica,
em Vulkan:

- Escolhes a GPU na lista (dedicada ou integrada).
- **Testar GPU** abre um pop-up que renderiza a cena **ao vivo**, com a câmara a orbitar, e mede ao
  mesmo tempo.
- No pop-up escolhes a resolução, de **320×180 até 4K**.
- **Testar ray tracing** faz o mesmo com as unidades de *ray tracing* do hardware (precisa de
  `VK_KHR_ray_query`; se não existir, o botão responde "indisponível").

A nota é **amostras por segundo ÷ 1000**: sobe com a placa gráfica, não com o processador. Nesta
máquina, uma RX 6700 XT dá cerca de **13 milhões**.

> **Em linguagem simples:** a placa gráfica desenha a mesma imagem milhões de vezes; a nota diz
> quantas conseguiu. Quanto maior, melhor e mais rápida é a placa.

## FPS de jogos (MangoHud)

O cartão **FPS · sessão do MangoHud** lê os ficheiros CSV que o MangoHud grava quando jogas. Mostra
**FPS médio**, **1 % low**, **0,1 % low** e **frametime médio**, com um gráfico dos frametimes.

- **1 % low** é a velocidade nos momentos piores — é o que sentes como "engasgos", mesmo com média
  alta.
- Em Wayland nenhuma aplicação vê os frames de outra; o MangoHud é a forma honesta de medir.
- A app diz como configurar o MangoHud quando não encontra sessões (ver [Instalar](Instalar)).

## A nota de CPU

O cartão **CPU · Benchmark** desenha a mesma cena no processador, sempre a 960×540 com 1024 amostras
por pixel:

- **Multi-core** usa uma thread por núcleo; **Single-core** usa uma só.
- A nota é **amostras por segundo ÷ 10 000** — comparável entre máquinas com a mesma versão.

## Memória, disco e tarefas reais

- **RAM** — banda (GB/s) e latência em ns, com uma **curva de latência por tamanho de bloco**: mostra
  L1, L2, L3 e RAM. É a forma de ver se a cache está a funcionar como devia.
- **Disco** — escrita e leitura sequencial, 4K aleatório e latência.
- **Tarefa real** — descomprimir 48 MB (com verificação SHA-256 do resultado) e compilar 1500 funções
  C. Sem rede; se o resultado da descompressão não bater certo, o número é descartado.

## Ranking e histórico

- **Ranking local** — para cada alvo, a **mediana das últimas 3 corridas**, a variação entre elas e a
  diferença da última face à mediana.
- Acima de **3 %** de variação, o cartão avisa que a máquina não estava estável e que a nota vale
  pouco. (É diferente de "o teste falhou".)
- **Histórico de resultados** — procura por texto, filtro por período (7/30/90 dias), lista e gráfico;
  exporta para CSV ou JSON e importa de outra máquina.

```bash
LinuxExplode --cli bench multi ram disco    # CPU + RAM + disco
LinuxExplode --cli bench gpu                # path tracer Vulkan
LinuxExplode --cli bench rt                 # ray tracing
LinuxExplode --cli bench real               # descomprimir + compilar
LinuxExplode --cli ranking                  # mediana das últimas 3, por alvo
```

## O que significam os números

- **Nota** — número relativo: só faz sentido comparado com outra corrida **do mesmo teste** na mesma
  versão.
- **Variação** — quanto as últimas corridas diferem entre si. Pouca variação = resultado de confiança.
- **Mediana** — o valor do meio: menos sensível a um pico isolado do que a média.

> **Em linguagem simples:** um número sozinho não é um resultado. A app mostra sempre a mediana, a
> variação e quantas corridas foram feitas — é isso que torna o número utilizável.

## Limites honestos

- Sem driver Vulkan (máquina virtual, servidor), a página di-lo e não inventa uma nota.
- Os dispositivos de software (llvmpipe) são ignorados: medir GPU no CPU não diz nada.
- A nota de ray tracing **não é comparável** com a de compute (a cena é maior).
- Corridas com a máquina ocupada dão valores baixos. Fecha o que estiver a correr.

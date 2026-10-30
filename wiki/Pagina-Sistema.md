# Página Sistema

![Página Sistema](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-sistema.png)

Responde à pergunta **"que computador é este?"**: o sistema operativo, o processador, a memória, a
placa-mãe, as placas gráficas e um teste de suspender/retomar.

## O que vês

No topo, quatro números grandes:

| Número | O que é |
|---|---|
| **16 threads** (com "8 núcleos" por baixo) | quantas tarefas o processador consegue correr ao mesmo tempo |
| **60,4 GB** RAM | memória instalada, com a parte em uso por baixo |
| **3,89 GHz** | frequência actual, com o máximo (turbo) por baixo |
| **4 h 7 m** | há quanto tempo o computador está ligado |

Depois, um cartão por assunto:

- **Sistema operativo** — distribuição, kernel, arquitectura (x64), modelo da máquina.
- **Processador** — modelo, núcleos/threads, frequência actual e máxima, cache L3.
- **Memória** — instalada, em uso, disponível, swap e **cada módulo**: capacidade, geração
  (DDR5), velocidade (5200 MT/s), fabricante, part number, data de fabrico.
- **Placa-mãe e BIOS** — fabricante, modelo, versão e data da BIOS.
- **Placa de vídeo** — modelo, endereço PCI, driver e — nas AMD — temperatura, uso e VRAM.
- **Suspender e retomar** — testa se a máquina dorme e acorda, e mede quanto tempo dormiu.

## O que significam os números

- **Threads vs núcleos** — o processador tem 8 núcleos físicos, mas cada um atende 2 tarefas ao mesmo
  tempo (16 threads). É como 8 funcionários com 2 telefones cada.
- **Frequência** — GHz é "mil milhões de operações por segundo". O valor sobe e desce conforme a
  carga e a temperatura; o máximo é o turbo que o fabricante permite.
- **Cache L3** — memória muito rápida dentro do processador, que evita ir à RAM.
- **Módulos de memória** — a app lê o SPD do próprio módulo (uma pequena memória que o módulo tem),
  por isso mostra o fabricante e a data, não uma estimativa.

## O que podes fazer

- **Actualizar** os dados depois de ligar hardware novo.
- **Testar suspender/retomar**: podes escolher a duração e usar **Simular (sem dormir)** para ver a
  conta dos relógios sem a máquina dormir.
- Ver o histórico do último teste de suspensão.

## O que a app não faz

- Não altera nada no sistema: é só leitura.
- Num portátil ou numa máquina virtual, alguns campos aparecem como "—" — porque o kernel não os
  expõe. Nesse caso a app diz porquê em vez de inventar.

> **Em linguagem simples:** esta é a "ficha técnica" do teu PC. Se um dia precisares de pedir ajuda
> num fórum, esta página tem quase tudo o que te vão perguntar.

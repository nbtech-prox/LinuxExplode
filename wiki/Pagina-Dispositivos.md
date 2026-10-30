# Página Dispositivos

![Página Dispositivos](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-dispositivos.png)

Responde a três perguntas que antes só se respondiam no terminal: **o link desta placa está no
máximo?**, **a que velocidade é que este periférico ligou?** e **o áudio está a falhar?** Tudo lido do
kernel, sem root e sem `lspci`/`lsusb`.

## PCI Express

Uma linha por dispositivo, com:

| Coluna | O que é |
|---|---|
| **Endereço** | `03:00.0` — a "morada" da placa no barramento |
| **Link em uso** | geração e largura negociadas agora (`PCIe 4.0 x16`) |
| **Classe** | que tipo de dispositivo é (placa gráfica, áudio, armazenamento, rede…) |
| **Nome** | fabricante e modelo |
| **Débito útil** | quantos GB/s isso dá na prática (`≈ 31,5 GB/s`) |

A caixa **Mostrar as pontes** revela os "switchs" que ligam vários dispositivos. Por omissão ficam
escondidos: numa ponte o valor em uso é o link que ela está a servir, não uma capacidade — compará-lo
com o máximo dava falsos avisos.

> **Em linguagem simples:** o PCI Express é a auto-estrada entre a placa gráfica e o processador. A
> coluna "link em uso" diz quantas faixas estão abertas e a que velocidade; se disser `x8` em vez de
> `x16`, a placa está a usar metade das faixas (slot, riser ou poupança de energia).

## USB

Cada dispositivo ligado, com fabricante, produto, ID, versão, **velocidade negociada** (1,5 Mbps, 12,
480 Mbps, 5, 10 ou 20 Gbps), classes das interfaces, driver e a potência que pede.

A velocidade é a que o kernel reporta — **não é um teste de débito**, e o cartão di-lo. Dispositivos
sem descritores de texto não ficam "Desconhecido": o nome é deduzido do driver (um dongle com `btusb`
aparece como *Adaptador Bluetooth*).

## Áudio

- **Placas** — índice, nome, driver e barramento.
- **Fluxos PCM** — o que está a tocar ou gravar, com taxa, canais, formato, **período** e **buffer** em
  milissegundos.
- **Xruns** — falhas de áudio contadas pelo servidor PipeWire (coluna `ERR` do `pw-top`). É um
  contador acumulado desde que o nó arrancou; volta a zero quando o servidor reinicia.
- **Quantum** — o tamanho do bloco de processamento do PipeWire (1024 frames a 48 kHz = 21,3 ms).

> **Em linguagem simples:** se ouvires estalidos no som, vê os **xruns**. Se subirem enquanto ouves
> música, o sistema não está a conseguir alimentar a placa a tempo. A app distingue "não há xruns" de
> "não consegui perguntar" — o segundo aparece escrito, não como zero.

## O que podes fazer

- Confirmar se uma placa gráfica está a usar todas as faixas do slot.
- Descobrir a que velocidade ligou um disco USB ou uma pen.
- Ver se o áudio está a falhar e com que latência.

```bash
LinuxExplode --cli placa
LinuxExplode --cli usb
LinuxExplode --cli audio
```

## Limites honestos

- O débito é **teórico**: é a largura de banda do link, não uma medição de transferência real.
- O máximo é o que o próprio dispositivo anuncia, não o do slot.
- Sem PipeWire (ou dentro de um sandbox sem socket), os xruns aparecem como "não medidos".
- Dispositivos USB que não expõem S.M.A.R.T. não mostram saúde.

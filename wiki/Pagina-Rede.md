# Página Rede

![Página Rede](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-rede.png)

Duas coisas diferentes, no mesmo sítio:

1. **Diagnóstico de rede** — "a internet está boa?" (teste rápido, ~15 segundos).
2. **Débito real** — "a minha rede local faz quantos MB/s?" (entre dois computadores teus).

## Diagnóstico de rede

Carregas em **Testar rede** e a app mede:

| Medida | O que significa |
|---|---|
| **Latência** (router e internet) | o tempo de ida e volta de um pacote, em ms — quanto menor, melhor |
| **Jitter** | quanto a latência varia; alto = chamadas e jogos instáveis |
| **Perda de pacotes** | percentagem de pacotes que não chegaram — deve ser 0 % |
| **DNS** | tempo para traduzir um nome (`exemplo.pt`) num endereço |
| **Download / upload** | velocidade em Mbps, medida com os servidores da Cloudflare |

> **Em linguagem simples:** a latência é o tempo que a bola leva a ir e voltar; a velocidade é quanta
> água passa na mangueira. Para jogar, a latência importa mais do que a velocidade.

## Débito real — cliente/servidor

Mede o débito **entre dois PC teus**, sem passar por servidores de terceiros. Um lado fica à escuta, o
outro envia N MB e recebe os mesmos N MB de volta:

```
PC A: linux-explode --cli netbench servidor
PC B: linux-explode --cli netbench cliente 192.168.1.10 --mb 64
```

Na janela: escolhes **Cliente** ou **Servidor**, o IP do outro PC, a porta (47820 por omissão) e o
tamanho do teste.

O resultado mostra **subida**, **descida**, **latência média/mín/máx** e **jitter**. O protocolo é
mínimo — 20 idas e voltas de 8 bytes para a latência, depois os bytes — e é o mesmo código nos dois
lados, por isso o teste prova o caminho todo.

> **Em linguagem simples:** é como encher um balde no PC A com água do PC B e voltar a despejá-la.
> Serve para saber se o problema é do Wi-Fi, do cabo ou da operadora.

## O que podes fazer

- Testar a internet em segundos, com números em vez de "parece lento".
- Medir a rede local entre dois PC (cabo vs Wi-Fi, por exemplo).
- Comparar resultados ao longo do dia.

## Limites honestos

- O teste rápido usa servidores da Cloudflare: mede a internet **até eles**, não a um site específico.
- O débito real depende dos dois lados: um PC lento ou um disco lento baixa o número.
- Em loopback (o mesmo PC) os valores são de memória do kernel — servem para provar que funciona, não
  para medir a rede.
- Sem rede, a página abre com os alvos em falha — e diz isso.

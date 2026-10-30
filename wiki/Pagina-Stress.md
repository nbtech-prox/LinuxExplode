# Página Stress

![Página Stress](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-stress.png)

Serve para responder a **"a máquina aguenta? Aquece demasiado? É estável?"** — pondo o computador a
trabalhar a sério, com uma protecção térmica que corta o teste antes de ser tarde.

> **Atenção:** isto é carga a sério. A máquina aquece, as ventoinhas fazem barulho e a factura da luz
> sobe um pouco. Não é para deixar a correr sem vigilância.

## Configuração

Escolhes antes de começar:

| Campo | Opções |
|---|---|
| **Duração** | quanto tempo o teste corre |
| **Threads de CPU** | todos (16) ou um número |
| **Carga de CPU** | FMA vetorial (SIMD), inteiro, vírgula flutuante, memória |
| **Memória a encher** | 25 %, 50 % ou 75 % da RAM |
| **Guarda térmica** | 85, 90, 95 ou 100 °C |
| **Disco por passagem** | 512 MB, 1 GB, 2 GB… |

## Os testes

| Botão | O que faz | O que mede |
|---|---|---|
| **Testar CPU** | passagens contínuas de cálculo | milhões/s, GFLOP/s, temperatura, potência |
| **Testar RAM** | enche a memória, escreve um padrão, relê e **verifica cada palavra** | GB/s de escrita e leitura, erros |
| **Testar GPU** | path tracer Vulkan em ciclo | milhões de pontos/s, temperatura e potência |
| **Testar disco** | escreve e relê a verificar, sequencial e 4 KB | MB/s, IOPS, bytes diferentes |
| **Tudo (burn-in)** | os quatro em paralelo | tudo, mais os picos de temperatura |

## Guarda térmica

Lê os sensores da CPU e da GPU **quatro vezes por segundo** e pára tudo quando um limite é passado.
O resultado diz qual sensor disparou e a que temperatura:

```
parado — guarda térmica: CPU a 96 °C (limite 95 °C)
```

Os picos ficam registados mesmo que o teste seja cortado.

## Ciclos agendados

Um teste longo diz que a máquina aguenta; **N ciclos iguais dizem se ela repete o resultado**.

Escolhes o alvo, a duração de cada ciclo, o número de ciclos e a pausa entre eles. No fim, o cartão
mostra por métrica a **mediana, o mínimo, o máximo e a variação**. A variação só é afirmada a partir
de 3 ciclos — com 2, diz "amostra pequena". Acima de 3 %, avisa que a máquina não repetiu o resultado.

## Memória da GPU (VRAM)

Enche a memória da placa gráfica em blocos (64 MB a 1 GB), **escreve um padrão em cada bloco inteiro**
e relê amostras para comparar. Quando o driver recusa um bloco, o teste termina e diz qual foi a
resposta (`ErrorOutOfDeviceMemory`). O uso em `/sys` serve de prova independente.

> Encher até o driver recusar pode deixar o ambiente gráfico sem memória. Com o desktop a correr,
> 2 GB é um limite seguro nestas placas.

## Registo e relatório

O cartão **Registo** guarda as últimas 12 entradas (início, fim, paragens). **Guardar relatório**
escreve um JSON em `~/linux-explode-relatorios/stress-<data>.json` com a configuração, os resultados,
os picos e a energia medida.

## Linha de comandos

```bash
LinuxExplode --cli stress cpu 60                 # 1 minuto, todos os núcleos
LinuxExplode --cli stress ram 60
LinuxExplode --cli stress disco 120
LinuxExplode --cli stress tudo 300               # burn-in de 5 minutos
LinuxExplode --cli stress cpu 30 --limite 80     # guarda térmica mais apertada
LinuxExplode --cli ciclos cpu 60 --ciclos 5 --pausa 30
LinuxExplode --cli vram --limite 2048 --bloco 128
```

## Segurança

- O ficheiro do teste de disco é criado em `~/.cache/linux-explode/stress/` e **apagado no fim**.
- A RAM deixa sempre 1 GB livre; sem memória suficiente, o teste recusa arrancar em vez de provocar o
  OOM killer.
- Sair da página pára o teste.
- Não há escrita em discos do sistema nem alteração de definições — é só carga.

## Limites honestos

- O stress de GPU usa o renderizador do projecto; não substitui ferramentas dedicadas.
- O stress de disco corre num ficheiro dentro do teu home — não testa o disco inteiro.
- A carga de CPU é sintética: aquece e mede, mas não prevê o desempenho de um jogo específico.

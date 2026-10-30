# Página Discos e saúde

![Página Discos](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-discos.png)

Responde a **"o meu disco está bom ou está a morrer?"** — e deixa-te mandar o disco testar-se a si
próprio.

## O que vês

Um cartão por disco, com o modelo, o dispositivo (`/dev/nvme0n1`, `/dev/sda`), o tamanho, se é SSD ou
HDD e o estado de saúde (`Bom`, `Atenção`, `—`).

| Campo | O que é |
|---|---|
| **Horas ligado** | total de horas que o disco já trabalhou (e o equivalente em dias) |
| **Ciclos de energia** | quantas vezes foi ligado e desligado |
| **Desgaste** | percentagem de vida já consumida (SSD/NVMe) |
| **Setores realocados** | setores estragados que o disco substituiu por reservas — deve ficar em 0 |
| **Erros I/O** | erros de leitura/escrita registados pelo disco |

Depois, as **partições** com o espaço livre e uma barra de ocupação, e o **auto-teste S.M.A.R.T.**

### Auto-teste

- **Teste curto** — alguns minutos; verifica a electrónica e partes do disco.
- **Teste longo** — varre a superfície toda; pode demorar **horas** num HDD.
- **Abortar** — pára o teste em curso.

O cartão mostra o progresso (actualiza de 4 em 4 segundos) e o **histórico** dos últimos testes, com
as horas de funcionamento em que correram.

## O que significam os números

- **S.M.A.R.T.** é um sistema de auto-diagnóstico que todos os discos modernos têm. O disco conta ao
  sistema o que já correu mal.
- **Setores realocados** acima de zero é sinal de superfície a degradar-se. Um valor pequeno e
  estável não é motivo de pânico; a subir depressa, é.
- **Horas ligado** é o "conta-quilómetros" do disco. Um HDD com 34 000 h tem quase 4 anos de
  funcionamento efectivo.
- **Ciclos de energia** conta os arranques — mais relevante em portáteis.

> **Em linguagem simples:** o auto-teste é como levar o carro à inspecção. A app manda o disco fazer o
> teste e fica a ver o resultado; quem trabalha é o disco.

## O que podes fazer

- Ver o estado de cada disco e de cada partição.
- Lançar um teste curto ou longo e acompanhar o progresso.
- Abortar um teste que já demore demasiado.

Na linha de comandos:

```bash
LinuxExplode --cli discos
LinuxExplode --cli autoteste                    # estado e histórico
LinuxExplode --cli autoteste curto nvme0n1      # dispara um teste curto
LinuxExplode --cli autoteste parar nvme0n1      # aborta
```

## O que precisas

O `smartctl` (pacote **smartmontools**) e permissão para o correr sem password. O comando exacto é
mostrado pela própria app quando falta — vê [Instalar](Instalar).

## Limites honestos

- Dentro do Flatpak o acesso a `/dev` é limitado; a app diz isso na página em vez de inventar um
  veredito.
- Um disco USB pode não expor S.M.A.R.T. dessa forma.
- O teste longo num HDD deixa o disco mais lento enquanto corre. Podes abortá-lo.
- A app **não** testa o disco inteiro no stress: o stress usa um ficheiro temporário, apagado no fim.

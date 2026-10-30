# LINUX EXPLODE — a wiki

O **LINUX EXPLODE** mostra o que se passa dentro do teu computador: temperaturas, ventoinhas,
consumo, discos, rede e a velocidade real do processador e da placa gráfica. É uma aplicação para
Linux, com janela própria, e não precisa de root para quase nada.

Esta wiki explica **como instalar** e **para que serve cada página**, sem admitir que já sabes o que
é um "hwmon" ou um "S.M.A.R.T.". Se um termo aparecer e não estiver explicado, procura no
[Glossário](Glossario).

---

## Começar por aqui

1. **[Instalar](Instalar)** — o que precisas, como instalar em Flatpak e como actualizar.
2. **[Primeiros passos](Primeiros-passos)** — a janela, o tema, a escala, os idiomas e os atalhos.
3. Escolhe a página que responde à tua pergunta na tabela abaixo.

## Que página responde à minha pergunta?

| Quero saber… | Vai a |
|---|---|
| Que computador é este? Que CPU, quanta RAM, que placa gráfica? | [Sistema](Pagina-Sistema) |
| Está quente? As ventoinhas estão a trabalhar? Quanto consome? | [Sensores](Pagina-Sensores) |
| O disco está bom? Quantas horas tem? | [Discos](Pagina-Discos) |
| Quão rápido é o meu PC? E a placa gráfica? | [Benchmark](Pagina-Benchmark) |
| Aguenta carga? Aquece demasiado? É estável? | [Stress](Pagina-Stress) |
| A internet está boa? Que latência tenho? | [Rede](Pagina-Rede) |
| A placa está a usar o máximo do slot? O que está ligado por USB? | [Dispositivos](Pagina-Dispositivos) |
| Quero um relatório para guardar ou partilhar | [Relatório](Pagina-Relatorio) |
| Prefiro trabalhar no terminal | [Linha de comandos](Linha-de-comandos) |
| Algo não aparece ou não funciona | [Problemas comuns](Problemas-comuns) |

## Resumo em três linhas

- Lê o hardware **directamente do kernel** (`/sys`, `/proc`, DMI) — sem programas extra e sem root.
- Tem **oito páginas** e uma linha de comandos com os mesmos dados.
- Está disponível em **português, espanhol e inglês**, e abre em português.

![Página Sistema](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-sistema.png)

> **Em linguagem simples:** é como o painel de um carro. Em vez de luzes e ponteiros, mostra números
> — e diz sempre de onde vem cada número. Quando a máquina não tem esse sensor, escreve "sem sensor"
> em vez de inventar um valor.

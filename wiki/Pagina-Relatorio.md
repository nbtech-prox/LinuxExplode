# Página Relatório

![Página Relatório](https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/docs/wiki/pagina-relatorio.png)

Junta tudo o que a aplicação mediu num **único ficheiro** que podes guardar, arquivar ou enviar a
alguém.

## Gerar HTML completo

Carrega em **Gerar HTML completo** e sai um ficheiro autónomo:

- CSS e gráficos **SVG embutidos**;
- **zero pedidos de rede** e **zero JavaScript** — abre em qualquer browser, mesmo sem internet;
- pode ser enviado por email ou guardado numa pen.

O que vai lá dentro:

| Secção | Conteúdo |
|---|---|
| **Destaques** | CPU, memória, GPU, discos, corridas no histórico e último stress |
| **Sistema** | distribuição, kernel, arquitectura, máquina, placa-mãe, BIOS, uptime, CPU e RAM |
| **Placas de vídeo** | modelo, PCI, driver, relógio, VRAM, temperatura |
| **Discos e saúde** | modelo, tamanho, veredito S.M.A.R.T., horas, desgaste, auto-teste e partições |
| **Sensores** | todas as leituras de hwmon, agrupadas por chip |
| **Benchmarks** | ranking local (mediana, melhor, última, variação) e as últimas 15 corridas |
| **Stress, ciclos e VRAM** | o último resultado de cada um, com métricas agregadas |
| **Sessão gravada** | resumo da última sessão CSV e quatro gráficos (CPU, GPU, potência, frequência) |

## Gerar JSON

Para automação ou para tratar os dados noutro programa. O mesmo relatório, em texto estruturado.

## Onde ficam os ficheiros

Por omissão, no teu home (`~/linux-explode-<data>.html`). Os botões **Abrir** e **Abrir pasta**
levam-te ao ficheiro.

O último resultado de cada teste é guardado automaticamente em `~/.local/share/linux-explode/`
(`ultimo-stress.json`, `ultimo-ciclos.json`, `ultimo-vram.json`), por isso o relatório não depende de
teres clicado em "Guardar".

## Linha de comandos

```bash
LinuxExplode --cli relatorio html                  # grava no home
LinuxExplode --cli relatorio html /caminho/rel.html
LinuxExplode --cli relatorio json                  # para automação
```

## Para que serve, na prática

- **Pedir ajuda** num fórum: um ficheiro com tudo o que te vão perguntar.
- **Guardar o estado** antes e depois de uma limpeza, de mudar de pasta térmica ou de fazer
  overclock.
- **Comparar máquinas**: o mesmo relatório de dois PC.
- **Arquivar** uma medição que queres mostrar mais tarde.

> **Em linguagem simples:** é a "folha de exame" do teu PC, num só ficheiro que abre em qualquer lado
> — sem internet e sem instalar nada.

## Limites honestos

- O relatório mostra **o que foi medido**: sem benchmarks ou sem stress, essas secções dizem isso em
  vez de aparecerem vazias.
- O texto do relatório é em português (a interface é que muda de idioma).
- Não inclui dados de outros programas — só o que o LINUX EXPLODE leu e mediu.

# Primeiros passos

Esta página explica a janela: onde ficam as coisas, como mudar o tema, a escala e o idioma, e os
atalhos que poupam cliques.

## A janela em três partes

```
┌──────────────┬─────────────────────────────────────────────┐
│ LINUX        │  Título da página                           │
│ EXPLODE      │  uma linha a dizer de onde vêm os dados     │
│              │                                             │
│ Sistema      │  cartões com os números                     │
│ Sensores     │                                             │
│ Discos       │                                             │
│ Benchmark    │                                             │
│ Stress       │                                             │
│ Rede         │                                             │
│ Dispositivos │                                             │
│ Relatório    │                                             │
│              │                                             │
│ Tema         │                                             │
│ Idioma       │                                             │
│ Escala       │                                             │
└──────────────┴─────────────────────────────────────────────┘
```

- **Barra lateral (esquerda)** — navegação entre as oito páginas e, em baixo, o tema, o idioma e a
  escala.
- **Cabeçalho** — o nome da página e uma linha que diz de onde vêm os dados (por exemplo,
  `/sys/class/hwmon … leitura directa, sem root`). É a promessa da app: cada número tem uma fonte.
- **Conteúdo** — cartões. Cada cartão responde a uma pergunta; os que medem ao vivo actualizam
  sozinhos, uma vez por segundo.

## Tema claro ou escuro

O botão **Tema** alterna entre claro e escuro. A escolha fica guardada e aplica-se logo, sem
reiniciar.

## Escala da interface

O cursor **A — A** muda o tamanho de tudo entre **85 % e 135 %**. Serve para ecrãs pequenos ou para
quem prefere texto maior. O texto e os gráficos escalam juntos.

## Idioma

O selector mostra **Português**, **Español** e **English**. A aplicação **abre sempre em português** —
mesmo que o resto do sistema esteja em inglês — e só muda quando escolhes outra língua. A escolha
fica guardada e os números e as datas acompanham o idioma (por exemplo, `3,89 GHz` em português e
`3.89 GHz` em inglês).

> **Em linguagem simples:** se um dia abrires a app e ela estiver noutra língua, carrega no selector
> em baixo, à esquerda, e escolhe **Português**.

## Atalhos de teclado

| Atalho | O que faz |
|---|---|
| `Ctrl+1` | Sistema |
| `Ctrl+2` | Sensores |
| `Ctrl+3` | Discos e saúde |
| `Ctrl+4` | Benchmark |
| `Ctrl+5` | Stress |
| `Ctrl+6` | Rede |
| `Ctrl+7` | Dispositivos |
| `Ctrl+8` | Relatório |
| `Ctrl+=` / `Ctrl+-` | Aumenta / diminui a escala |
| `Ctrl+0` | Repõe a escala |
| `Ctrl+Q` | Fecha a aplicação |

## Ícone na bandeja

Se o teu ambiente tiver bandeja (KDE, niri+noctalia, GNOME com extensão), aparece um ícone com as
temperaturas da CPU e da GPU e o consumo medido. O menu tem **Abrir janela**, **Sensores**,
**Dispositivos**, **Parar todo o stress**, **Fechar para a bandeja** e **Sair**.

- Fechar a janela **esconde para a bandeja** — é o comportamento normal de um monitor. Para sair de
  vez, usa `Ctrl+Q` ou **Sair**.
- Sem bandeja, a janela abre normalmente e o ícone simplesmente não existe.

## Onde ficam as configurações e os dados

| O que | Onde (fora do Flatpak) | Dentro do Flatpak |
|---|---|---|
| Configuração (tema, escala, idioma, limites, tarifa) | `~/.config/linux-explode/config.json` | `~/.var/app/pt.linuxexplode.LinuxExplode/.config/linux-explode/config.json` |
| Histórico de benchmarks | `~/.local/share/linux-explode/historico.db` | `…/.local/share/linux-explode/historico.db` |
| Sessões gravadas (CSV) | `~/.local/share/linux-explode/sessoes/` | `…/.local/share/linux-explode/sessoes/` |
| Relatórios de stress | `~/linux-explode-relatorios/` | igual, dentro do home visível na sandbox |

No Flatpak a pasta de dados é persistida (`--persist=.local/share/linux-explode`), por isso o
histórico **não se perde** quando fechas a aplicação.

## Próximo passo

Escolhe uma página na barra lateral — ou começa por [Sensores](Pagina-Sensores), que é a que responde
às perguntas do dia-a-dia.

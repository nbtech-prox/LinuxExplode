# Instalar

O LINUX EXPLODE é distribuído **só em Flatpak**. Isso quer dizer que não precisas de instalar .NET,
bibliotecas ou drivers à mão: o pacote traz tudo o que precisa e corre em qualquer distribuição Linux.

## O que precisas

| Item | É obrigatório? | Para quê |
|---|---|---|
| Uma distribuição Linux com **Flatpak** | Sim | é a única forma de instalar |
| Driver de vídeo com **Vulkan** | Não | só para o benchmark de GPU; sem ele a app diz "indisponível" |
| **smartmontools** (`smartctl`) | Não | saúde e auto-teste dos discos |
| **PipeWire** (`pw-top`, `pw-metadata`) | Não | xruns e latência de áudio na página Dispositivos |
| Um ambiente com **bandeja** (StatusNotifier) | Não | ícone junto ao relógio; sem ele a janela abre na mesma |

## Instalar pelo repositório (recomendado)

O projecto publica um repositório Flatpak próprio, **assinado com GPG**, em
`https://nbtech-prox.github.io/LinuxExplode/`. Acrescentas uma vez e a partir daí recebes
actualizações como qualquer outra aplicação.

Abre um terminal e cola:

```bash
curl -fsSLO https://nbtech-prox.github.io/LinuxExplode/linux-explode-release.gpg
flatpak remote-add --user --gpg-import=linux-explode-release.gpg \
  linuxexplode https://nbtech-prox.github.io/LinuxExplode/
flatpak install --user linuxexplode pt.linuxexplode.LinuxExplode
```

Depois é só abrir pelo menu de aplicações (procura "Linux Explode") ou com:

```bash
flatpak run pt.linuxexplode.LinuxExplode
```

> **Em linguagem simples:** o `remote-add` é como dizer ao teu sistema "esta loja existe e é de
> confiança"; o ficheiro `.gpg` é a assinatura que prova que os pacotes são mesmo deste projecto.

### Atalho para o terminal (opcional)

No Flatpak o programa **não fica no `PATH`**: `LinuxExplode` responde "command not found". O comando
completo é `flatpak run pt.linuxexplode.LinuxExplode`. Para usar a forma curta, cria um atalho:

```bash
mkdir -p ~/.local/bin
printf '#!/bin/sh\nexec flatpak run pt.linuxexplode.LinuxExplode "$@"\n' > ~/.local/bin/LinuxExplode
chmod +x ~/.local/bin/LinuxExplode
```

A partir daí, `LinuxExplode --cli info` funciona como qualquer outro comando. (Se `~/.local/bin` não
estiver no `PATH`, acrescenta `export PATH="$HOME/.local/bin:$PATH"` ao teu `~/.zshrc` ou `~/.bashrc`.)

### Actualizar

```bash
flatpak update --user
```

Ou usa o gestor de aplicações da tua distribuição, que passa a mostrar o LINUX EXPLODE na lista de
actualizações.

## Instalar a partir do ficheiro

Se preferires descarregar o pacote da página de releases:

1. Vai a https://github.com/nbtech-prox/LinuxExplode/releases/latest
2. Descarrega `linux-explode.flatpak` (x86_64) ou `linux-explode-aarch64.flatpak` (ARM64).
3. Instala:

```bash
flatpak install --user ./linux-explode.flatpak
```

> Um bundle instalado assim fica ligado a uma origem local **sem URL**: o gestor de aplicações pode
> anunciar uma actualização que falha. Para actualizar sem problemas, usa o repositório acima.

## Desinstalar

```bash
flatpak uninstall --user pt.linuxexplode.LinuxExplode
```

Se quiseres apagar também o histórico de medições e as configurações:

```bash
flatpak uninstall --user --delete-data pt.linuxexplode.LinuxExplode
```

## Passos opcionais (uma vez)

Nada disto é preciso para usar a aplicação. Sem estes passos, a app funciona e **diz o que falta** —
nunca inventa números.

### 1. Ver o consumo da CPU (RAPL)

O kernel esconde os contadores de energia por segurança (`energy_uj` é `0400 root`). Para os tornar
legíveis, descarrega o script do repositório e corre-o uma vez:

```bash
curl -fsSLO https://raw.githubusercontent.com/nbtech-prox/LinuxExplode/main/permitir-energia.sh
chmod +x permitir-energia.sh
sudo ./permitir-energia.sh
```

Para confirmar, desfazer ou ver o estado:

```bash
sudo ./permitir-energia.sh --estado
sudo ./permitir-energia.sh --remover
```

Sem isto, a página Sensores mostra a potência das GPUs e explica que a da CPU precisa de permissão.

### 2. Saúde dos discos (smartctl)

Instala o `smartmontools` pela tua distribuição e dá permissão sem password ao `smartctl`:

```bash
echo "$USER ALL=(root) NOPASSWD: /usr/bin/smartctl, /usr/bin/rtcwake" | sudo tee /etc/sudoers.d/smartctl
sudo chmod 440 /etc/sudoers.d/smartctl
```

Isto cobre também o **rtcwake**, usado no teste de suspender/retomar. Se o caminho do `smartctl` for
outro na tua distribuição (`/usr/sbin/smartctl`, por exemplo), usa esse — a própria app mostra o
comando certo quando falta a permissão.

### 3. FPS de jogos (MangoHud)

Se queres ver os fotogramas por segundo de um jogo, grava uma sessão com o
[MangoHud](https://github.com/flightlessmango/MangoHud):

```bash
mkdir -p ~/mangologs
```

E arranca o jogo com `MANGOHUD=1 %command%` (nas opções de arranque da Steam) ou
`MANGOHUD=1 mangohud <programa>`. O cartão **FPS** da página Benchmark lê esses ficheiros.

## Problemas na instalação?

Vai a [Problemas comuns](Problemas-comuns). Os casos típicos são: Flatpak antigo, falta de driver
Vulkan e o ícone da bandeja não aparecer.

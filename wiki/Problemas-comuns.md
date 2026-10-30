# Problemas comuns

Respostas curtas para o que costuma correr mal. Se o problema não estiver aqui, abre um _issue_ no
repositório com o resultado de `LinuxExplode --cli info` e, se possível, o relatório HTML.

## `command not found: LinuxExplode`

O Flatpak não põe o programa no `PATH`. Usa o comando completo:

```bash
flatpak run pt.linuxexplode.LinuxExplode --cli info
```

Ou cria o atalho descrito em [Instalar](Instalar) (um ficheiro em `~/.local/bin`), e passa a poder
escrever `LinuxExplode --cli info`.

## A aplicação abriu noutra língua

Carrega no selector em baixo, à esquerda (**Português / Español / English**) e escolhe **Português**.
A escolha fica guardada. A app abre sempre em português até alguém escolher outra língua.

## Não aparecem temperaturas

1. Confirma que existem sensores: `LinuxExplode --cli sensores`.
2. Num portátil ou numa máquina virtual pode não haver sensor de CPU. Nesse caso a app diz isso.
3. Em AMD o sensor é o `k10temp`; em Intel, o `coretemp`. Se nenhum estiver carregado, a app lista o
   que o kernel tiver.

## Não aparecem as ventoinhas da placa-mãe

O chip Super I/O da placa precisa de um driver do kernel. Nesta placa (MSI MAG X870E) é o `nct6683`:

```bash
sudo modprobe nct6683 force=1
```

Para ficar permanente:

```bash
echo nct6683 | sudo tee /etc/modules-load.d/nct6683.conf
printf 'options nct6683 force=1\n' | sudo tee /etc/modprobe.d/nct6683.conf
```

Noutras placas pode ser `nct6775`, `it87` ou `f71882fg`. Sem driver, a app não inventa ventoinhas.

## "Sem leitura de potência" / energia a zero

O kernel esconde os contadores de energia (`energy_uj` é `0400 root`). Corre uma vez:

```bash
sudo ./permitir-energia.sh
```

Vê [Instalar](Instalar) para descarregar o script.

## Discos sem saúde S.M.A.R.T.

Falta o `smartctl` (pacote **smartmontools**) ou a permissão sem password. A app mostra o comando
exacto — é o mesmo descrito em [Instalar](Instalar). Confirma o caminho do binário
(`/usr/bin/smartctl` ou `/usr/sbin/smartctl`).

## O ícone da bandeja não aparece

A bandeja precisa de `StatusNotifierItem`/AppIndicator no ambiente: KDE, niri+noctalia, ou GNOME com
extensão. Sem isso a janela abre normalmente, só não há ícone. Para arrancar sem tentar:
`LINUXEXPLODE_SEM_BANDEJA=1`.

## O benchmark de GPU diz "indisponível"

- Falta o driver Vulkan (`vulkan-radeon`, `vulkan-intel`, `nvidia-utils`).
- A GPU não tem fila de computação (comum em máquinas virtuais e em alguns SoC ARM).
- Os dispositivos de software (llvmpipe) são ignorados de propósito: medir GPU no CPU não diz nada.

## O ray tracing diz "indisponível"

A placa ou o driver não expõe `VK_KHR_ray_query` (comum em Polaris e GTX 10xx). O modo compute
continua a funcionar.

## O cartão de FPS está vazio

1. Instala o MangoHud e cria a pasta: `mkdir -p ~/mangologs`.
2. Arranca o jogo com `MANGOHUD=1 %command%` (Steam) ou `MANGOHUD=1 mangohud <programa>`.
3. No Flatpak, as pastas `~/mangologs`, `~/.config/MangoHud` e `~/.local/share/MangoHud` estão
   montadas só de leitura.

Em Wayland não há outra forma de ler FPS de outra aplicação — o compositor não os expõe.

## Acerca do stress

- **A máquina ficou lenta e quente:** é o esperado. A guarda térmica pára tudo no limite escolhido.
- **Não arranca o stress de RAM:** não há memória livre suficiente; a app deixa sempre 1 GB livre e
  recusa em vez de provocar o OOM killer.
- **O disco fica lento:** o teste escreve e relê um ficheiro grande. O ficheiro é apagado no fim.

## Suspender/retomar não funciona

- Para suspender sem password, o `systemctl` tem de estar no sudoers.
- Para acordar sozinho, o `rtcwake` tem de estar no sudoers (o comando em [Instalar](Instalar) inclui
  ambos).
- Sem `rtcwake`, a máquina suspende mas só acorda quando lhe tocas — o cartão avisa antes.
- Em máquinas virtuais o kernel pode não anunciar suspensão; a app diz isso.

## Actualização falha

Se instalaste a partir de um ficheiro `.flatpak`, a origem é local e sem URL — o gestor de aplicações
pode anunciar uma actualização que falha. Usa o repositório assinado:

```bash
flatpak remote-add --user --if-not-exists \
  --gpg-import=linux-explode-release.gpg \
  linuxexplode https://nbtech-prox.github.io/LinuxExplode/
flatpak update --user
```

## Perdi o histórico?

O histórico vive em `~/.local/share/linux-explode/historico.db` (no Flatpak,
`~/.var/app/pt.linuxexplode.LinuxExplode/.local/share/linux-explode/`). É persistido entre arranques.
Se apagares a app com `--delete-data`, o histórico vai com ela.

## A janela está pequena e o texto corta-se

Usa a escala (**A — A**, 85 % a 135 %) ou maximiza a janela. O cartão de alertas passou a poder
descer de linha precisamente para não cortar texto.

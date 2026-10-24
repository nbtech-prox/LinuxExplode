#!/usr/bin/env bash
# Instala o LINUX EXPLODE a partir deste pacote (linux-arm64) sem root.
#   ./install.sh                     → instala em ~/.local
#   PREFIXO=/usr/local sudo ./install.sh  → instala no sistema
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_ID=pt.linuxexplode.LinuxExplode
PREFIXO="${PREFIXO:-$HOME/.local}"
DESTINO="$PREFIXO/share/linux-explode"
BIN="$PREFIXO/bin"

case "$(uname -m)" in
  aarch64|arm64) ;;
  *) printf 'AVISO: este pacote é arm64 e esta máquina é %s — instala, mas a aplicação não arranca.\n\n' "$(uname -m)" >&2 ;;
esac

if [ ! -x "$AQUI/app/LinuxExplode" ]; then
  echo "erro: não encontro app/LinuxExplode ao lado deste script" >&2
  exit 1
fi

echo "== a instalar em $DESTINO"
mkdir -p "$DESTINO" "$BIN" \
         "$PREFIXO/share/applications" \
         "$PREFIXO/share/icons/hicolor/256x256/apps"
cp -a "$AQUI/app/." "$DESTINO/"
chmod +x "$DESTINO/LinuxExplode"
ln -sfn "$DESTINO/LinuxExplode" "$BIN/linux-explode"

install -m644 "$AQUI/share/pt.linuxexplode.LinuxExplode.png" \
  "$PREFIXO/share/icons/hicolor/256x256/apps/$APP_ID.png"

cat > "$PREFIXO/share/applications/$APP_ID.desktop" <<DESKTOP
[Desktop Entry]
Name=LINUX EXPLODE
Comment=Hardware, sensores, saúde dos discos e benchmarks
Exec=$BIN/linux-explode
Icon=$APP_ID
Terminal=false
Type=Application
Categories=System;Monitor;Utility;
Keywords=hardware;sensores;benchmark;diagnostico;bench;
StartupNotify=true
StartupWMClass=LinuxExplode
DESKTOP
chmod 644 "$PREFIXO/share/applications/$APP_ID.desktop"

# caches opcionais — só correm se as ferramentas existirem
command -v update-desktop-database >/dev/null && update-desktop-database "$PREFIXO/share/applications" >/dev/null 2>&1 || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q -t -f "$PREFIXO/share/icons/hicolor" >/dev/null 2>&1 || true

echo "== instalado"
echo "   binário : $DESTINO/LinuxExplode"
echo "   atalho  : $BIN/linux-explode"
echo "   menu    : $PREFIXO/share/applications/$APP_ID.desktop"

case ":$PATH:" in
  *":$BIN:"*) echo; echo "corre:  linux-explode" ;;
  *) echo; echo "corre:  $BIN/linux-explode"
     echo "        (ou acrescenta $BIN ao PATH para teres só 'linux-explode')" ;;
esac
echo "interface gráfica precisa de sessão Wayland/X11 e driver Vulkan; a CLI funciona sem ambos:"
echo "        $BIN/linux-explode --cli discos"

#!/usr/bin/env bash
# Remove o que o install.sh criou.
set -euo pipefail

APP_ID=pt.linuxexplode.LinuxExplode
PREFIXO="${PREFIXO:-$HOME/.local}"
DESTINO="$PREFIXO/share/linux-explode"

rm -rf "$DESTINO"
rm -f "$PREFIXO/bin/linux-explode" \
      "$PREFIXO/share/applications/$APP_ID.desktop" \
      "$PREFIXO/share/icons/hicolor/256x256/apps/$APP_ID.png"

command -v update-desktop-database >/dev/null && update-desktop-database "$PREFIXO/share/applications" >/dev/null 2>&1 || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q -t -f "$PREFIXO/share/icons" >/dev/null 2>&1 || true

echo "removido de $PREFIXO"

#!/bin/bash
# Compila o LINUX EXPLODE e empacota.
#   ./empacotar.sh            → compila x86_64 e instala localmente (user)
#   ./empacotar.sh --bundle   → idem + gera linux-explode.flatpak (x86_64)
#   ./empacotar.sh --arm64    → gera o pacote ARM64: tar.gz com instalador
#                               e linux-explode-aarch64.flatpak (sem emulação)
set -euo pipefail

cd "$(dirname "$0")"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

APP_ID=pt.linuxexplode.LinuxExplode
STAGING=flatpak/staging
VERSAO=$(grep -oP '(?<=<Version>)[^<]+' LinuxExplode.csproj | head -1)
MODO="${1:-}"

# ------------------------------------------------------------------ assinatura GPG
# Com LINUXEXPLODE_GPG_KEY (chave privada em ASCII armor, com quebras de linha) o repositório sai
# assinado: os commits e o summary levam assinatura e o cliente deixa de precisar de --no-gpg-verify.
# Sem a variável o build continua a servir para desenvolvimento, mas o remote fica sem verificação.
# O homedir temporário tem de viver DENTRO do projecto: o org.flatpak.Builder corre em sandbox e não
# vê o /tmp do anfitrião.
GPG_DIR=""
GPG_ARGS=()
if [[ -n "${LINUXEXPLODE_GPG_KEY:-}" ]]; then
  GPG_DIR="$(pwd)/.gpg-release"
  rm -rf "$GPG_DIR"; mkdir -p "$GPG_DIR"; chmod 700 "$GPG_DIR"
  trap 'rm -rf "$GPG_DIR"' EXIT
  printf '%s' "$LINUXEXPLODE_GPG_KEY" | GNUPGHOME="$GPG_DIR" gpg --batch --import >/dev/null 2>&1
  GPG_ID=$(GNUPGHOME="$GPG_DIR" gpg --batch --list-secret-keys --with-colons | awk -F: '/^sec:/{print $5; exit}')
  if [[ -z "$GPG_ID" ]]; then
    echo "erro: LINUXEXPLODE_GPG_KEY não contém uma chave secreta válida" >&2
    exit 1
  fi
  GPG_ARGS=(--gpg-sign="$GPG_ID" --gpg-homedir="$GPG_DIR")
  echo "== assinatura GPG activa (chave $GPG_ID)"
fi

# ------------------------------------------------------------------ ARM64
# O bundle aarch64 é produzido sem flatpak-builder e SEM emulação: o build
# dir é montado à mão e finalizado com build-finish (que não aceita --arch;
# a arquitectura vem do runtime escrito no metadata), exportado com
# `build-export --arch=aarch64` e empacotado com `build-bundle --arch=aarch64`.
# Nada aqui executa um binário ARM.
empacotar_arm64() {
  local PUB=publish-arm64
  local B=flatpak/build-arm64
  local R=flatpak/repo-arm64
  local PKG="linux-explode-$VERSAO-linux-arm64"
  local STAGE

  echo "== 1/4  publicar binário (self-contained, linux-arm64)"
  rm -rf "$PUB"
  dotnet publish LinuxExplode.csproj -c Release -r linux-arm64 --self-contained true \
    -p:PublishSingleFile=false -p:DebugType=None -o "$PUB" --nologo

  echo "== 2/4  pacote tar.gz com instalador"
  STAGE=$(mktemp -d)
  mkdir -p "$STAGE/$PKG/share"
  cp -a "$PUB" "$STAGE/$PKG/app"
  cp Assets/logo.png "$STAGE/$PKG/share/$APP_ID.png"
  cp packaging/arm64/install.sh packaging/arm64/uninstall.sh packaging/arm64/LEIAME.txt "$STAGE/$PKG/"
  chmod 755 "$STAGE/$PKG/install.sh" "$STAGE/$PKG/uninstall.sh"
  chmod 644 "$STAGE/$PKG/LEIAME.txt" "$STAGE/$PKG/share/$APP_ID.png"
  tar -C "$STAGE" -czf "$PKG.tar.gz" "$PKG"
  rm -rf "$STAGE"

  echo "== 3/4  build dir flatpak aarch64"
  rm -rf "$B" "$R"
  mkdir -p "$B/files/bin/linuxexplode" "$B/files/share/applications" \
           "$B/files/share/metainfo" "$B/files/share/icons/hicolor/256x256/apps" "$R"
  cp -a "$PUB/." "$B/files/bin/linuxexplode/"
  chmod 755 "$B/files/bin/linuxexplode/LinuxExplode"
  ln -sfn /app/bin/linuxexplode/LinuxExplode "$B/files/bin/LinuxExplode"
  install -m644 flatpak/$APP_ID.desktop "$B/files/share/applications/$APP_ID.desktop"
  install -m644 flatpak/$APP_ID.metainfo.xml "$B/files/share/metainfo/$APP_ID.metainfo.xml"
  install -m644 Assets/logo.png "$B/files/share/icons/hicolor/256x256/apps/$APP_ID.png"
  printf '[Application]\nname=%s\nruntime=org.freedesktop.Platform/aarch64/25.08\nsdk=org.freedesktop.Sdk/aarch64/25.08\ncommand=LinuxExplode\n' "$APP_ID" > "$B/metadata"
  flatpak build-finish --command=LinuxExplode --share=network --share=ipc \
    --socket=wayland --socket=x11 --device=dri --filesystem=xdg-documents \
    --talk-name=org.freedesktop.Notifications --talk-name=org.freedesktop.Flatpak "$B" >/dev/null

  echo "== 4/4  exportar e empacotar o bundle"
  flatpak build-export --arch=aarch64 "${GPG_ARGS[@]}" "$R" "$B" stable
  flatpak build-bundle --arch=aarch64 "$R" linux-explode-aarch64.flatpak "$APP_ID" stable

  echo
  echo "pronto:"
  echo "  $(pwd)/$PKG.tar.gz                ($(du -h "$PKG.tar.gz" | cut -f1), instalador sem root)"
  echo "  $(pwd)/linux-explode-aarch64.flatpak ($(du -h linux-explode-aarch64.flatpak | cut -f1), precisa do runtime org.freedesktop.Platform aarch64)"
  echo "  no destino ARM: ./install.sh   |   flatpak install --user linux-explode-aarch64.flatpak"
}

if [[ "$MODO" == "--arm64" ]]; then
  empacotar_arm64
  exit 0
fi

# ------------------------------------------------------------------ x86_64
echo "== 1/3  publicar binário (self-contained, linux-x64)"
rm -rf "$STAGING" publish
dotnet publish LinuxExplode.csproj -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=false -p:DebugType=None -o publish --nologo

echo "== 2/3  preparar staging"
mkdir -p "$STAGING"
cp -r publish/. "$STAGING/app/"
cp flatpak/$APP_ID.desktop "$STAGING/"
cp flatpak/$APP_ID.metainfo.xml "$STAGING/"
cp Assets/logo.png "$STAGING/"

echo "== 3/3  construir flatpak e publicar num repositório local"
# Um bundle instalado com `flatpak install ./x.flatpak` fica ligado a uma origem local sem URL:
# o gestor de pacotes anuncia "actualização" e a actualização falha. Por isso o build é exportado
# para um repositório OSTree do projecto, exposto como remote, e a instalação é feita a partir dele
# — assim `flatpak update` funciona. Quando houver repositório público, muda-se só o URL do remote.
REPO=flatpak/repo-x86_64
REMOTE=linuxexplode
rm -rf "$REPO"
flatpak run org.flatpak.Builder --user --force-clean --repo="$REPO" "${GPG_ARGS[@]}" build-dir flatpak/$APP_ID.json
flatpak build-update-repo --title="LINUX EXPLODE" "${GPG_ARGS[@]}" "$REPO"

# Em CI só interessa o repositório e o bundle; instalar ali a app não serve para nada.
if [ "${CI:-}" = "true" ]; then
  echo "CI: repositório em $REPO (sem instalar localmente)"
else
  if [[ ${#GPG_ARGS[@]} -gt 0 ]]; then
    if flatpak remote-list --user --columns=name | grep -qx "$REMOTE"; then
      flatpak remote-modify --user --url="file://$(pwd)/$REPO" "$REMOTE"
      flatpak remote-modify --user --gpg-import=docs/linux-explode-release.gpg "$REMOTE"
      flatpak remote-modify --user --gpg-verify "$REMOTE"
    else
      flatpak remote-add --user --title="LINUX EXPLODE" \
        --gpg-import=docs/linux-explode-release.gpg "$REMOTE" "file://$(pwd)/$REPO"
    fi
  else
    if flatpak remote-list --user --columns=name | grep -qx "$REMOTE"; then
      flatpak remote-modify --user --url="file://$(pwd)/$REPO" "$REMOTE"
    else
      flatpak remote-add --user --no-gpg-verify --title="LINUX EXPLODE" \
        "$REMOTE" "file://$(pwd)/$REPO"
    fi
  fi

  flatpak install --user -y --reinstall "$REMOTE" "$APP_ID" >/dev/null
  echo "instalado a partir do remote '$REMOTE' (flatpak update passa a funcionar)"
fi

if [[ "$MODO" == "--bundle" ]]; then
  flatpak build-bundle "$REPO" linux-explode.flatpak "$APP_ID" stable
  echo "bundle: $(pwd)/linux-explode.flatpak"
fi

echo "pronto: flatpak run $APP_ID"

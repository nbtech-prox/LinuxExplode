#!/usr/bin/env bash
#
# LinuxExplode — permitir a leitura dos contadores RAPL (potência da CPU).
#
# O kernel expõe /sys/class/powercap/*/energy_uj com modo 0400 root desde a correcção
# da CVE-2020-8694 (os contadores de energia servem de canal lateral). Sem esta
# permissão a app mostra a potência das GPUs mas não a do pacote da CPU.
#
# Este script instala uma unidade systemd (oneshot, corre em cada arranque) que
# torna os contadores legíveis por todos (0444). Nada mais é alterado.
#
#   sudo ./permitir-energia.sh            instala e aplica
#   sudo ./permitir-energia.sh --remover  desinstala e volta a 0400
#   sudo ./permitir-energia.sh --estado   só mostra o estado actual
#
set -euo pipefail

UNIDADE=/etc/systemd/system/linuxexplode-rapl.service
RAIZ=/sys/class/powercap

se_preciso_root() {
    if [ "$(id -u)" -ne 0 ]; then
        echo "precisa de root — a pedir sudo…"
        exec sudo -- "$0" "$@"
    fi
}

mostrar() {
    echo
    echo "contadores RAPL:"
    local algum=0
    for f in "$RAIZ"/*/energy_uj; do
        [ -e "$f" ] || continue
        algum=1
        local nome
        nome=$(cat "$(dirname "$f")/name" 2>/dev/null || basename "$(dirname "$f")")
        printf '  %-14s %-10s %s\n' "$nome" "$(stat -c %A "$f")" "$f"
    done
    [ "$algum" -eq 1 ] || echo "  nenhum — este kernel não expõe RAPL (sem leitura de potência da CPU)"

    if [ "$algum" -eq 1 ]; then
        local a b
        for f in "$RAIZ"/*/energy_uj; do
            cat "$f" >/dev/null 2>&1 || { echo "  (sem permissão de leitura: $(id -un))"; return; }
        done
        a=$(cat "$RAIZ"/intel-rapl:0/energy_uj)
        sleep 1
        b=$(cat "$RAIZ"/intel-rapl:0/energy_uj)
        awk -v d="$((b - a))" 'BEGIN { printf "  pacote da CPU agora: %.1f W\n", d / 1e6 }'
    fi

    echo
    if systemctl is-enabled --quiet linuxexplode-rapl.service 2>/dev/null; then
        echo "unidade linuxexplode-rapl.service: activa (permissões repostas em cada arranque)"
    else
        echo "unidade linuxexplode-rapl.service: não instalada"
    fi
}

instalar() {
    echo "a instalar $UNIDADE…"
    cat > "$UNIDADE" <<'EOF'
[Unit]
Description=LinuxExplode — tornar legíveis os contadores RAPL (potência da CPU)
After=sys-devices-virtual-powercap-intel-rapl-intel-rapl:0.device
ConditionPathExists=/sys/class/powercap

[Service]
Type=oneshot
RemainAfterExit=yes
# 0444 em cada energy_uj: a app (e ferramentas do utilizador) passam a ler a potência
# da CPU sem root. O RAPL é um canal lateral teórico; num posto de trabalho local
# o risco é o mesmo de qualquer monitor de hardware.
ExecStart=/bin/sh -c 'for f in /sys/class/powercap/*/energy_uj; do [ -e "$f" ] && chmod 0444 "$f"; done'

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable --now linuxexplode-rapl.service
    echo "instalada e aplicada."
    echo
    echo "para desfazer: sudo $0 --remover"
}

remover() {
    echo "a remover $UNIDADE…"
    systemctl disable --now linuxexplode-rapl.service 2>/dev/null || true
    rm -f "$UNIDADE"
    systemctl daemon-reload
    for f in "$RAIZ"/*/energy_uj; do
        [ -e "$f" ] && chmod 0400 "$f"
    done
    echo "removida — os contadores voltaram a ser exclusivos do root."
}

se_preciso_root "$@"

case "${1:-}" in
    --remover|--remove|remover) remover ;;
    --estado|--status|estado)   ;;
    ""|--instalar|instalar)     instalar ;;
    *)
        echo "uso: $0 [--instalar | --remover | --estado]"
        exit 2
        ;;
esac

mostrar

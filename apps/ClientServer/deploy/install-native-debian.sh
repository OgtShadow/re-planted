#!/usr/bin/env bash
set -euo pipefail

APP_SOURCE_DIR="${1:-$(pwd)}"
APP_DIR="/opt/re-planted-clientserver"
CONFIG_DIR="/etc/re-planted-clientserver"
SERVICE_FILE="re-planted-clientserver.service"

if [[ "$(id -u)" -ne 0 ]]; then
    echo "Run as root: sudo $0 /path/to/published-directory"
    exit 1
fi

if [[ ! -x "$APP_SOURCE_DIR/ClientServer" ]]; then
    echo "Missing executable: $APP_SOURCE_DIR/ClientServer"
    exit 1
fi

getent group replanted >/dev/null || groupadd --system replanted
id replanted >/dev/null 2>&1 || useradd --system --gid replanted --home-dir "$APP_DIR" --shell /usr/sbin/nologin replanted

install -d -o replanted -g replanted "$APP_DIR" "$APP_DIR/data" "$APP_DIR/logs" "$CONFIG_DIR"
cp -a "$APP_SOURCE_DIR/." "$APP_DIR/"
chown -R replanted:replanted "$APP_DIR"

if [[ ! -f "$CONFIG_DIR/clientserver.env" ]]; then
    install -o root -g replanted -m 0640 "$APP_DIR/clientserver.raspberrypi.env.example" "$CONFIG_DIR/clientserver.env"
    echo "Edit $CONFIG_DIR/clientserver.env and set the real Jwt key before starting the service."
fi

install -o root -g root -m 0644 "$APP_DIR/re-planted-clientserver.service" /etc/systemd/system/re-planted-clientserver.service
systemctl daemon-reload
systemctl enable re-planted-clientserver.service
systemctl restart re-planted-clientserver.service
systemctl --no-pager --full status re-planted-clientserver.service

#!/usr/bin/env bash
set -e

echo "======================================="
echo " ReadyStackGo - Bootstrap Installer"
echo "======================================="

if [ "$EUID" -ne 0 ]; then
  echo "Bitte mit sudo oder als root ausfuehren."
  exit 1
fi

# -----------------------------------------------------------
# Parameter / Defaults
# -----------------------------------------------------------

PORT=${1:-8080}

if ! [[ "$PORT" =~ ^[0-9]+$ ]] || [ "$PORT" -lt 1 ] || [ "$PORT" -gt 65535 ]; then
  echo "Ungueltiger Port: $PORT"
  echo "Verwendung: curl ... | sudo bash -s 3000"
  exit 1
fi

CONTAINER_NAME="readystackgo"
IMAGE_NAME="wiesenwischer/readystackgo:latest"
VOLUME_CONFIG="readystackgo-config"
VOLUME_DATA="readystackgo-data"
VOLUME_STACKS="readystackgo-stacks"

echo "Verwendeter Port: $PORT"
echo ""

# -----------------------------------------------------------
# 1. Pruefen ob Docker installiert ist
# -----------------------------------------------------------

if command -v docker >/dev/null 2>&1; then
  echo "[OK] Docker ist bereits installiert"
else
  echo "[...] Docker ist nicht installiert - Installation wird gestartet..."

  # Detect package manager
  if command -v apt >/dev/null 2>&1; then
    apt update
    apt install -y ca-certificates curl gnupg lsb-release

    mkdir -p /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
      | gpg --dearmor -o /etc/apt/keyrings/docker.gpg

    echo \
    "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
    https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" \
    | tee /etc/apt/sources.list.d/docker.list > /dev/null

    apt update
    apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

  elif command -v yum >/dev/null 2>&1; then
    yum install -y yum-utils
    yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
    yum install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

  elif command -v dnf >/dev/null 2>&1; then
    dnf install -y dnf-plugins-core
    dnf config-manager --add-repo https://download.docker.com/linux/fedora/docker-ce.repo
    dnf install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

  else
    echo "Nicht unterstuetztes System. Bitte Docker manuell installieren."
    exit 1
  fi

  systemctl enable docker
  systemctl start docker

  echo "[OK] Docker wurde installiert"
fi

# -----------------------------------------------------------
# 2. Container starten
# -----------------------------------------------------------

echo "[...] Starte Container '$CONTAINER_NAME'..."

# Existierenden Container entfernen
if [ "$(docker ps -aq -f name=$CONTAINER_NAME)" ]; then
  echo "[...] Entferne alten Container..."
  docker rm -f $CONTAINER_NAME
fi

docker pull $IMAGE_NAME

docker run -d \
  --name $CONTAINER_NAME \
  --restart unless-stopped \
  -p $PORT:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v $VOLUME_CONFIG:/app/config \
  -v $VOLUME_DATA:/app/data \
  -v $VOLUME_STACKS:/app/stacks \
  $IMAGE_NAME

echo ""
echo "[OK] ReadyStackGo laeuft jetzt!"
echo "URL: http://$(hostname -I | awk '{print $1}'):$PORT"
echo ""

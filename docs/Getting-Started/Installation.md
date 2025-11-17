# Developer Setup Guide

Dieses Dokument beschreibt, wie du ReadyStackGo lokal entwickeln und ausführen kannst.

---

## 1. Voraussetzungen

- **.NET 9 SDK**
- **Node.js 20+**
- **npm** oder **pnpm**
- **Docker** (Linux, Docker Desktop oder remote Engine)
- Ein Editor deiner Wahl (z. B. Rider, VS Code)

---

## 2. Repository klonen

```bash
git clone <repo-url> readystackgo
cd readystackgo
```

---

## 3. Backend bauen

```bash
cd src/ReadyStackGo.Api
dotnet restore
dotnet build
```

---

## 4. Frontend bauen

```bash
cd src/ReadyStackGo.WebUi
npm install
npm run dev
```

Für ein Production-Build:

```bash
npm run build
```

Das gebaute Frontend kann dann als statischer Inhalt vom Admin-Container ausgeliefert werden.

---

## 5. Admin-Container lokal starten

Beispiel (Linux / WSL):

```bash
docker build -t rsgo-admin:dev -f docker/ReadyStackGo.Admin.Dockerfile .
docker run -d \
  --name rsgo-admin-dev \
  -p 8443:8443 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v rsgo-config-dev:/app/config \
  rsgo-admin:dev
```

Danach im Browser:

- `https://localhost:8443` öffnen
- Self-Signed-Zertifikat temporär akzeptieren
- Wizard durchlaufen

---

## 6. Debugging

### Backend (API)

- Projekt `ReadyStackGo.Api` starten
- Startprofil so konfigurieren, dass Kestrel auf z. B. Port 5001 läuft
- Optional HTTPS aktivieren

### Frontend

- `npm run dev` ausführen
- UI unter `http://localhost:5173` (oder ähnlichem Port) öffnen
- API-Proxy konfigurieren (Dev-Proxy auf `https://localhost:5001`)

---

## 7. Tests ausführen

```bash
dotnet test
```

Frontend-Tests (sofern vorhanden):

```bash
npm test
```

---

## 8. Docker Cleanup

Zum Entfernen der Dev-Umgebung:

```bash
docker stop rsgo-admin-dev
docker rm rsgo-admin-dev
docker volume rm rsgo-config-dev
```

---

Damit ist deine Entwicklungsumgebung bereit, um an ReadyStackGo zu arbeiten 🚀

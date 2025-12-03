# Developer Setup Guide

This document describes how to develop and run ReadyStackGo locally.

---

## 1. Prerequisites

- **.NET 9 SDK**
- **Node.js 20+**
- **npm** or **pnpm**
- **Docker** (Linux, Docker Desktop, or remote engine)
- An editor of your choice (e.g., Rider, VS Code)

---

## 2. Clone Repository

```bash
git clone <repo-url> readystackgo
cd readystackgo
```

---

## 3. Build Backend

```bash
cd src/ReadyStackGo.Api
dotnet restore
dotnet build
```

---

## 4. Build Frontend

```bash
cd src/ReadyStackGo.WebUi
npm install
npm run dev
```

For a production build:

```bash
npm run build
```

The built frontend can then be served as static content from the admin container.

---

## 5. Start Admin Container Locally

Example (Linux / WSL):

```bash
docker build -t rsgo-admin:dev -f docker/ReadyStackGo.Admin.Dockerfile .
docker run -d \
  --name rsgo-admin-dev \
  -p 8443:8443 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v rsgo-config-dev:/app/config \
  rsgo-admin:dev
```

Then in browser:

- Open `https://localhost:8443`
- Temporarily accept self-signed certificate
- Complete the wizard

---

## 6. Debugging

### Backend (API)

- Start project `ReadyStackGo.Api`
- Configure launch profile so Kestrel runs on e.g., port 5001
- Optionally enable HTTPS

### Frontend

- Run `npm run dev`
- Open UI at `http://localhost:5173` (or similar port)
- Configure API proxy (dev proxy to `https://localhost:5001`)

---

## 7. Run Tests

```bash
dotnet test
```

Frontend tests (if available):

```bash
npm test
```

---

## 8. Docker Cleanup

To remove the dev environment:

```bash
docker stop rsgo-admin-dev
docker rm rsgo-admin-dev
docker volume rm rsgo-config-dev
```

---

Your development environment is now ready to work on ReadyStackGo!

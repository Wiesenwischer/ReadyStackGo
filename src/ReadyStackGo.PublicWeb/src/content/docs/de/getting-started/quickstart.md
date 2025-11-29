---
title: Schnellstart
description: ReadyStackGo in 5 Minuten starten
---

Diese Schnellstart-Anleitung bringt dich in wenigen Minuten von der Installation zum ersten laufenden Stack.

## Übersicht

```
Installation → Ersteinrichtung → Erster Stack
   (2 min)        (2 min)          (1 min)
```

---

## 1. Installation

Führe auf deinem Linux-Server aus:

```bash
curl -fsSL https://get.readystackgo.io/install.sh | sudo bash
```

Nach erfolgreicher Installation wird die URL angezeigt:

```
[OK] ReadyStackGo läuft jetzt!
URL: http://192.168.1.100:8080
```

📖 [Ausführliche Installationsanleitung](/getting-started/installation/)

---

## 2. Ersteinrichtung

1. **Browser öffnen** – Gehe zu `http://<server-ip>:8080`
2. **Admin erstellen** – Benutzername und Passwort festlegen
3. **Organisation benennen** – ID und Name eingeben
4. **Environment verbinden** – Docker-Socket bestätigen (oder überspringen)
5. **Abschließen** – Setup finalisieren

📖 [Ausführliche Setup-Anleitung](/getting-started/initial-setup/)

---

## 3. Erster Stack

1. **Einloggen** – Mit Admin-Account anmelden
2. **Stacks öffnen** – Im Menü auf "Stacks" klicken
3. **Deploy Custom** – Button oben rechts klicken
4. **Compose einfügen:**

```yaml
services:
  hello:
    image: nginx:alpine
    ports:
      - "8081:80"
```

5. **Stack Name:** `hello-world`
6. **Deploy** – Klicken und warten

Fertig! Öffne `http://<server-ip>:8081` im Browser.

📖 [Ausführliche Deployment-Anleitung](/getting-started/first-deployment/)

---

## Nächste Schritte

| Thema | Beschreibung |
|-------|--------------|
| [Installation](/getting-started/installation/) | Verschiedene Installationsmethoden |
| [Ersteinrichtung](/getting-started/initial-setup/) | Setup-Wizard im Detail |
| [Erster Stack](/getting-started/first-deployment/) | Stack-Deployment erklärt |
| [Dokumentation](/docs/) | Weitere Themen und Anleitungen |

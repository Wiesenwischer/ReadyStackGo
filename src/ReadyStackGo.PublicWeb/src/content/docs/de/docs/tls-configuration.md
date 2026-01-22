---
title: TLS-Konfiguration
description: Umfassende Anleitung zur Konfiguration von HTTPS, Zertifikaten und Reverse Proxy in ReadyStackGo
---

Diese Anleitung erklärt alle TLS-Konfigurationsoptionen in ReadyStackGo - von selbstsignierten Zertifikaten über eigene Zertifikate bis hin zu Let's Encrypt und Reverse Proxy-Szenarien.

## Übersicht

ReadyStackGo unterstützt verschiedene TLS/HTTPS-Konfigurationen:

| Option | Verwendung | Komplexität |
|--------|------------|-------------|
| **Selbstsigniert** | Entwicklung, Tests, internes Netzwerk | Keine Konfiguration nötig |
| **Eigenes Zertifikat** | Firmen-CA, gekaufte Zertifikate | Datei-Upload |
| **Let's Encrypt** | Produktionsumgebungen mit öffentlicher Domain | Domain-Validierung |
| **Reverse Proxy** | Hinter nginx, Traefik, HAProxy, etc. | Proxy-abhängig |

Alle Einstellungen befinden sich unter **Settings → TLS / Certificates** in der Web UI.

---

## Selbstsigniertes Zertifikat

### Was ist ein selbstsigniertes Zertifikat?

Ein selbstsigniertes Zertifikat wird von ReadyStackGo beim ersten Start automatisch generiert. Es verschlüsselt die Verbindung, wird aber nicht von einer vertrauenswürdigen Zertifizierungsstelle (CA) signiert. Browser zeigen daher eine Sicherheitswarnung an.

### Wann verwenden?

- **Lokale Entwicklung** - Verschlüsselung ohne CA-Setup
- **Interne Testsysteme** - Wenn Sicherheitswarnungen akzeptabel sind
- **Docker-Entwicklung** - Schneller Start ohne Zertifikatskonfiguration

### Funktionsweise

1. Beim ersten Start prüft ReadyStackGo, ob ein Zertifikat existiert
2. Falls nicht, wird automatisch ein selbstsigniertes Zertifikat generiert
3. Das Zertifikat wird unter `/app/config/tls/selfsigned.pfx` gespeichert
4. Gültigkeitsdauer: 365 Tage

### Zurück zum selbstsignierten Zertifikat

Falls du ein eigenes Zertifikat hochgeladen hast und zurück zum selbstsignierten wechseln möchtest:

1. Navigiere zu **Settings → TLS / Certificates**
2. Klicke auf **Configure Certificate**
3. Wähle **Reset to Self-Signed**
4. Bestätige die Aktion
5. **Starte ReadyStackGo neu**, um das neue Zertifikat zu laden

---

## Eigenes Zertifikat

### Wann verwenden?

- **Firmen-CA** - Interne Zertifizierungsstelle
- **Gekaufte Zertifikate** - Von einer öffentlichen CA (DigiCert, Comodo, etc.)
- **Wildcard-Zertifikate** - Ein Zertifikat für mehrere Subdomains

### Unterstützte Formate

ReadyStackGo akzeptiert zwei Zertifikatsformate:

| Format | Dateien | Typische Quelle |
|--------|---------|-----------------|
| **PFX/PKCS#12** | Eine .pfx/.p12 Datei mit Passwort | Windows-Export, IIS |
| **PEM** | Separate Zertifikat- und Schlüssel-Datei | Linux, OpenSSL, Let's Encrypt |

### Schritt-für-Schritt: PFX-Zertifikat hochladen

1. Navigiere zu **Settings → TLS / Certificates**
2. Klicke auf **Configure Certificate**
3. Wähle **Upload PFX Certificate**
4. Wähle die .pfx-Datei aus
5. Gib das Passwort ein (falls vorhanden)
6. Klicke auf **Upload**
7. **Starte ReadyStackGo neu**

:::tip[PFX ohne Passwort]
Falls dein PFX kein Passwort hat, lass das Passwortfeld leer.
:::

### Schritt-für-Schritt: PEM-Zertifikat hochladen

1. Navigiere zu **Settings → TLS / Certificates**
2. Klicke auf **Configure Certificate**
3. Wähle **Upload PEM Certificate**
4. Füge den Inhalt der Zertifikatsdatei ein (beginnt mit `-----BEGIN CERTIFICATE-----`)
5. Füge den Inhalt der Schlüsseldatei ein (beginnt mit `-----BEGIN PRIVATE KEY-----` oder `-----BEGIN RSA PRIVATE KEY-----`)
6. Klicke auf **Upload**
7. **Starte ReadyStackGo neu**

:::note[Zertifikatskette]
Falls du eine Zertifikatskette hast (Intermediate-Zertifikate), füge alle Zertifikate in der richtigen Reihenfolge in das Zertifikatsfeld ein: Erst das Server-Zertifikat, dann die Intermediate-Zertifikate.
:::

### Zertifikat mit OpenSSL erstellen

Falls du ein selbstsigniertes Zertifikat mit längerer Gültigkeit oder spezifischen Einstellungen benötigst:

```bash
# Schlüssel und Zertifikat generieren (10 Jahre gültig)
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 3650 -nodes \
  -subj "/CN=readystackgo.local"

# Zu PFX konvertieren (optional)
openssl pkcs12 -export -out certificate.pfx -inkey key.pem -in cert.pem
```

---

## Let's Encrypt

### Was ist Let's Encrypt?

Let's Encrypt ist eine kostenlose, automatisierte Zertifizierungsstelle. Die Zertifikate sind von allen gängigen Browsern vertraut und 90 Tage gültig. ReadyStackGo erneuert sie automatisch.

### Voraussetzungen

- **Öffentlich erreichbare Domain** - Let's Encrypt muss deine Domain validieren können
- **DNS-Eintrag** - Die Domain muss auf deinen Server zeigen
- **Port 80 oder DNS-Zugriff** - Je nach Challenge-Typ

### Challenge-Typen

Let's Encrypt verwendet Challenges um zu verifizieren, dass du die Domain kontrollierst:

| Challenge | Anforderung | Vorteile |
|-----------|-------------|----------|
| **HTTP-01** | Port 80 erreichbar | Einfachste Einrichtung |
| **DNS-01** | DNS-Zugriff | Wildcard-Support, kein Port 80 nötig |

### Schritt-für-Schritt: HTTP-01 Challenge

Diese Methode ist am einfachsten, wenn ReadyStackGo direkt aus dem Internet erreichbar ist.

**Voraussetzung:** Port 80 muss auf deinen Server zeigen (nicht nur Port 443).

1. Navigiere zu **Settings → TLS / Certificates**
2. Klicke auf **Configure Certificate**
3. Wähle **Let's Encrypt**
4. Gib folgende Daten ein:
   - **Domains:** Deine Domain(s), z.B. `rsgo.example.com`
   - **E-Mail:** Für Ablaufbenachrichtigungen
   - **Challenge Type:** HTTP-01
5. Optional: Aktiviere **Use Staging** zum Testen (keine echten Zertifikate)
6. Klicke auf **Request Certificate**
7. Warte auf die Validierung (wenige Sekunden bis Minuten)
8. **Starte ReadyStackGo neu**

:::note[Port 80 Weiterleitung]
Falls du einen Reverse Proxy nutzt, muss dieser Port 80 an ReadyStackGo weiterleiten, damit die HTTP-01 Challenge funktioniert.
:::

### Schritt-für-Schritt: DNS-01 Challenge (Manuell)

Diese Methode erfordert manuelles Erstellen von DNS-Einträgen, funktioniert aber auch für Wildcard-Domains.

1. Navigiere zu **Settings → TLS / Certificates**
2. Klicke auf **Configure Certificate**
3. Wähle **Let's Encrypt**
4. Gib folgende Daten ein:
   - **Domains:** Deine Domain(s), z.B. `*.example.com`
   - **E-Mail:** Für Ablaufbenachrichtigungen
   - **Challenge Type:** DNS-01
   - **DNS Provider:** Manual
5. Klicke auf **Request Certificate**
6. Die UI zeigt dir die benötigten TXT-Records:

```
Name: _acme-challenge.example.com
Value: abc123xyz...
```

7. Erstelle den TXT-Record bei deinem DNS-Provider
8. Warte auf DNS-Propagation (kann bis zu 24 Stunden dauern)
9. Klicke auf **Confirm DNS Challenge**
10. **Starte ReadyStackGo neu**

### Schritt-für-Schritt: DNS-01 Challenge (Cloudflare)

Mit Cloudflare werden DNS-Records automatisch erstellt und gelöscht.

1. Erstelle einen Cloudflare API-Token:
   - Gehe zu [Cloudflare Dashboard → Profile → API Tokens](https://dash.cloudflare.com/profile/api-tokens)
   - Klicke auf **Create Token**
   - Wähle **Edit zone DNS** als Template
   - Beschränke auf deine Zone
   - Kopiere den Token

2. Navigiere zu **Settings → TLS / Certificates**
3. Klicke auf **Configure Certificate**
4. Wähle **Let's Encrypt**
5. Gib folgende Daten ein:
   - **Domains:** Deine Domain(s)
   - **E-Mail:** Für Ablaufbenachrichtigungen
   - **Challenge Type:** DNS-01
   - **DNS Provider:** Cloudflare
   - **Cloudflare API Token:** Der kopierte Token
   - **Cloudflare Zone ID:** (Optional) Findest du im Cloudflare Dashboard unter Overview
6. Klicke auf **Request Certificate**
7. **Starte ReadyStackGo neu**

### Automatische Erneuerung

ReadyStackGo erneuert Let's Encrypt-Zertifikate automatisch:

- **Prüfintervall:** Alle 12 Stunden
- **Erneuerung:** 30 Tage vor Ablauf
- **Status:** Sichtbar unter Settings → TLS

Falls die automatische Erneuerung fehlschlägt:
1. Prüfe den Fehler unter **Settings → TLS**
2. Stelle sicher, dass die Challenge noch funktioniert
3. Bei DNS-01 Manual: Erstelle den neuen TXT-Record

### Staging vs. Production

Let's Encrypt hat strenge Rate-Limits für Production-Zertifikate. Zum Testen:

1. Aktiviere **Use Staging**
2. Teste die vollständige Konfiguration
3. Staging-Zertifikate werden von Browsern nicht vertraut (Warnung)
4. Deaktiviere Staging für das echte Zertifikat
5. Fordere ein neues Zertifikat an

---

## Reverse Proxy-Konfiguration

### Wann verwenden?

Wenn ReadyStackGo hinter einem Edge-Proxy läuft:

- **nginx** als Reverse Proxy
- **Traefik** für Container-Routing
- **HAProxy** für Load Balancing
- **Cloud Load Balancer** (AWS ALB, Azure App Gateway, etc.)

### SSL-Handling-Modi

ReadyStackGo unterstützt drei Modi für die SSL-Kommunikation mit dem Proxy:

#### SSL Termination

```
Client ──HTTPS──► Proxy ──HTTP──► ReadyStackGo
```

**Beschreibung:**
- Der Proxy terminiert SSL und entschlüsselt den Traffic
- ReadyStackGo empfängt unverschlüsselten HTTP-Traffic
- ReadyStackGo benötigt **kein** Zertifikat

**Wann verwenden:**
- Proxy verwaltet alle Zertifikate (z.B. Traefik mit Let's Encrypt)
- Einfachste Konfiguration
- Interne Verbindung (Proxy und ReadyStackGo im gleichen Netzwerk)

**Konfiguration:**

1. Navigiere zu **Settings → TLS / Certificates**
2. Aktiviere **Reverse Proxy Mode**
3. Wähle **SSL Termination**
4. **Starte ReadyStackGo neu**

**Nginx Beispiel:**
```nginx
server {
    listen 443 ssl;
    server_name rsgo.example.com;

    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;

    location / {
        proxy_pass http://readystackgo:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
    }
}
```

#### SSL Passthrough

```
Client ──HTTPS──► Proxy ──HTTPS──► ReadyStackGo
        (verschlüsselt durchgeleitet)
```

**Beschreibung:**
- Der Proxy leitet den verschlüsselten Traffic unverändert weiter
- ReadyStackGo terminiert SSL
- ReadyStackGo **benötigt** ein Zertifikat

**Wann verwenden:**
- End-to-End-Verschlüsselung erforderlich
- ReadyStackGo verwaltet sein eigenes Zertifikat
- Layer 4 Load Balancing (TCP-Proxy)

**Konfiguration:**

1. Konfiguriere ein Zertifikat in ReadyStackGo (selbstsigniert, eigenes, oder Let's Encrypt)
2. Navigiere zu **Settings → TLS / Certificates**
3. Aktiviere **Reverse Proxy Mode**
4. Wähle **SSL Passthrough**
5. **Starte ReadyStackGo neu**

**Nginx Beispiel (Stream-Modul):**
```nginx
stream {
    upstream readystackgo {
        server readystackgo:8443;
    }

    server {
        listen 443;
        proxy_pass readystackgo;
    }
}
```

**Traefik Beispiel:**
```yaml
# traefik.yml
entryPoints:
  websecure:
    address: ":443"

# Dynamic config
tcp:
  routers:
    rsgo:
      rule: "HostSNI(`rsgo.example.com`)"
      service: rsgo
      tls:
        passthrough: true
  services:
    rsgo:
      loadBalancer:
        servers:
          - address: "readystackgo:8443"
```

#### Re-Encryption

```
Client ──HTTPS──► Proxy ──HTTPS──► ReadyStackGo
        (terminiert)    (neu verschlüsselt)
```

**Beschreibung:**
- Der Proxy terminiert die Client-SSL-Verbindung
- Der Proxy erstellt eine neue SSL-Verbindung zu ReadyStackGo
- Beide Seiten benötigen Zertifikate

**Wann verwenden:**
- Compliance erfordert verschlüsselte interne Verbindungen
- Zero-Trust-Netzwerk
- Proxy und ReadyStackGo in verschiedenen Netzwerksegmenten

**Konfiguration:**

1. Konfiguriere ein Zertifikat in ReadyStackGo
2. Navigiere zu **Settings → TLS / Certificates**
3. Aktiviere **Reverse Proxy Mode**
4. Wähle **Re-Encryption**
5. **Starte ReadyStackGo neu**

:::tip[Selbstsigniert für Backend]
Bei Re-Encryption ist ein selbstsigniertes Zertifikat für ReadyStackGo oft ausreichend, da nur der Proxy es validieren muss. Konfiguriere den Proxy, dem Backend-Zertifikat zu vertrauen.
:::

**Nginx Beispiel:**
```nginx
server {
    listen 443 ssl;
    server_name rsgo.example.com;

    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;

    location / {
        proxy_pass https://readystackgo:8443;
        proxy_ssl_verify off;  # Für selbstsigniertes Backend-Zertifikat
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
    }
}
```

### Forwarded Headers

Bei SSL Termination und Re-Encryption verarbeitet ReadyStackGo automatisch X-Forwarded-* Header:

| Header | Beschreibung |
|--------|--------------|
| `X-Forwarded-For` | Echte Client-IP (wichtig für Logs und Rate-Limiting) |
| `X-Forwarded-Proto` | Original-Protokoll (http/https) für korrekte Redirects |
| `X-Forwarded-Host` | Original-Hostname für URL-Generierung |

Diese Headers werden automatisch vertraut, wenn der Reverse Proxy-Modus aktiviert ist.

:::caution[Sicherheitshinweis]
Aktiviere den Reverse Proxy-Modus **nur**, wenn ReadyStackGo tatsächlich hinter einem Proxy läuft. Bei direktem Internet-Zugriff können gefälschte Header zu Sicherheitsproblemen führen.
:::

### Vergleichstabelle SSL-Modi

| Aspekt | SSL Termination | SSL Passthrough | Re-Encryption |
|--------|-----------------|-----------------|---------------|
| Zertifikat in ReadyStackGo | Nicht nötig | Erforderlich | Erforderlich |
| Proxy-Zertifikat | Erforderlich | Nicht nötig | Erforderlich |
| Traffic zum Backend | HTTP | HTTPS | HTTPS |
| Forwarded Headers | Ja | Nein | Ja |
| Proxy kann Traffic lesen | Ja | Nein | Ja |
| Konfigurationskomplexität | Niedrig | Mittel | Mittel |

---

## HTTP-Port aktivieren/deaktivieren

ReadyStackGo kann optional auch HTTP (unverschlüsselt) anbieten:

1. Navigiere zu **Settings → TLS / Certificates**
2. Aktiviere oder deaktiviere **HTTP Enabled**
3. **Starte ReadyStackGo neu**

:::note[HTTPS Redirect]
Wenn HTTP aktiviert ist, werden Anfragen auf HTTP automatisch zu HTTPS weitergeleitet - außer im SSL Termination-Modus, wo der Backend-Traffic bereits HTTP ist.
:::

---

## Fehlerbehebung

### Zertifikat wird nicht geladen

**Problem:** Nach dem Upload wird das alte Zertifikat angezeigt.

**Lösung:** ReadyStackGo muss neu gestartet werden, damit das neue Zertifikat geladen wird.

### Let's Encrypt schlägt fehl

**Problem:** "Failed to validate domain"

**Mögliche Ursachen:**
1. **HTTP-01:** Port 80 nicht erreichbar
2. **DNS-01:** TXT-Record nicht erstellt oder noch nicht propagiert
3. **Domain zeigt nicht auf Server:** DNS-Eintrag prüfen

**Debug-Schritte:**
1. Prüfe den Fehler unter Settings → TLS
2. Teste Port 80 Erreichbarkeit: `curl http://deinedomain.com/.well-known/acme-challenge/test`
3. Prüfe DNS: `dig TXT _acme-challenge.deinedomain.com`

### Browser zeigt Sicherheitswarnung

**Bei selbstsigniertem Zertifikat:** Erwartet. Verwende Let's Encrypt oder ein CA-Zertifikat für Produktion.

**Bei Let's Encrypt:** Prüfe, ob du im Staging-Modus bist. Staging-Zertifikate sind nicht vertraut.

### Proxy erhält "Connection refused"

**Problem:** Der Reverse Proxy kann ReadyStackGo nicht erreichen.

**Prüfe:**
1. Läuft ReadyStackGo? `docker ps`
2. Richtiger Port? HTTP = 8080, HTTPS = 8443
3. Netzwerk-Konnektivität? `docker network ls`

---

## Weiterführende Links

- [Installation](/de/getting-started/installation/) - ReadyStackGo installieren
- [Stack Deployment](/de/docs/stack-deployment/) - Stacks deployen

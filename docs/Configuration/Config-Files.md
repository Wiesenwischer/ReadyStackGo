# Config Files (`rsgo.*.json`)

Dieses Dokument beschreibt alle zentralen Config-Dateien, die durch den Admin-Container verwaltet werden.

## Dateien

- `rsgo.system.json`
- `rsgo.security.json`
- `rsgo.tls.json`
- `rsgo.contexts.json`
- `rsgo.features.json`
- `rsgo.release.json`

Details sind in der Gesamtspezifikation erläutert – hier dient dieses Dokument als Kurzreferenz.

---

## rsgo.system.json

Speichert Wizard-Status, Organisation, Netzwerkname etc.

---

## rsgo.security.json

Speichert Benutzer, Passwörter (Hashes), JWT-Secret, OIDC-Konfiguration.

---

## rsgo.tls.json

Definiert TLS-Modus, Zertifikatspfad, Ports.

---

## rsgo.contexts.json & rsgo.connections.json

Definieren, welche Connection Strings verwendet werden – global oder pro Kontext.

---

## rsgo.features.json

Globale Feature Flags – werden als EnvVars exportiert.

---

## rsgo.release.json

Enthält die installierte Stack-Version und Kontextversionen.

# TLS System Documentation

Dieses Dokument beschreibt das TLS-/Zertifikatssystem von ReadyStackGo.

## Inhalt
1. Ziele
2. TLS-Modi
3. Bootstrap mit Self-Signed
4. Custom-Zertifikate
5. Integration in Gateway
6. Diagramme

---

## 1. Ziele

- Sofortige Absicherung mit Self-Signed-Zertifikaten
- Späterer Austausch gegen Custom-Zertifikate möglich
- TLS-Terminierung im Gateway
- Minimale Komplexität für den Betreiber

---

## 2. TLS-Modi

- `SelfSigned` – Default beim ersten Start
- `Custom` – PFX-Upload durch Admin

Definition in `rsgo.tls.json`.

---

## 3. Bootstrap mit Self-Signed

Beim ersten Start:

1. Prüfen, ob `rsgo.tls.json` existiert  
2. Falls nicht → Self-Signed-Zertifikat erzeugen  
3. PFX unter `/app/certs/selfsigned.pfx` ablegen  
4. `rsgo.tls.json` mit `mode = SelfSigned` schreiben  

---

## 4. Custom-Zertifikate

Über Admin-UI:

- Upload einer PFX-Datei
- Eingabe des Passworts
- Validierung (Ablaufdatum, Private Key, etc.)
- Update von `rsgo.tls.json` mit `mode = Custom`

---

## 5. TLS-Flow Diagramm

```mermaid
sequenceDiagram
    participant Admin as Admin User
    participant UI as RSGO UI
    participant API as RSGO API
    participant TLS as TLS Engine
    participant GW as edge-gateway

    Admin->>UI: öffnet TLS-Seite
    UI->>API: GET /admin/tls
    API-->>UI: tlsMode=SelfSigned

    Admin->>UI: lädt custom.pfx hoch
    UI->>API: POST /admin/tls/upload
    API->>TLS: Validate & Install
    TLS-->>API: OK
    API-->>UI: success

    Note over API,GW: nächstes Deployment startet Gateway mit Custom-PFX
```

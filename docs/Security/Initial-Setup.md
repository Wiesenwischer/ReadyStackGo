# Initial Setup Security

Dieses Dokument beschreibt das Sicherheitskonzept während der erstmaligen Einrichtung von ReadyStackGo.

## Überblick

Bei der ersten Installation ist ReadyStackGo noch nicht konfiguriert und hat keinen Admin-Benutzer. Die Wizard-Endpoints müssen daher ohne Authentifizierung zugänglich sein, um die initiale Einrichtung zu ermöglichen.

## Aktueller Sicherheitsmechanismus (v0.4)

### State-basierte Validierung

Der Wizard durchläuft definierte Zustände, die verhindern, dass Schritte übersprungen oder wiederholt werden:

| State | Beschreibung | Erlaubte Aktion |
|-------|-------------|-----------------|
| `NotStarted` | Keine Konfiguration vorhanden | Admin erstellen |
| `AdminCreated` | Admin existiert, aber keine Organisation | Organisation setzen |
| `OrganizationSet` | Organisation konfiguriert | Wizard abschließen |
| `Installed` | Wizard abgeschlossen | Keine Wizard-Aktionen |

### Endpoint-Schutz

- **Während des Wizards**: Endpoints sind anonym zugänglich (`AllowAnonymous`)
- **Nach Abschluss**: Wizard-Endpoints geben Fehler zurück, da der State nicht mehr passt
- **Normale API**: Erfordert JWT-Authentifizierung

### Einschränkungen

- Kein Zeitfenster-Limit für die Ersteinrichtung
- Theoretisch kann ein Angreifer bei ungesicherter Netzwerkumgebung den ersten Admin erstellen
- Keine IP-Bindung oder Setup-Token-Mechanismus

## Empfohlene Best Practices (v0.4)

### Netzwerksicherheit

1. **Lokale Installation**: ReadyStackGo zunächst nur lokal (localhost/127.0.0.1) zugänglich machen
2. **VPN/Firewall**: Bei Remote-Installation sicherstellen, dass nur autorisierte Clients Zugriff haben
3. **Schnelle Einrichtung**: Wizard unmittelbar nach der Installation abschließen

### Docker-Deployment

```bash
# Option 1: Nur localhost binden während der Einrichtung
docker run -p 127.0.0.1:5259:5259 readystackgo

# Option 2: Mit VPN/Firewall-geschütztem Netzwerk
docker run -p 5259:5259 readystackgo
# Sicherstellen, dass Port 5259 nur aus vertrauenswürdigen Netzwerken erreichbar ist
```

## Vergleich mit Portainer

| Feature | ReadyStackGo (v0.4) | Portainer |
|---------|---------------------|-----------|
| Anonyme Wizard-Endpoints | Ja | Ja |
| State-Validierung | Ja | Ja |
| Zeitfenster-Limit | Nein | Ja (5 Minuten) |
| API-Lockdown nach Timeout | Nein | Ja |
| Setup-Token | Nein | Nein |

## Geplante Verbesserungen (v0.5+)

### Wizard Timeout (geplant für v0.5)

- **5-Minuten-Zeitfenster** für die Admin-Erstellung nach Serverstart
- Nach Ablauf: API-Blockierung bis zum Neustart
- Status-Endpoint bleibt erreichbar für Timeout-Anzeige

### Weitere geplante Features

- **IP-Whitelist**: Nur bestimmte IPs können während Setup zugreifen
- **Secure Restart**: Möglichkeit, das Zeitfenster durch authentifizierten Neustart zu verlängern
- **Audit-Logging**: Protokollierung aller Setup-Versuche

## Technische Details

### Wizard-Endpoints

```
POST /api/wizard/admin        - Admin-Benutzer erstellen
POST /api/wizard/organization - Organisation setzen
POST /api/wizard/install      - Wizard abschließen
GET  /api/wizard/status       - Wizard-Status abfragen
```

### State-Prüfung

```csharp
// Beispiel: Admin kann nur erstellt werden wenn State = NotStarted
if (systemConfig.WizardState != WizardState.NotStarted)
{
    return new CreateAdminResponse
    {
        Success = false,
        Message = "Admin can only be created when wizard is NotStarted."
    };
}
```

## Sicherheitsempfehlungen

1. **Sofortige Einrichtung**: Wizard direkt nach Installation abschließen
2. **Netzwerkisolation**: Während der Einrichtung Zugriff beschränken
3. **TLS aktivieren**: Nach der Einrichtung HTTPS konfigurieren
4. **Regelmäßige Updates**: ReadyStackGo aktuell halten für Security-Patches

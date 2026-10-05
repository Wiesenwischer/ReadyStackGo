---
title: Theme-Pakete
description: Wie Farb-Themes in ReadyStackGo funktionieren, wie man ein eigenes Theme-Paket baut und es einem Container mitgibt.
---

Das Aussehen der Weboberfläche von ReadyStackGo kommt aus **Theme-Paketen**. Ein Theme-Paket ist reines CSS: ein Satz Werte für die Design-Tokens der Oberfläche, für den hellen und den dunklen Modus. Es braucht weder Code noch einen Build, so kann jeder ein Theme erstellen und einer laufenden Installation mitgeben.

Das Theme wählt man unter **Settings → Appearance**. Die Wahl wird im Browser gespeichert. Den Modus wählt man dort getrennt davon: Light, Dark oder System, das dem Betriebssystem folgt. Der Knopf in der Kopfzeile schaltet weiterhin zwischen hell und dunkel um.

## Eingebaute Themes

| Id | Name | Beschreibung |
|----|------|--------------|
| `turquoise` | Turquoise | Standard. Die Farben des ReadyStackGo-Schriftzugs. |
| `pastel-green` | Pastel Green | Helles Pastellgrün mit demselben orangefarbenen Akzent. |
| `classic` | Classic | Das bisherige blaue Aussehen von ReadyStackGo. |

## Format eines Theme-Pakets

Ein Theme-Paket ist ein Ordner, dessen Name die Id des Themes ist:

```
my-theme/
├── theme.json
└── theme.css
```

### theme.json

```json
{
  "id": "my-theme",
  "name": "My Theme",
  "description": "Short description shown on the theme card.",
  "order": 10
}
```

| Feld | Beschreibung |
|------|--------------|
| `id` | Eindeutige Id. Muss `^[a-z0-9][a-z0-9-]{0,39}$` entsprechen (Kleinbuchstaben, Ziffern und Bindestriche, höchstens 40 Zeichen, nicht mit Bindestrich beginnend) und **gleich dem Ordnernamen** sein. |
| `name` | Anzeigename unter Settings → Appearance. |
| `description` | Kurzbeschreibung auf der Theme-Karte. |
| `order` | Reihenfolge in der Auswahl (aufsteigend). |

### theme.css

`theme.css` setzt nur CSS-Variablen mit dem Präfix `--rsgo-`. Sie enthält zwei Blöcke, einen für den hellen und einen für den dunklen Modus:

```css
/* Heller Modus */
[data-theme="my-theme"] {
  --rsgo-bg-page: #F3FBFB;
  --rsgo-primary-default: #1AD3D6;
  /* ... alle weiteren Tokens ... */
}

/* Dunkler Modus */
[data-theme="my-theme"][data-mode="dark"] {
  --rsgo-bg-page: #060B11;
  --rsgo-primary-default: #00CED1;
  /* ... alle weiteren Tokens ... */
}
```

Verwende genau diese Selektoren. Der dunkle Block hängt an keinem umgebenden Element, so wirkt das Theme am `<html>`-Element und ebenso in den Farbkugeln der Theme-Auswahl, die jedes Theme hell und dunkel nebeneinander zeigen. Beide Blöcke müssen **alle** Tokens setzen:

- die semantischen Tokens, z. B. `--rsgo-bg-page`, `--rsgo-bg-surface`, `--rsgo-text-primary`, `--rsgo-text-brand`, `--rsgo-primary-default`, `--rsgo-accent-go`, `--rsgo-focus-ring`, `--rsgo-nav-*`, `--rsgo-status-*` und `--rsgo-logo-*`,
- die Farbskalen `--rsgo-brand-25` … `--rsgo-brand-950` und `--rsgo-gray-25` … `--rsgo-gray-950`.

Optional kann ein Theme `--rsgo-font-sans`, `--rsgo-font-display` und `--rsgo-radius-*` setzen.

:::tip[Mit einer Vorlage beginnen]
Am einfachsten kopiert man das eingebaute Theme Turquoise und ändert dessen Werte: [`themes/turquoise/theme.css`](https://github.com/Wiesenwischer/ReadyStackGo/blob/main/src/ReadyStackGo.WebUi/apps/rsgo-generic/public/themes/turquoise/theme.css). Dort stehen alle Tokens für beide Modi.
:::

Achten Sie darauf, dass Texte lesbar bleiben: Textfarben sollten gegenüber ihren Flächen einen Kontrast von mindestens 4,5 : 1 erreichen, Ränder und der Fokus-Ring mindestens 3 : 1.

## Ein Theme einem Container mitgeben

Neben den eingebauten Themes lädt ReadyStackGo beim Start Theme-Pakete aus einem Verzeichnis. Standard ist `/app/themes` im Container; dort bindet man die eigenen Pakete ein:

```yaml
services:
  readystackgo:
    image: wiesenwischer/readystackgo:latest
    volumes:
      - ./themes:/app/themes:ro
    environment:
      - Themes__Default=my-theme
```

Jeder Unterordner des Verzeichnisses ist ein Paket. Ein Paket aus dem Verzeichnis mit derselben Id wie ein eingebautes Theme ersetzt dieses. Ungültige Pakete (fehlende Dateien, ungültige Id, Id ungleich Ordnername) werden mit einer Warnung im Log übersprungen. Nach dem Hinzufügen oder Ändern eines Pakets den Container neu starten; ein neues Image ist nicht nötig.

### Konfiguration

| Umgebungsvariable | Beschreibung | Standard |
|-------------------|--------------|----------|
| `Themes__Path` | Verzeichnis mit zusätzlichen Theme-Paketen. Darf fehlen. | `/app/themes` |
| `Themes__Enabled` | Komma-Liste der Theme-Ids, die angeboten werden. Leer bedeutet: alle gefundenen Themes. | (leer) |
| `Themes__Default` | Theme, das gilt, solange ein Browser noch keines gewählt hat (oder seine Wahl nicht mehr angeboten wird). | `turquoise`; fehlt es, das erste nach `order` |

Beispiel: nur das eigene Theme anbieten:

```bash
Themes__Enabled=my-theme
Themes__Default=my-theme
```

Wird nur **ein** Theme angeboten, blendet Settings → Appearance die Theme-Auswahl aus und zeigt nur den Umschalter für den Modus.

## API

Die Weboberfläche lädt die Themes über zwei Endpunkte. Beide sind ohne Anmeldung erreichbar, denn auch die Anmeldeseite ist gestaltet.

| Endpunkt | Antwort |
|----------|---------|
| `GET /api/themes` | `{ "default": "<id>", "themes": [{ "id", "name", "description", "cssUrl" }] }` |
| `GET /api/themes/{id}/theme.css` | Die `theme.css` des Pakets (`text/css`) |

## Sicherheit

Ein Theme-Paket ist CSS, das jeder Benutzer der Installation in seinen Browser lädt. Verwenden Sie nur Pakete aus Quellen, denen Sie vertrauen, und prüfen Sie ihren Inhalt, bevor Sie sie einbinden. Für die Pakete im Theme-Verzeichnis ist der Betreiber der Installation verantwortlich.

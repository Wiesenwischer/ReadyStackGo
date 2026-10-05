# Themes

ReadyStackGo lädt seine Farb-Themes zur Laufzeit als **Theme-Pakete**. Ein Paket ist reines CSS mit einer kleinen
Beschreibung, ohne Code und ohne Build. So kann eine Installation zusätzliche Themes mitbringen, eingebaute ersetzen
oder die Auswahl auf ein einziges Theme beschränken, ohne dass die App neu gebaut wird.

Die App fragt beim Start `GET /api/themes` ab, bindet die Stylesheets der angebotenen Themes ein und setzt
`data-theme="<id>"` am `<html>`. Hell und Dunkel (`.dark`) sind davon unabhängig; jedes Paket liefert beide Varianten.

## Paketformat

Ein Paket ist ein Ordner `<id>/` mit genau diesen beiden Dateien:

```
<id>/
├── theme.json
└── theme.css
```

### theme.json

```json
{
  "id": "turquoise",
  "name": "Turquoise",
  "description": "Default. The colors of the ReadyStackGo wordmark.",
  "order": 1
}
```

| Feld | Pflicht | Bedeutung |
|---|---|---|
| `id` | ja | Muster `^[a-z0-9][a-z0-9-]{0,39}$` (Kleinbuchstaben, Ziffern, Bindestrich, max. 40 Zeichen). Muss **genau dem Ordnernamen** entsprechen. |
| `name` | nein | Anzeigename in Settings → Appearance. Fehlt er, wird die `id` angezeigt. |
| `description` | nein | Kurzer Text unter dem Namen. |
| `order` | nein | Reihenfolge (aufsteigend, bei Gleichstand nach `id`). Fehlt er, steht das Theme am Ende. |

### theme.css

- Setzt **nur CSS Custom Properties `--rsgo-*`**, keine Selektoren auf Elemente oder Klassen der App.
- Heller Block mit dem Selektor `[data-theme="<id>"]`.
- Dunkler Block mit dem Selektor `.dark [data-theme="<id>"], .dark[data-theme="<id>"]`.
  Beide Selektoren sind nötig: der erste greift in einem Vorschau-Container (z. B. die Miniatur auf der Theme-Karte),
  der zweite am `<html>`, das selbst `.dark` und `data-theme` trägt.
- Beide Blöcke setzen **alle** Tokens unten. Optional zusätzlich `--rsgo-font-sans`, `--rsgo-font-display` und
  `--rsgo-radius-*`.

**Semantische Tokens (38):**

| Gruppe | Variablen |
|---|---|
| Flächen | `--rsgo-bg-page`, `--rsgo-bg-surface`, `--rsgo-bg-raised` |
| Ränder | `--rsgo-border-default`, `--rsgo-border-strong` |
| Text | `--rsgo-text-primary`, `--rsgo-text-secondary`, `--rsgo-text-muted`, `--rsgo-text-brand`, `--rsgo-text-on-primary` |
| Primary | `--rsgo-primary-default`, `--rsgo-primary-hover`, `--rsgo-primary-subtle` |
| Akzent „Go“ | `--rsgo-accent-go`, `--rsgo-accent-go-hover`, `--rsgo-accent-go-text`, `--rsgo-text-on-go` |
| Fokus | `--rsgo-focus-ring` |
| Navigation | `--rsgo-nav-bg`, `--rsgo-nav-text`, `--rsgo-nav-text-muted`, `--rsgo-nav-hover-bg`, `--rsgo-nav-active-bg`, `--rsgo-nav-active-text`, `--rsgo-nav-active-marker`, `--rsgo-nav-border` |
| Status | `--rsgo-status-healthy`, `--rsgo-status-healthy-bg`, `--rsgo-status-degraded`, `--rsgo-status-degraded-bg`, `--rsgo-status-unhealthy`, `--rsgo-status-unhealthy-bg`, `--rsgo-status-unknown`, `--rsgo-status-unknown-bg` |
| Logo | `--rsgo-logo-ready`, `--rsgo-logo-go`, `--rsgo-logo-stack`, `--rsgo-logo-stack-on-nav` |

**Skalen:** Die bestehenden Tailwind-Klassen `brand-*` und `gray-*` der App lesen diese Skalen, deshalb müssen sie
vollständig gesetzt sein.

- `--rsgo-brand-25`, `-50`, `-100`, `-200`, `-300`, `-400`, `-500`, `-600`, `-700`, `-800`, `-900`, `-950`
- `--rsgo-gray-25`, `-50`, `-100`, `-200`, `-300`, `-400`, `-500`, `-600`, `-700`, `-800`, `-900`, `-950`
- `--rsgo-gray-dark`

Die eingebauten Pakete unter `src/ReadyStackGo.WebUi/apps/rsgo-generic/public/themes/` (`turquoise`, `pastel-green`,
`classic`) sind vollständige Beispiele.

## Herkunft der Pakete

1. **Eingebaut:** `wwwroot/themes/<id>/`. Der WebUi-Build kopiert `apps/rsgo-generic/public/themes/` dorthin.
   Der Host setzt den Pfad aus `WebRootPath`; ohne Host-Wert gilt `<AppContext.BaseDirectory>/wwwroot/themes`.
   Überschreibbar mit `Themes:BuiltInPath` (vor allem für Tests).
2. **Verzeichnis des Betreibers:** `Themes:Path`, Standard `/app/themes`. Das Verzeichnis darf fehlen. Ein Paket mit
   derselben `id` wie ein eingebautes **ersetzt** das eingebaute.

Ungültige Pakete werden mit einer Warnung im Log übersprungen: ungültige `id` oder Ordnername, `id` ≠ Ordner, fehlende
`theme.json` oder `theme.css`, nicht lesbares JSON. Ein ungültiges Paket im Verzeichnis des Betreibers ersetzt kein
gültiges eingebautes.

Der `ThemeCatalog` hält das Ergebnis des Durchsuchens (Metadaten und Dateipfade) **30 Sekunden** im Speicher. Neue,
geänderte oder entfernte Pakete im Verzeichnis werden also spätestens nach 30 Sekunden sichtbar, ohne Neustart. Den
Inhalt von `theme.css` liest jeder Abruf frisch von der Platte.

## Konfiguration

| Einstellung | Umgebungsvariable | Standard | Bedeutung |
|---|---|---|---|
| `Themes:Path` | `Themes__Path` | `/app/themes` | Zusätzliches Verzeichnis mit Paketen. |
| `Themes:Enabled` | `Themes__Enabled` | leer | Komma-Liste angebotener Ids; leer = alle gefundenen. Leerzeichen werden ignoriert, unbekannte Ids mit Warnung übergangen. Groß-/Kleinschreibung zählt. |
| `Themes:Default` | `Themes__Default` | leer | Standard-Theme, vom Betreiber festgelegt. Leer = das Standard-Theme der Installation (siehe unten). |
| `Themes:BuiltInPath` | `Themes__BuiltInPath` | `WebRootPath/themes` | Verzeichnis der eingebauten Pakete. |

### Standard-Theme

Das Standard-Theme gilt für jeden Browser, der noch kein Theme gewählt hat. Es ist das erste davon, das angeboten wird:

1. `Themes:Default`, wenn der Betreiber es setzt.
2. Das Standard-Theme der Installation, gespeichert in `rsgo.system.json` (`defaultTheme`). Eine neue Installation
   bekommt beim Abschluss des Wizards `turquoise`. Eine Installation, die vor den Theme-Paketen eingerichtet wurde,
   bekommt beim ersten Start nach dem Update `classic` und behält so ihr bisheriges Aussehen.
3. `turquoise`.
4. Das erste Theme nach `order` (dann `id`).

Beispiel `docker-compose.yml`:

```yaml
    volumes:
      - ./themes:/app/themes:ro
    environment:
      - Themes__Enabled=turquoise,my-theme
      - Themes__Default=my-theme
```

## API

Beide Endpunkte sind anonym, weil auch die Anmeldeseite gestaltet ist.

**`GET /api/themes`**

```json
{
  "default": "turquoise",
  "themes": [
    { "id": "turquoise", "name": "Turquoise", "description": "…", "cssUrl": "/api/themes/turquoise/theme.css" }
  ]
}
```

`themes` ist nach `order`, dann `id` sortiert. Wird kein Theme angeboten, ist die Liste leer und `default` `null`;
die App fällt dann auf die Türkis-Werte in `index.css` zurück.

**`GET /api/themes/{id}/theme.css`**

- `200` mit `Content-Type: text/css; charset=utf-8`, `Cache-Control: no-cache` und einem `ETag` (Hash des Inhalts).
- `304 Not Modified`, wenn `If-None-Match` zum aktuellen `ETag` passt.
- `404` für ungültige, unbekannte oder nicht angebotene Ids. Die `id` wird gegen das Muster geprüft, bevor auf eine
  Datei zugegriffen wird; ausgeliefert werden nur Dateien aus dem Katalog, ein Pfad-Ausbruch (`..`, `/`, Großbuchstaben)
  ist damit nicht möglich.

Code: `Application/Services/IThemeCatalog.cs`, `Infrastructure/Services/Themes/ThemeCatalog.cs` und `ThemeOptions.cs`,
`Api/Endpoints/Themes/`.

## Eine Distribution mit eigenem Theme

Eine Distribution oder ein Betreiber, der nur sein eigenes Erscheinungsbild anbieten will, braucht keinen eigenen
Build der Oberfläche:

1. Paket `my-brand/` mit `theme.json` und `theme.css` nach dem Format oben anlegen.
2. Paket in das Verzeichnis `Themes:Path` legen (im Image kopieren oder als Volume nach `/app/themes` mounten).
3. `Themes__Enabled=my-brand` und `Themes__Default=my-brand` setzen.

Bietet die Installation nur ein Theme an, blendet Settings → Appearance die Theme-Auswahl aus und zeigt nur den
Umschalter Light/Dark.

## Sicherheit

CSS aus dem Verzeichnis des Betreibers wird **nicht inhaltlich geprüft**, nur die Form (Id, Ordner, Dateien). CSS führt
keinen Code aus, kann aber die Oberfläche unlesbar oder unbedienbar machen, etwa durch fehlende Tokens, schlechten
Kontrast oder Regeln außerhalb der `--rsgo-*`-Variablen. Die Verantwortung für fremde Pakete liegt beim Betreiber. Das
Verzeichnis sollte nur für den Betreiber beschreibbar sein und kann schreibgeschützt gemountet werden (`:ro`).

## Minimales Beispielpaket

`my-brand/theme.json`:

```json
{ "id": "my-brand", "name": "My Brand", "description": "Corporate colors.", "order": 10 }
```

`my-brand/theme.css` (gekürzt; ein echtes Paket setzt in beiden Blöcken alle 38 Tokens und beide Skalen):

```css
[data-theme="my-brand"] {
  --rsgo-bg-page: #F7F8FA;
  --rsgo-bg-surface: #FFFFFF;
  --rsgo-text-primary: #111827;
  --rsgo-primary-default: #2563EB;
  --rsgo-brand-500: #2563EB;
  --rsgo-gray-900: #111827;
  /* … */
}

.dark [data-theme="my-brand"],
.dark[data-theme="my-brand"] {
  --rsgo-bg-page: #0B1120;
  --rsgo-bg-surface: #111827;
  --rsgo-text-primary: #F3F4F6;
  --rsgo-primary-default: #3B82F6;
  --rsgo-brand-500: #3B82F6;
  --rsgo-gray-900: #111827;
  /* … */
}
```

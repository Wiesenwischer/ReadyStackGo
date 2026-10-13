# Entwurf: Lokales Passwort nur nach erneuter Anmeldung

> **Verworfen** (Marcus, 13.10.2026): Vorhaben #506 ist geschlossen, Plan-PR #508 nicht gemergt. Ein Admin-Token erlaubt ohnehin,
> Container mit Zugriff auf den Docker-Host zu deployen; die erneute Anmeldung rechtfertigt keinen eigenen Ablauf. Das erste
> lokale Passwort setzt ein angemeldetes Konto weiter einmalig selbst. Diese Datei bleibt als Material.

- **Vorhaben:** #506
- **Spezifikation:** [`docs/specs/lokales-passwort-erneute-anmeldung.md`](../../lokales-passwort-erneute-anmeldung.md)
- **Figma-Datei:** [ReadyStackGo Design](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1) (`RxVNdSKNs7PpJqkYgvb6a1`), Seite „App“ (`2:99`)
- **Freigabe:** freigegeben mit dem Merge von PR #507
- **Grundlage:** Profil-Rahmen aus Vorhaben #498 (`177:4834` hell, `177:5913` dunkel, `docs/specs/identity-provider-vorlagen/entwurf/`). Die neuen Rahmen sind Kopien davon; geändert ist nur die Karte „Set a local password“ und im Rahmen mit mehreren Providern die Karte „Connected accounts“ und der Text der Warnung.

## Rahmen

Ganze Seite, 1440 breit:

| Oberfläche | Zustand | Node-ID | Link | Bild |
|---|---|---|---|---|
| Profil, vor der Bestätigung | ein Provider (WYSCH) | `545:5499` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-5499) | `profil-bestaetigen.png` |
| Profil, vor der Bestätigung (dunkel) | wie oben | `545:6819` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6819) | `profil-bestaetigen-dunkel.png` |
| Profil, vor der Bestätigung | mehrere Provider (WYSCH, Company SSO) | `545:5683` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-5683) | `profil-bestaetigen-mehrere-provider.png` |
| Profil, vor der Bestätigung (dunkel) | wie oben | `545:6899` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6899) | `profil-bestaetigen-mehrere-provider-dunkel.png` |
| Profil, bestätigt | Formular mit Hinweis auf das Zeitfenster | `545:5897` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-5897) | `profil-bestaetigt.png` |
| Profil, bestätigt (dunkel) | wie oben | `545:6985` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6985) | `profil-bestaetigt-dunkel.png` |

Nur die Karte „Set a local password“ (Rahmen 1150 breit, Karte 1086 wie auf der Seite), jeweils hell und dunkel:

| Zustand | Rahmenname (Suffix) | Node-ID hell | Node-ID dunkel | Bilder |
|---|---|---|---|---|
| Zeitfenster abgelaufen | `– expired` | `545:6573` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6573)) | `545:6766` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6766)) | `karte-abgelaufen.png`, `karte-abgelaufen-dunkel.png` |
| Anmeldung beim Provider abgebrochen | `– cancelled` | `545:6607` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6607)) | `545:6775` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6775)) | `karte-abgebrochen.png`, `karte-abgebrochen-dunkel.png` |
| Provider nicht erreichbar | `– provider not reachable` | `545:6641` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6641)) | `545:6784` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6784)) | `karte-nicht-erreichbar.png`, `karte-nicht-erreichbar-dunkel.png` |
| anderes Konto beim Provider | `– different account` | `545:6675` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6675)) | `545:6793` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6793)) | `karte-anderes-konto.png`, `karte-anderes-konto-dunkel.png` |
| Anmeldung nicht frisch (`auth_time` fehlt oder zu alt) | `– sign-in not recent` | `545:6709` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6709)) | `545:6802` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6802)) | `karte-nicht-frisch.png`, `karte-nicht-frisch-dunkel.png` |
| kein nutzbarer Provider (ausgeschaltet oder entfernt) | `– no provider available` | `545:6743` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6743)) | `545:6811` ([Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6811)) | `karte-kein-provider.png`, `karte-kein-provider-dunkel.png` |

## Komponenten

Keine neuen Komponenten, keine neuen Varianten. Wiederverwendet aus #498:

- `Alert` (`169:309`): `Tone=Success` `169:286`, `Warning` `169:293`, `Error` `169:301`, mit `Show body`
- `Provider Button` (`170:298`), `State=Default` `170:277`, Logo per Instanztausch: `Provider Logo/WYSCH` `168:246`, `Provider Logo/Generic OIDC` `168:247`; Breite 320. Hover und Fokus sind die vorhandenen Varianten `170:284`, `170:291`
- `Provider Mark` `Provider=Generic OIDC` `168:261` (zweite Zeile in „Connected accounts“)
- `Input`, `Button` und die Seitenteile wie im Profil-Rahmen von #498

## Geänderte Tokens und Variablen

Keine. Die dunklen Rahmen setzen nur den Modus „Turquoise Dark“ (`2:2`) der Sammlung „Theme“, wie `177:5913`.

## Texte (englisch)

- Vor der Bestätigung: „Before you set a password, confirm it’s you: sign in with WYSCH again. ReadyStackGo then lets you set the password for 5 minutes.“ und der Knopf „Confirm with WYSCH“. Bei mehreren Providern: „… sign in again with one of your connected providers. …“ und je Provider „Confirm with <Name>“.
- Bestätigt: Alert „Confirmed with WYSCH“, „Set your password within 5 minutes. After that, confirm again.“, darunter das Formular aus #498.
- Abgelaufen: „Confirmation expired“, „More than 5 minutes have passed since you confirmed with WYSCH. Confirm again to set the password.“
- Abgebrochen: „Sign-in cancelled“, „You cancelled the sign-in at WYSCH, so no password was set. Confirm again to continue.“
- Nicht erreichbar: „WYSCH is not reachable“, „ReadyStackGo could not reach <Adresse> (<Grund>). Try again later. If WYSCH stays unavailable, set the password on the server: run rsgo admin set-password <user> inside the container.“
- Anderes Konto: „Signed in with a different account“, „The WYSCH account you signed in with isn’t linked to <user>. Sign in with the linked account and try again.“
- Nicht frisch: „No new sign-in“, „WYSCH didn’t confirm a new sign-in. Confirm again and enter your password or passkey at WYSCH.“
- Kein Provider: „No provider to confirm with“, „WYSCH is disabled, so you can’t confirm it’s you here. Set the password on the server: run rsgo admin set-password <user> inside the container.“ Ohne Knopf.

## Interaktion und Navigation

- „Confirm with <Provider>“ verlässt die Seite zum Provider (erneute Anmeldung mit `max_age=0`). Die Rückkehr landet wieder im Profil, mit dem Zustand „Confirmed with …“ oder einem der Fehlerzustände in derselben Karte.
- Fehler und „abgelaufen“ ersetzen die Erklärung durch den Alert; der Knopf darunter startet die Bestätigung erneut. Bei mehreren Providern stehen dort alle Knöpfe wie im Rahmen `545:5683`.
- Läuft das Fenster ab, während das Formular offen ist, zeigt die Karte beim Absenden (403 `reauthentication_required`) den Zustand „Confirmation expired“.
- Eingaben mit Maus und Tastatur; Fokusreihenfolge: Knöpfe von oben nach unten, nach der Bestätigung „New password“, „Confirm new password“, „Set password“. Ein Ladezustand entfällt: Der Knopf verlässt die Seite sofort.
- Bildschirmgröße wie das Profil aus #498 (Desktop 1440).

## Festlegungen ohne Vorgabe der Spezifikation

- Die Restzeit steht als fester Text im Alert („within 5 minutes“), ohne laufenden Countdown. Die Zahl folgt dem Zeitfenster, das der Plan festlegt (Spezifikation, „Offene Fragen“, Vorschlag 5 Minuten).
- Bestätigt wird mit dem `Provider Button` der Anmeldeseite (Logo und „Confirm with <Name>“), nicht mit dem primären `Button`, weil der Knopf zum Provider führt.
- Mit mehreren Providern ist „Unlink“ in „Connected accounts“ möglich (Regel aus `User.UnlinkExternalIdentity`: gesperrt nur ohne Passwort und mit höchstens einer Verknüpfung); der Hinweis darunter entfällt, und die Warnung nennt beide Provider.

## Hinweise für die Umsetzung

- Nichts im Designsystem nachzuziehen: keine neuen Tokens, Komponenten oder Varianten.
- Name, Adresse, Grund und Benutzername in den Texten sind Beispiele (WYSCH, alex) und kommen zur Laufzeit aus Provider und Konto.

## Offene Fragen

Keine für den Entwurf. Länge des Zeitfensters und Ort der Bestätigung klärt der Plan (Spezifikation).

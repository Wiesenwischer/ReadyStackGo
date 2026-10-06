---
title: Single Sign-On
description: Anmeldung mit WYSCH oder jedem OpenID-Connect-Anbieter. Provider im geführten Einrichtungslauf hinzufügen, testen, sich nicht aussperren und Notzugang aus dem Container.
---

ReadyStackGo kann Benutzer über einen **Identity Provider** anmelden. **WYSCH** wird von Haus aus unterstützt: Ein geführter Einrichtungslauf verbindet die Installation mit WYSCH ID, ohne dass Adressen, Client-IDs oder Secrets abgetippt werden. Jeder andere **OpenID-Connect**-Anbieter (Keycloak, Entra ID, …) funktioniert über die Vorlage **Generic OIDC**.

Wer sich anmelden darf, ändert sich nicht: bestehende Benutzer und Personen mit offener Einladung (siehe [Benutzerzugang](/de/docs/configuration/user-access/)). Rollen vergibt immer ReadyStackGo, nie der Anbieter.

## Anmeldung mit WYSCH bei der Ersteinrichtung

Der Setup-Wizard beginnt mit **How do you want to sign in?**:

- **Built-in sign-in** legt den ersten Administrator mit Benutzername und Passwort an, wie bisher.
- **WYSCH** verbindet die Installation mit WYSCH und meldet Sie mit Ihrem WYSCH-Konto an. Dieses Konto wird erster Systemadministrator, **ohne lokales Passwort**.

![Anmeldeart im Setup-Wizard](/images/docs/sso-01-wizard-method.png)

1. Bestätigen Sie die **Adresse dieser Installation**. ReadyStackGo schlägt die Adresse aus dem Browser vor und speichert sie als Basis-Adresse.
2. Klicken Sie **Connect with WYSCH**. Melden Sie sich bei WYSCH an und bestätigen Sie die Verbindung.
3. Sie kommen automatisch zurück, ReadyStackGo meldet Sie mit WYSCH an und zeigt **Signed in as …**. Danach folgt wie gewohnt der E-Mail-Schritt.

:::caution[HTTPS erforderlich]
WYSCH verbindet nur Installationen, die unter einer `https://`-Adresse erreichbar sind; `http://` ist nur für `localhost` erlaubt. Läuft Ihre Installation unter `http://server:8080`, stellen Sie einen Reverse Proxy mit Zertifikat davor oder nutzen Sie die integrierte Anmeldung und fügen WYSCH später hinzu.
:::

Ist WYSCH nicht erreichbar, wird die Verbindung abgebrochen oder läuft der Anmeldelauf ab, erklärt der Wizard den Grund und bietet **Use built-in sign-in instead** an. Ein Lauf, der im Zeitfenster des Setups beginnt, darf bis zu 15 Minuten dauern, etwa um zuerst ein WYSCH-Konto anzulegen.

## Provider hinzufügen

Unter **Settings → Single Sign-On** sehen Sie alle Provider mit Status und letztem Ergebnis und fügen mit **Add provider** neue hinzu.

![Single-Sign-On-Provider](/images/docs/sso-02-provider-list.png)

Der Einrichtungslauf geht in Schritten; welche erscheinen, hängt von der Vorlage ab:

| Schritt | WYSCH | Generic OIDC |
|---------|-------|--------------|
| **Template** | WYSCH wählen | Generic OIDC wählen |
| **Provider address** | fest in der Vorlage | Issuer-URL eingeben; ReadyStackGo prüft sie sofort |
| **This installation** | Adresse von ReadyStackGo und der Provider-Name (Teil der Redirect-URI) | ebenso |
| **Connect / Register** | **Connect with WYSCH**: bei WYSCH anmelden und bestätigen, Client-ID und Secret kommen automatisch | **Redirect-URI** zum Anbieter kopieren, dann Client-ID und Secret eintragen |
| **Test** | Prüfungen und Test-Anmeldung | Prüfungen und Test-Anmeldung |
| **Save** | Anzeigename, einschalten, unbestätigten E-Mail-Adressen vertrauen | ebenso |

Der Provider-Name wird Teil der Redirect-URI `https://<Ihre Adresse>/api/auth/oidc/<name>/callback` und lässt sich später nicht ändern.

## Provider testen

Der Test hat zwei Stufen:

- **Prüfungen ohne Anmeldung** laufen vom Server aus: Discovery-Dokument erreichbar, Issuer passt zur Adresse des Anbieters, Endpunkte vorhanden, Pushed Authorization Requests (PAR), wo verlangt, sowie Client-ID und Secret (über einen PAR-Aufruf, wenn der Anbieter PAR anbietet). Jede fehlgeschlagene Prüfung sagt, was falsch ist und was zu tun ist.
- **Test-Anmeldung**: Sie melden sich einmal beim Anbieter an. ReadyStackGo zeigt, welche Angaben angekommen sind (Subject, E-Mail und ob sie bestätigt ist, Benutzername, Anzeigename) und ob sie reichen. Die Test-Anmeldung meldet Sie nicht bei ReadyStackGo an und verknüpft kein Konto.

![Test mit den Angaben der Test-Anmeldung](/images/docs/sso-03-test-sign-in.png)

Einschalten lässt sich ein Provider **erst nach bestandenen Prüfungen und einer erfolgreichen Test-Anmeldung**. Ohne bestandenen Test können Sie ihn ausgeschaltet speichern und später mit **Test** in der Liste prüfen.

## Neu koppeln

Wird die Verbindung in WYSCH gelöscht, scheitern Anmeldungen, und die Liste zeigt **Reconnect needed**. **Reconnect** koppelt die Installation neu und ersetzt Client-ID und Secret. Die neuen Zugangsdaten gelten nach einer erfolgreichen Test-Anmeldung.

## Zuordnung der Konten

Bei der Anmeldung sucht ReadyStackGo das Konto in dieser Reihenfolge:

1. Ein Konto, das schon mit diesem Provider und Subject verknüpft ist.
2. Sonst ein bestehender Benutzer oder eine offene Einladung mit derselben E-Mail-Adresse, aber nur, wenn der Anbieter die Adresse bestätigt hat (`email_verified`) oder **Trust unverified email addresses** eingeschaltet ist. Bei neuen Providern ist das aus; Provider, die es vor dieser Einstellung schon gab, behalten es an.
3. Alle anderen werden abgewiesen.

Ein Benutzer, der mit diesem Provider unter einer anderen Identität verknüpft ist, wird abgewiesen statt neu verknüpft. Deaktivierte Konten können sich nicht anmelden.

## Nicht aussperren

- Melden Sie sich nur über einen Provider an und haben kein lokales Passwort, lässt sich dieser Provider **weder ausschalten noch entfernen**, und neue Zugangsdaten (Reconnect, geänderte Einstellungen) gelten erst nach einer erfolgreichen Test-Anmeldung.
- **Unlink** im Profil ist gesperrt, solange Sie kein lokales Passwort haben.
- Solange kein Systemadministrator ein lokales Passwort hat, zeigen Settings › Single Sign-On und das Profil einen Hinweis. Setzen Sie ein Passwort unter **Profile → Set a local password**.

## Notzugang

Ist der Anbieter nicht erreichbar und kann sich niemand anmelden, setzen Sie vom Server aus ein lokales Passwort:

```bash
docker compose exec readystackgo rsgo admin set-password <username>
```

Der Befehl fragt das Passwort zweimal ab (oder liest mit `docker compose exec -T …` eine Zeile von der Standardeingabe). `--generate` erzeugt ein Passwort und zeigt es einmal an. Bei gestopptem Container geht `docker compose run --rm readystackgo admin set-password <username>`. Jede Nutzung wird protokolliert.

Exit-Codes: `0` erledigt, `1` Benutzer unbekannt, `2` Passwort erfüllt die Regeln nicht, `3` Datenbank nicht erreichbar, `64` falsche Argumente.

## Eigene Vorlagen

Provider entstehen aus Vorlagen. Betreiber und Distributionen können Vorlagen ergänzen oder ersetzen, ohne ReadyStackGo zu ändern, siehe [Identity-Provider-Vorlagen](/de/docs/system/identity-provider-templates/).

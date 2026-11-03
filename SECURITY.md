# Sicherheit

## Sicherheitslücken melden

Bitte melde Sicherheitslücken **nicht** als öffentliches Issue. Nutze stattdessen die private Meldefunktion von
GitHub in diesem Repository („Security“ → „Report a vulnerability“).

Hilfreich sind:

- betroffene Version bzw. Commit,
- eine Beschreibung der Lücke und ihrer Auswirkungen,
- Schritte zum Nachstellen – bitte mit Beispieldaten, nicht mit echten Rechnungen.

Wir melden uns, sobald wir die Meldung geprüft haben, und stimmen eine Veröffentlichung mit dir ab, wenn eine
Korrektur bereitsteht.

## Unterstützte Versionen

Sicherheitskorrekturen gibt es für den aktuellen Stand des Hauptzweigs (`main`) bzw. die jeweils neueste
Version.

## Was RechnungsWerk schützt

- Alle Seiten und Downloads mit Firmendaten, Kunden oder Rechnungen sind nur nach Anmeldung erreichbar.
- Anmeldung mit Passwortregeln (mindestens 12 Zeichen) und Sperre nach fünf Fehlversuchen für 15 Minuten.
  Nach einem Passwortwechsel enden andere Sitzungen spätestens nach wenigen Minuten.
- Die Ersteinrichtung ist nur mit einem einmaligen Code aus dem Datenordner möglich und danach gesperrt.
- Formulare sind gegen Cross-Site Request Forgery geschützt (Antiforgery-Token).
- Strenge Sicherheits-Header: Content-Security-Policy ohne Inline-Skripte, kein Einbetten in fremde Seiten,
  `X-Content-Type-Options: nosniff`, kein Referrer.
- Hochgeladene Dateien („Anzeigen“, „Prüfen“) werden nur im Arbeitsspeicher verarbeitet, sind auf 10 MB begrenzt
  und werden ohne DTD gelesen (kein XXE).
- Das SMTP-Passwort wird mit ASP.NET Core DataProtection verschlüsselt gespeichert, nie angezeigt und nicht
  protokolliert.
- Der Validator läuft in Docker Compose in einem internen Netz ohne Zugang nach außen; beide Container laufen ohne
  root-Rechte mit schreibgeschütztem Dateisystem.

## Was du als Betreiberin oder Betreiber tun solltest

- **HTTPS:** Betreibe RechnungsWerk außerhalb des eigenen Netzes nur hinter einem Reverse Proxy mit HTTPS und setze
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.
- **Datenordner schützen:** Die DataProtection-Schlüssel liegen unverschlüsselt im Datenordner neben der Datenbank.
  Wer den Ordner (oder eine Sicherung davon) lesen kann, kann auch das SMTP-Passwort entschlüsseln.
- **Aktuell bleiben:** Baue die Images regelmäßig neu, damit Sicherheitsupdates von .NET, Java und den Basis-Images
  ankommen.
- **Starke Passwörter** für alle Konten verwenden.

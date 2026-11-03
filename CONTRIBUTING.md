# Mitmachen bei RechnungsWerk

Danke, dass du RechnungsWerk besser machen willst! Fehlerberichte, Verbesserungsvorschläge und Pull Requests sind
willkommen.

## Fehler melden und Ideen vorschlagen

- Prüfe zuerst, ob es zum Thema schon ein Issue gibt.
- Beschreibe, was du erwartet hast, was stattdessen passiert ist und wie man es nachstellen kann.
- Hänge **keine echten Rechnungen** an. Sie enthalten Namen, Anschriften und Bankdaten. Nutze Beispieldaten oder
  schwärze die Datei vorher.
- Sicherheitslücken meldest du bitte nicht öffentlich, sondern wie in [SECURITY.md](SECURITY.md) beschrieben.

## Entwicklungsumgebung

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Java 17 oder neuer, wenn du die Validator-Tests lokal laufen lassen willst
- Docker, wenn du die Container bauen willst

```bash
dotnet tool restore
dotnet build
dotnet test
dotnet run --project src/RechnungsWerk.Web
```

Die Validator-Tests laufen nur, wenn `RECHNUNGSWERK_VALIDATOR_URL` auf einen laufenden KoSIT-Validator im
Daemon-Modus zeigt (Anleitung im [README](README.md#entwicklung)). Wenn du an der Erzeugung von XRechnung oder
ZUGFeRD arbeitest, lass sie bitte vor dem Pull Request laufen.

## Regeln für Code

- Fachlogik gehört nach `src/RechnungsWerk.Core`, die Oberfläche nach `src/RechnungsWerk.Web`. Seiten nutzen die
  Dienste des Core, statt Berechnungen zu wiederholen.
- Bezeichner im Code sind englisch, Kommentare deutsch. Kommentare erklären das *Warum*, nicht das *Was*.
- Der Bau behandelt Warnungen als Fehler – bitte nichts unterdrücken, sondern die Ursache beheben.
- Neue Funktionen und Fehlerbehebungen kommen mit Tests (xUnit, `tests/RechnungsWerk.Tests`).
- Datenbankänderungen nur als EF-Core-Migration:
  `dotnet dotnet-ef migrations add <Name> --project src/RechnungsWerk.Web --output-dir Data/Migrations`
- Dateien in UTF-8 ohne BOM mit LF-Zeilenenden (siehe `.editorconfig` und `.gitattributes`).
- Neue Abhängigkeiten nur mit einer Lizenz, die zur AGPL-3.0 passt, und mit Version in `Directory.Packages.props`.

## Regeln für die Oberfläche

- Deutsch, du-Form, echte Umlaute („Prüfen“, nicht „Pruefen“) – nur in Adressen (URLs) stehen Umschreibungen.
- Fehlermeldungen sagen, was passiert ist und was man tun kann.
- Jede Seite mit Firmendaten liegt in `Components/Pages/Business` und ist damit nur nach Anmeldung erreichbar; ein
  Test prüft das für alle Seiten.
- Keine Inline-Skripte und keine Inline-Stile – die Content-Security-Policy blockiert sie.
- Bedienbar mit Tastatur, beschriftete Formularfelder, gut lesbar auf dem Smartphone und im Ausdruck.

## Pull Requests

- Ein Thema pro Pull Request, mit kurzer Beschreibung, was sich ändert und warum.
- Commit-Nachrichten auf Englisch, im Imperativ (z. B. „Add credit note support“).
- `dotnet build` und `dotnet test` müssen grün sein; die CI prüft das zusätzlich mit den Containern.

## Lizenz

Mit deinem Beitrag erklärst du dich einverstanden, dass er unter der [AGPL-3.0](LICENSE) veröffentlicht wird.

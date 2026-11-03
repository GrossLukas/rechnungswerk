# RechnungsWerk

Freie Web-App für deutsche E-Rechnungen – gemacht für Kleinbetriebe, Vereine und Selbstständige.
RechnungsWerk läuft auf deinem eigenen Server: E-Rechnungen ansehen und drucken, mit dem offiziellen
KoSIT-Validator prüfen, eigene Rechnungen als XRechnung oder ZUGFeRD erstellen und per E-Mail verschicken.

*English summary below.*

## Was RechnungsWerk kann

- **Anzeigen & Drucken** – ohne Anmeldung: XRechnung (UBL oder CII, XML) oder ZUGFeRD/Factur-X (PDF mit
  eingebettetem XML) hochladen und als gut lesbare Rechnung sehen: Rechnungssteller und -empfänger, Leitweg-ID,
  Nummer und Datum, Leistungszeitraum, Positionen, Steuern je Satz, Summen, Zahlungsangaben mit IBAN und die Liste
  der Anhänge. Über den Browser lässt sich die Ansicht drucken oder als PDF speichern. Nichts wird gespeichert.
- **Offiziell prüfen** – ohne Anmeldung: Prüfung mit dem [KoSIT-Validator](https://github.com/itplr-kosit/validator)
  und der offiziellen [XRechnung-Konfiguration](https://github.com/itplr-kosit/validator-configuration-xrechnung).
  Du siehst, ob die Rechnung gültig ist, und jeden Fehler bzw. jede Warnung mit Regel-ID, Fundstelle und einer
  kurzen deutschen Erklärung.
- **Rechnungen schreiben** – mit Anmeldung: Firmenprofil (auch für Kleinunternehmer nach § 19 UStG), Kunden mit
  E-Mail und optionaler Leitweg-ID für Behörden, Artikel mit Einheit nach UN/ECE Rec. 20 und Umsatzsteuersatz
  (19 %, 7 % oder 0 %), fortlaufender Nummernkreis, Leistungsdatum oder -zeitraum, Fälligkeit, Steuerberechnung je
  Satz mit kaufmännischer Rundung. Download als **XRechnung 3.0 UBL** (Standard für Behörden), **XRechnung CII** oder
  **ZUGFeRD/Factur-X EN 16931** (PDF/A-3 mit eingebettetem XML).
- **Versand per E-Mail** – über dein eigenes Postfach (SMTP) mit Betreff- und Textvorlage, Platzhaltern
  (`{Rechnungsnummer}`, `{Kunde}`, `{Betrag}`, `{Fälligkeit}`, `{Firma}`), Testmail und Versandstatus je Rechnung.
  Versendete Rechnungen sind festgeschrieben und lassen sich nicht mehr ändern.

Die erzeugten Dateien werden in den automatischen Tests mit dem KoSIT-Validator geprüft (siehe [Entwicklung](#entwicklung)).

## Schnellstart mit Docker Compose

Voraussetzung: Docker mit Compose-Plugin.

```bash
git clone <Adresse dieses Repositorys> rechnungswerk
cd rechnungswerk
docker compose up -d --build
```

Danach läuft RechnungsWerk unter <http://localhost:8080>. Der Validator läuft als eigener Dienst im internen
Docker-Netz und ist von außen nicht erreichbar.

**Ersteinrichtung:** Beim ersten Start legt RechnungsWerk einen einmaligen Einrichtungscode im Datenordner ab.
So kann niemand, der eine frisch gestartete Instanz im Netz findet, das erste Konto anlegen. Den Code zeigst du an mit

```bash
docker compose exec web cat /data/setup-code.txt
```

Unter <http://localhost:8080/einrichtung> legst du mit diesem Code dein Konto und dein Firmenprofil an. Danach ist
die Einrichtung gesperrt und die Code-Datei gelöscht.

## Konfiguration

Die Einstellungen kommen aus Umgebungsvariablen (oder `appsettings.json`):

| Variable | Bedeutung | Standard |
| --- | --- | --- |
| `Data__Path` | Datenordner für SQLite-Datenbank, DataProtection-Schlüssel und Einrichtungscode | im Container `/data` |
| `Validator__Url` | Adresse des KoSIT-Validators im Daemon-Modus | in Compose `http://validator:8080/` |
| `Validator__TimeoutSeconds` | Wartezeit auf das Prüfergebnis | `60` |
| `SourceCode__Url` | Link „Quellcode“ in der Fußzeile (siehe [Lizenz](#lizenz)) | leer |
| `ASPNETCORE_HTTP_PORTS` | Port im Container | `8080` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` hinter einem Reverse Proxy, damit die App das Original-Protokoll kennt | aus |

### HTTPS

RechnungsWerk selbst spricht nur HTTP. Wenn die App nicht nur im eigenen Netz läuft, gehört ein Reverse Proxy mit
HTTPS davor (z. B. Caddy, Traefik oder nginx). Setze dann `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` und gib Port 8080
nicht direkt nach außen frei.

### E-Mail-Versand (SMTP)

Den SMTP-Zugang trägst du nach der Anmeldung unter **Einstellungen → E-Mail-Versand** ein. Das Passwort wird mit
ASP.NET Core DataProtection verschlüsselt gespeichert und nie wieder angezeigt. Mit „Testmail senden“ prüfst du die
Angaben, bevor die erste Rechnung rausgeht.

- **Gmail** verlangt für SMTP ein App-Passwort; dafür muss die Bestätigung in zwei Schritten aktiv sein.
- **Outlook.com und Microsoft 365** setzen je nach Konto OAuth voraus. RechnungsWerk unterstützt derzeit nur die
  Anmeldung mit Benutzername und Passwort (bzw. App-Passwort).
- **Microsoft 365:** „SMTP AUTH“ ist für Postfächer oft deaktiviert und muss von der Administration freigeschaltet
  werden.
- Übliche Ports: 587 mit STARTTLS oder 465 mit SSL/TLS. „Keine Verschlüsselung“ ist nur für Mailserver im eigenen
  Netz gedacht.

RechnungsWerk übersetzt typische Fehler (Anmeldung abgelehnt, falscher Port, Zertifikatsproblem …) in verständliche
Hinweise.

### Datensicherung

Alles Wichtige liegt im Datenordner (`/data`, in Compose das Volume `rechnungswerk-data`):

- `rechnungswerk.db` (mit `-wal`/`-shm`) – Firmenprofil, Kunden, Artikel, Rechnungen, Konten
- `keys/` – DataProtection-Schlüssel; ohne sie lässt sich das gespeicherte SMTP-Passwort nicht mehr entschlüsseln
  (du musst es dann neu eintragen) und alle müssen sich neu anmelden

Sichere den Ordner vollständig und am besten bei gestoppter Web-App, z. B.:

```bash
docker compose stop web
docker run --rm -v <projekt>_rechnungswerk-data:/data -v "$PWD":/backup busybox \
  tar czf /backup/rechnungswerk-daten.tgz -C /data .
docker compose start web
```

Den genauen Volume-Namen zeigt `docker volume ls`. Die Schlüssel liegen unverschlüsselt im Datenordner – schütze die
Sicherung entsprechend.

## Datenschutz

- Alles bleibt auf deinem Server: Datenbank, Schlüssel und erzeugte Rechnungen verlassen ihn nicht.
- Dateien unter „Anzeigen“ und „Prüfen“ werden nur im Arbeitsspeicher verarbeitet und nicht gespeichert.
- Die Prüfung läuft über den eigenen Validator-Dienst; dabei wird nichts an fremde Server geschickt.
- Keine Tracker, keine Analyse, keine Schriften oder Skripte von fremden Servern.
- E-Mails gehen ausschließlich über den SMTP-Server, den du einträgst.

## Rechtliches

- Seit dem 1. Januar 2025 müssen Unternehmen in Deutschland E-Rechnungen **empfangen** können.
- Die Pflicht zum **Ausstellen** kommt stufenweise: ab 2027 für Unternehmen mit mehr als 800.000 € Vorjahresumsatz,
  ab 2028 für alle. Ausnahmen gibt es unter anderem für Kleinbetragsrechnungen bis 250 € und für Kleinunternehmer.
- Rechnungen an Behörden laufen in der Regel als XRechnung mit Leitweg-ID.

RechnungsWerk ist ein Werkzeug und **kein Steuer- oder Rechtsrat**. Ob deine Rechnungen alle Pflichtangaben enthalten
und wie du sie aufbewahren musst, klärst du im Zweifel mit deiner Steuerberatung.

## Entwicklung

Voraussetzungen: [.NET 10 SDK](https://dotnet.microsoft.com/download), für die Validator-Tests zusätzlich Java 17
oder neuer.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/RechnungsWerk.Web
```

Die App läuft dann unter <http://localhost:5036>, die Daten landen in `src/RechnungsWerk.Web/App_Data/`.

Aufbau:

- `src/RechnungsWerk.Core` – Rechnungsmodell, Steuerberechnung, Lesen und Schreiben von XRechnung/ZUGFeRD,
  sichtbares PDF, Prüfbericht, SMTP-Versand (ohne Oberfläche)
- `src/RechnungsWerk.Web` – Blazor Web App (Interactive Server), EF Core mit SQLite, ASP.NET Core Identity
- `tests/RechnungsWerk.Tests` – xUnit-Tests

**Validator-Tests:** Lade `validator-1.6.3-standalone.jar` aus den
[Validator-Releases](https://github.com/itplr-kosit/validator/releases) und
`xrechnung-3.0.2-validator-configuration-2026-08-31.zip` aus den
[Konfigurations-Releases](https://github.com/itplr-kosit/validator-configuration-xrechnung/releases), entpacke die
Konfiguration und starte den Daemon:

```bash
java -jar validator-1.6.3-standalone.jar -s konfiguration/scenarios.xml -r konfiguration -D -G -H 127.0.0.1 -P 8081
RECHNUNGSWERK_VALIDATOR_URL=http://127.0.0.1:8081/ dotnet test
```

Ohne `RECHNUNGSWERK_VALIDATOR_URL` werden diese Tests übersprungen. In der CI laufen sie gegen das Validator-Image.

Datenbankänderungen kommen als EF-Core-Migration dazu (`dotnet tool restore`, dann
`dotnet dotnet-ef migrations add <Name> --project src/RechnungsWerk.Web --output-dir Data/Migrations`).
Beiträge sind willkommen – siehe [CONTRIBUTING.md](CONTRIBUTING.md). Sicherheitslücken bitte nach
[SECURITY.md](SECURITY.md) melden.

## Lizenz

RechnungsWerk steht unter der [GNU Affero General Public License v3.0](LICENSE). Wer RechnungsWerk verändert und
anderen über ein Netzwerk anbietet, muss ihnen den Quellcode der veränderten Fassung zugänglich machen – dafür gibt
es die Einstellung `SourceCode__Url`.

Verwendete Komponenten (jeweils unter eigener Lizenz):

- [KoSIT-Validator](https://github.com/itplr-kosit/validator) und
  [XRechnung-Konfiguration](https://github.com/itplr-kosit/validator-configuration-xrechnung) – Apache-2.0.
  Sie liegen nicht im Repository; das Validator-Image lädt sie beim Bau aus den offiziellen Releases und prüft die
  SHA-256-Prüfsummen.
- [ZUGFeRD-csharp](https://github.com/stephanstapel/ZUGFeRD-csharp) – Apache-2.0
- [PDFsharp-extended](https://github.com/KDS/PDFsharp/tree/pdfsharp-6-2-1-extended) (Abwandlung von PDFsharp, für PDF/A-3) und [MailKit](https://github.com/jstedfast/MailKit) – MIT
- Schrift Liberation Sans – SIL Open Font License (siehe `src/RechnungsWerk.Core/Pdf/Fonts/OFL.txt`)

---

## English summary

RechnungsWerk is a free, self-hosted web app for German e-invoices (XRechnung and ZUGFeRD/Factur-X), aimed at small
businesses, associations and freelancers. It can display and print incoming e-invoices, validate them with the
official KoSIT validator (running as a separate internal service), create invoices as XRechnung 3.0 UBL/CII or
ZUGFeRD EN 16931 PDF/A-3, and send them through your own SMTP server with a per-invoice dispatch status.

Quick start: `docker compose up -d --build`, open <http://localhost:8080>, read the one-time setup code with
`docker compose exec web cat /data/setup-code.txt` and create the first account at `/einrichtung`.
All data stays on your server. The user interface is German only. RechnungsWerk is not tax or legal advice.
License: AGPL-3.0; the KoSIT validator and its XRechnung configuration are Apache-2.0.

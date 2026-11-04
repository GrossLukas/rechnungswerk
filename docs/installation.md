# RechnungsWerk installieren

Diese Anleitung bringt RechnungsWerk auf deinen eigenen Rechner – unter Windows, macOS oder Linux. RechnungsWerk läuft
dabei in Docker, einem Programm, das Anwendungen in abgeschotteten Containern ausführt. Die Befehle kopierst du ins
Terminal; sie funktionieren in PowerShell unter Windows und im Terminal von macOS und Linux.

## Was du brauchst

- einen 64-Bit-Rechner mit Windows 10 (Version 22H2) oder Windows 11 (ab Version 23H2), einer der drei neuesten
  macOS-Versionen oder einem aktuellen Linux wie Ubuntu, Debian oder Fedora
- 8 GB Arbeitsspeicher – RechnungsWerk selbst belegt im Betrieb rund 1 GB
- rund 10 GB freien Speicherplatz für Docker und RechnungsWerk
- Internet beim ersten Start: Docker lädt dann rund 600 MB herunter. Danach braucht RechnungsWerk das Internet nur noch
  für den E-Mail-Versand und für Updates.
- Docker: unter Windows und macOS **Docker Desktop**, unter Linux **Docker Engine** mit dem Compose-Plugin

Docker Desktop ist für die private Nutzung kostenlos; größere Unternehmen und Behörden brauchen ein kostenpflichtiges
Abo. Maßgeblich sind die aktuellen [Lizenzbedingungen von Docker](https://docs.docker.com/subscription/desktop-license/).
Docker Engine unter Linux ist freie Software.

## Docker installieren

### Windows

1. Klicke mit der rechten Maustaste auf **Start** und wähle **Terminal (Administrator)** – unter Windows 10
   **Windows PowerShell (Administrator)**.
2. Richte das Windows-Subsystem für Linux (WSL) ein, auf dem Docker aufbaut, und starte den Rechner danach neu:

   ```
   wsl --install --no-distribution
   ```

3. Lade Docker Desktop von der [Download-Seite](https://docs.docker.com/desktop/setup/install/windows-install/)
   herunter: **x86_64** für die meisten Rechner, **Arm** für Rechner mit ARM-Prozessor (etwa Snapdragon; bei Docker
   noch eine Vorabversion).
4. Führe `Docker Desktop Installer.exe` aus und übernimm die Voreinstellungen.
5. Starte **Docker Desktop** über das Startmenü und akzeptiere die Nutzungsbedingungen. Ein Docker-Konto brauchst du
   nicht; die Anmeldung kannst du überspringen. Bereit ist Docker, sobald unten links **Engine running** steht.

### macOS

1. Lade Docker Desktop von der [Download-Seite](https://docs.docker.com/desktop/setup/install/mac-install/) herunter:
   **Apple silicon** für Macs mit Apple-Chip (M1 und neuer), **Intel chip** für ältere Macs. Welchen Chip dein Mac
   hat, zeigt das Apple-Menü unter **Über diesen Mac**.
2. Öffne `Docker.dmg` und ziehe **Docker** in den Ordner **Programme**.
3. Starte **Docker** aus dem Ordner **Programme** und akzeptiere die Nutzungsbedingungen. Ein Docker-Konto brauchst du
   nicht; die Anmeldung kannst du überspringen. Bereit ist Docker, sobald unten links **Engine running** steht.

### Linux

1. Installiere Docker Engine mit dem Compose-Plugin nach der [Anleitung von Docker](https://docs.docker.com/engine/install/)
   für deine Distribution. Nimm die Paketquellen von Docker, nicht das Paket `docker.io` deiner Distribution – dort
   fehlen oft das Compose-Plugin oder aktuelle Bauwerkzeuge.
2. Unter Debian und Ubuntu startet Docker danach von selbst, auch nach jedem Neustart. Bei anderen Distributionen
   schaltest du das ein mit:

   ```
   sudo systemctl enable --now docker
   ```

3. Setze vor jeden `docker`-Befehl dieser Anleitung `sudo`, zum Beispiel `sudo docker compose up -d --build` – es sei
   denn, du hast Docker [für deinen Benutzer eingerichtet](https://docs.docker.com/engine/install/linux-postinstall/).

## RechnungsWerk herunterladen

**Ohne Git:**

1. Lade [rechnungswerk-main.zip](https://github.com/GrossLukas/rechnungswerk/archive/refs/heads/main.zip) herunter.
2. Entpacke die Datei an einen Ort deiner Wahl, zum Beispiel in deinen Benutzerordner. Windows: Rechtsklick →
   **Alle extrahieren**, macOS: Doppelklick.
3. Du brauchst den Ordner, in dem die Datei `docker-compose.yml` liegt – unter Windows ist das oft
   `rechnungswerk-main\rechnungswerk-main`.

**Mit Git** – damit gehen spätere Updates etwas einfacher:

```
git clone https://github.com/GrossLukas/rechnungswerk.git
cd rechnungswerk
```

**Terminal im Ordner öffnen:**

- **Windows 11:** Öffne den Ordner im Explorer, klicke mit der rechten Maustaste auf eine freie Stelle und wähle
  **In Terminal öffnen**. **Windows 10:** Halte die Umschalttaste gedrückt, klicke mit der rechten Maustaste und wähle
  **PowerShell-Fenster hier öffnen**.
- **macOS:** Öffne **Programme → Dienstprogramme → Terminal**, tippe `cd ` (mit Leerzeichen), ziehe den Ordner aus dem
  Finder ins Terminal-Fenster und drücke die Eingabetaste.
- **Linux:** Klicke im Ordner mit der rechten Maustaste und wähle **Im Terminal öffnen**.

## Starten und einrichten

1. Starte RechnungsWerk:

   ```
   docker compose up -d --build
   ```

   Beim ersten Mal lädt Docker rund 600 MB herunter und baut RechnungsWerk. Das dauert je nach Internetverbindung und
   Rechner etwa 5 bis 15 Minuten. Fertig ist es, wenn die Eingabezeile wieder erscheint. Fragt macOS, ob Docker auf
   den Ordner zugreifen darf, erlaube es.
2. Lass dir den einmaligen Einrichtungscode anzeigen:

   ```
   docker compose exec web cat /data/setup-code.txt
   ```

   Der Code besteht aus vier Gruppen mit je fünf Zeichen, etwa `7KQ2M-XW4PA-H9D3R-T6VNB`. Kommt eine Fehlermeldung,
   warte ein paar Sekunden und versuche es noch einmal.
3. Öffne <http://localhost:8080> im Browser. Beim ersten Mal erscheint die Seite **Einrichten**.
4. Trage den Code, deine E-Mail-Adresse und ein Passwort ein – mindestens 12 Zeichen, darunter ein Kleinbuchstabe und
   eine Ziffer. Darunter folgen die Angaben, die auf deinen Rechnungen stehen: Name, Anschrift, Steuernummer oder
   USt-IdNr., Ansprechperson, Telefon und E-Mail. Die Bankverbindung kannst du auch später ergänzen.
5. Klicke auf **Einrichten** und melde dich danach mit E-Mail-Adresse und Passwort an.

Bewahre das Passwort gut auf, etwa in einem Passwort-Manager – eine Funktion „Passwort vergessen“ gibt es nicht. Die
Angaben zur Firma änderst du später unter **Einstellungen → Firma**, das Passwort unter **Einstellungen → Konto**.

Künftig öffnest du einfach <http://localhost:8080> und meldest dich über **Anmelden** an. **Anzeigen** und **Prüfen**
funktionieren auch ohne Anmeldung. Klappt etwas nicht, hilft der Abschnitt [Häufige Probleme](#häufige-probleme).

## Im Alltag

Gib die Befehle immer im RechnungsWerk-Ordner ein.

- Beenden: `docker compose stop`
- Wieder starten: `docker compose up -d`

Solange du es nicht beendest, läuft RechnungsWerk, sobald Docker läuft – auch nach einem Neustart des Rechners. Unter
Windows und macOS muss dafür Docker Desktop gestartet sein. Soll Docker Desktop von selbst starten, schalte dort unter
**Settings → General** die Option **Start Docker Desktop when you sign in to your computer** ein. Unter **Containers**
findest du RechnungsWerk in Docker Desktop als **rechnungswerk** und kannst es auch per Mausklick stoppen und starten.

## Aktualisieren

Deine Daten bleiben beim Aktualisieren erhalten – sie liegen nicht im RechnungsWerk-Ordner, sondern in Docker. Eine
[Datensicherung](#datensicherung) vorher schadet trotzdem nicht.

**Mit Git** im RechnungsWerk-Ordner:

```
git pull
docker compose build --pull
docker compose up -d
```

Meldet `git pull`, dass lokale Änderungen überschrieben würden (etwa weil du den Port geändert hast), gib
`git stash`, dann `git pull` und danach `git stash pop` ein.

**Mit ZIP-Datei:**

1. Beende die bisherige Version in ihrem Ordner: `docker compose down`
2. Lade die ZIP-Datei neu herunter und entpacke sie wie unter
   [RechnungsWerk herunterladen](#rechnungswerk-herunterladen) – in einen neuen Ordner, nicht über die alte Version.
   Wie der neue Ordner heißt, spielt keine Rolle.
3. Hast du in der alten `docker-compose.yml` etwas geändert, etwa den Port, ändere es in der neuen Datei genauso.
4. Öffne das Terminal im neuen Ordner und gib ein:

   ```
   docker compose build --pull
   docker compose up -d
   ```

5. Den alten Ordner kannst du danach löschen.

`docker compose build --pull` holt auch Sicherheitsupdates für .NET, Java und das Basissystem der Container. Führe
`docker compose build --pull` und `docker compose up -d` deshalb ab und zu aus, auch wenn es keine neue Version von
RechnungsWerk gibt.

## Datensicherung

Alle Daten – Firmenprofil, Kunden, Artikel, Rechnungen, dein Konto und die Schlüssel für das gespeicherte
SMTP-Passwort – liegen im Docker-Volume `rechnungswerk_rechnungswerk-data`. So sicherst du sie in die Datei
`rechnungswerk-daten.tgz` im RechnungsWerk-Ordner:

```
docker compose stop web
docker run --rm -v rechnungswerk_rechnungswerk-data:/data -v "${PWD}:/backup" busybox sh -c "test -f /data/rechnungswerk.db && tar czf /backup/rechnungswerk-daten.tgz -C /data . && echo Sicherung erstellt"
docker compose start web
```

Erscheint „Sicherung erstellt“, ist die Datei fertig. Kopiere sie an einen sicheren Ort, etwa auf einen USB-Stick –
sonst löschst du sie mit, wenn du beim Aktualisieren den alten Ordner entfernst. Eine neue Sicherung überschreibt die
Datei. Sie ist nicht verschlüsselt; schütze sie wie deine Rechnungen. Mehr dazu im [README](../README.md#datensicherung).

**Sicherung zurückspielen** – ersetzt alle aktuellen Daten. Lege `rechnungswerk-daten.tgz` in den
RechnungsWerk-Ordner und gib dort ein:

```
docker compose stop web
docker run --rm -v rechnungswerk_rechnungswerk-data:/data -v "${PWD}:/backup" busybox sh -c "tar tzf /backup/rechnungswerk-daten.tgz | grep -q rechnungswerk.db && rm -rf /data/* && tar xzf /backup/rechnungswerk-daten.tgz -C /data && chown -R 1654:1654 /data && echo Sicherung eingespielt"
docker compose start web
```

Erscheint „Sicherung eingespielt“, ist alles zurück. Auf einem neuen Rechner startest du RechnungsWerk vorher einmal
mit `docker compose up -d --build`; die Einrichtung im Browser brauchst du dann nicht.

## Entfernen

1. Mach eine [Datensicherung](#datensicherung), falls du die Daten noch brauchst.
2. Gib im RechnungsWerk-Ordner ein:

   ```
   docker compose down --volumes --rmi all
   ```

   **Achtung:** Das löscht alle Daten von RechnungsWerk unwiderruflich.
3. Lösche den RechnungsWerk-Ordner.
4. Brauchst du Docker nicht mehr, deinstalliere es. Sonst gibt `docker builder prune --all` den Speicher frei, den das
   Bauen belegt hat; das betrifft alle Projekte in Docker.

## Häufige Probleme

**„docker“ wird nicht erkannt oder Docker läuft nicht** („command not found“, „Cannot connect to the Docker
daemon“, „error during connect“): Starte Docker Desktop und warte, bis es bereit ist. Hast du Docker gerade erst
installiert, öffne ein neues Terminal-Fenster. Unter Linux setze `sudo` davor; läuft der Dienst nicht, starte ihn mit
`sudo systemctl start docker`.

**„no configuration file provided“:** Das Terminal ist im falschen Ordner. Öffne es in dem Ordner, in dem
`docker-compose.yml` liegt.

**Docker Desktop startet unter Windows nicht:** Meist ist die Virtualisierung ausgeschaltet. Im Task-Manager
(Strg+Umschalt+Esc) muss unter **Leistung → CPU** „Virtualisierung: Aktiviert“ stehen. Wenn nicht, schalte sie im
BIOS/UEFI ein – wie das geht, beschreibt
[Microsoft](https://support.microsoft.com/de-de/windows/virtualisierung-unter-windows-aktivieren-c5578302-6e43-4b4b-a449-8ced115f58e1).
Meldet Docker Desktop ein Problem mit WSL, gib in PowerShell als Administrator `wsl --update` ein und starte den
Rechner neu.

**Port 8080 ist belegt** („port is already allocated“ oder „address already in use“): Ein anderes Programm nutzt
Port 8080. Öffne `docker-compose.yml` mit einem Texteditor (Windows: Rechtsklick → **Öffnen mit → Editor**, macOS:
**Öffnen mit → TextEdit**) und ändere in der Zeile `- "8080:8080"` die erste Zahl, etwa in `- "8090:8080"`.
Speichere, gib `docker compose up -d` ein und öffne <http://localhost:8090>.

**Der erste Start dauert lange oder bricht ab:** Beim ersten Start und nach Updates baut Docker RechnungsWerk; das
dauert einige Minuten. Bricht es wegen der Internetverbindung ab, gib den Befehl noch einmal ein – schon Geladenes wird
wiederverwendet.

**Zu wenig Speicherplatz** („no space left on device“): Nach Updates bleiben alte Versionen liegen. Diese Befehle
räumen sie weg, bestätige jeweils mit `y`:

```
docker image prune
docker builder prune --all
```

**Zu wenig Arbeitsspeicher** (der Rechner wird zäh oder der Bau bricht mit „killed“ bzw. Code 137 ab): RechnungsWerk
braucht im Betrieb rund 1 GB, beim Bauen kurzzeitig mehr. Schließe andere Programme und versuche es erneut; beende
RechnungsWerk mit `docker compose stop`, wenn du es nicht brauchst. Auf dem Mac legst du in Docker Desktop unter
**Settings → Resources** fest, wie viel Arbeitsspeicher Docker nutzen darf – lass dort mindestens 4 GB.

**<http://localhost:8080> lädt nicht:** Gleich nach dem Start braucht RechnungsWerk einige Sekunden. `docker compose ps`
zeigt, ob `web` und `validator` laufen; `docker compose logs web` zeigt, woran es hakt.

**Nach dem Aktualisieren ist RechnungsWerk leer:** Deine Daten sind nicht verloren. Frühere Versionen haben das
Daten-Volume nach dem Ordner benannt, in dem RechnungsWerk lag; heute heißt es immer `rechnungswerk_rechnungswerk-data`.
`docker volume ls` zeigt das alte Volume, etwa `rechnungswerk-main_rechnungswerk-data`. So übernimmst du die Daten –
setze den alten Namen ein; was du seit dem Update eingegeben hast, wird ersetzt:

```
docker compose stop web
docker run --rm -v rechnungswerk-main_rechnungswerk-data:/alt -v rechnungswerk_rechnungswerk-data:/data busybox sh -c "test -f /alt/rechnungswerk.db && rm -rf /data/* && cp -a /alt/. /data/ && echo Daten kopiert"
docker compose start web
```

Erscheint „Daten kopiert“, ist alles wieder da.

## Andere Geräte und E-Mail

**Andere Geräte:** Docker gibt Port 8080 für alle Netzwerkverbindungen deines Rechners frei. Andere Geräte in deinem
Netz erreichen RechnungsWerk dann unter Umständen über `http://<IP-Adresse deines Rechners>:8080` – unverschlüsselt,
samt Passwort. Willst du RechnungsWerk von anderen Geräten aus nutzen, gehört ein Reverse Proxy mit HTTPS davor, siehe
[README → HTTPS](../README.md#https). Nutzt du es nur an diesem Rechner, beschränke den Zugang darauf: Ändere in
`docker-compose.yml` die Zeile `- "8080:8080"` in `- "127.0.0.1:8080:8080"` und gib `docker compose up -d` ein.

**E-Mail-Versand:** Den Zugang zu deinem Postfach (SMTP) trägst du nach der Anmeldung unter **Einstellungen →
Versand** ein. Hinweise zu Gmail, Outlook und den Ports stehen im
[README → E-Mail-Versand](../README.md#e-mail-versand-smtp).

# RechnungsWerk installieren

Mit dieser Anleitung läuft RechnungsWerk auf deinem eigenen Rechner – unter Windows, macOS oder Linux. Technikwissen
brauchst du nicht: Du kopierst ein paar Befehle, fügst sie ein und drückst Enter. Rechne beim ersten Mal mit etwa
30 Minuten.

## Das brauchst du

- einen Rechner mit Windows 10 (ab Version 22H2) oder Windows 11 (ab Version 23H2, Home reicht), einem aktuellen
  macOS (eine der drei neuesten Versionen) oder einem aktuellen Linux wie Ubuntu, Debian oder Fedora
- 8 GB Arbeitsspeicher und rund 10 GB freien Speicherplatz
- Internet: Beim ersten Start werden rund 600 MB geladen. Danach braucht RechnungsWerk das Internet nur noch für den
  E-Mail-Versand und für Updates.
- **Docker** – ein kostenloses Programm, in dem RechnungsWerk läuft. Wie du es installierst, steht in Schritt 1.

Docker Desktop ist kostenlos für Privatleute, für Bildung und für kleine Firmen mit weniger als 250 Beschäftigten und
weniger als 10 Mio. US-Dollar Jahresumsatz. Größere Firmen und Behörden brauchen ein Abo. Es gelten die
[Lizenzbedingungen von Docker](https://docs.docker.com/subscription/desktop-license/). Unter Linux ist Docker Engine
freie Software.

## So gibst du Befehle ein

Befehle gibst du im **Terminal** ein – einem Fenster, in das man Befehle tippt. Tippen musst du aber nichts:

1. Kopiere den Befehl mit dem Symbol rechts oben am grauen Kasten.
2. Füge ihn im Terminal ein: Windows **Strg+V**, Mac **⌘V**, Linux **Strg+Umschalt+V**. Fragt das Terminal bei
   mehreren Zeilen nach, bestätige es.
3. Drücke **Enter**.

Alle Befehle funktionieren in PowerShell unter Windows und im Terminal von macOS und Linux.

## Schritt 1: Docker installieren

### Windows

1. Klicke mit der rechten Maustaste auf **Start** und wähle **Terminal (Administrator)**. Unter Windows 10 heißt es
   **Windows PowerShell (Administrator)**.
2. Gib diesen Befehl ein. Er richtet WSL ein, das Docker unter Windows braucht:

   ```
   wsl --install --no-distribution
   ```

   Starte den Rechner danach neu. Kommt statt der Installation nur ein langer Hilfetext, gib zuerst `wsl --update` ein
   und dann noch einmal den Befehl oben.
3. Lade Docker Desktop von der [Download-Seite](https://docs.docker.com/desktop/setup/install/windows-install/)
   herunter. Für fast alle Rechner ist **x86_64** richtig. **Arm** brauchst du nur bei einem ARM-Prozessor wie
   Snapdragon (bei Docker noch eine Vorabversion). Was du hast, steht unter **Einstellungen → System → Info** bei
   **Systemtyp**.
4. Öffne im Ordner **Downloads** die Datei `Docker Desktop Installer.exe` und klicke dich mit den Voreinstellungen
   durch.
5. Starte **Docker Desktop** über das Startmenü und klicke auf **Accept**. Ein Docker-Konto brauchst du nicht –
   Anmeldung und Fragebogen kannst du überspringen. Docker ist bereit, wenn unten links **Engine running** steht.
6. Damit Docker nach jedem Neustart von selbst startet: Klicke oben rechts auf das Zahnrad (**Settings**), schalte
   unter **General** die Option **Start Docker Desktop when you sign in to your computer** ein und klicke auf
   **Apply**.

### macOS

1. Lade Docker Desktop von der [Download-Seite](https://docs.docker.com/desktop/setup/install/mac-install/) herunter:
   **Apple silicon** für Macs mit Apple-Chip (M1 und neuer), **Intel chip** für ältere Macs. Welchen Chip du hast,
   siehst du im Apple-Menü unter **Über diesen Mac**.
2. Öffne `Docker.dmg` und ziehe **Docker** in den Ordner **Programme**.
3. Starte **Docker** aus dem Ordner **Programme** und klicke auf **Accept**. Ein Docker-Konto brauchst du nicht –
   Anmeldung und Fragebogen kannst du überspringen. Docker ist bereit, wenn unten links **Engine running** steht.
4. Damit Docker nach jedem Neustart von selbst startet: Klicke oben rechts auf das Zahnrad (**Settings**), schalte
   unter **General** die Option **Start Docker Desktop when you sign in to your computer** ein und klicke auf
   **Apply**.

### Linux

1. Installiere Docker Engine mit dem Compose-Plugin nach der [Anleitung von Docker](https://docs.docker.com/engine/install/)
   für deine Distribution. Nimm die Paketquellen von Docker, nicht das Paket `docker.io` – dort fehlt oft das
   Compose-Plugin.
2. Unter Debian und Ubuntu startet Docker danach von selbst, auch nach jedem Neustart. Bei anderen Distributionen
   schaltest du das so ein:

   ```
   sudo systemctl enable --now docker
   ```

3. Setze vor jeden `docker`-Befehl dieser Anleitung `sudo`, also zum Beispiel `sudo docker compose up -d --build`. Das
   brauchst du nicht, wenn du Docker [für deinen Benutzer eingerichtet](https://docs.docker.com/engine/install/linux-postinstall/)
   hast.

## Schritt 2: RechnungsWerk herunterladen

1. Lade [rechnungswerk-main.zip](https://github.com/GrossLukas/rechnungswerk/archive/refs/heads/main.zip) herunter.
2. Entpacke die Datei, zum Beispiel in deinen Benutzerordner. Windows: Rechtsklick → **Alle extrahieren**. Mac:
   Doppelklick – meistens hat Safari das schon für dich erledigt.
3. Merk dir den Ordner, in dem die Datei `docker-compose.yml` liegt. Unter Windows ist das oft
   `rechnungswerk-main\rechnungswerk-main`.

Du nutzt schon Git? Dann geht es auch so:

```
git clone https://github.com/GrossLukas/rechnungswerk.git
cd rechnungswerk
```

**Terminal in diesem Ordner öffnen:**

- **Windows 11:** Ordner im Explorer öffnen, mit der rechten Maustaste auf eine freie Stelle klicken, **In Terminal
  öffnen** wählen.
- **Windows 10:** Umschalttaste gedrückt halten, rechte Maustaste, **PowerShell-Fenster hier öffnen** wählen.
- **Mac:** **Programme → Dienstprogramme → Terminal** öffnen, `cd ` tippen (mit Leerzeichen), den Ordner aus dem Finder
  ins Terminal ziehen und Enter drücken.
- **Linux:** Im Ordner mit der rechten Maustaste klicken, **Im Terminal öffnen** wählen.

## Schritt 3: RechnungsWerk starten

```
docker compose up -d --build
```

Beim ersten Mal lädt Docker rund 600 MB und baut RechnungsWerk. Das dauert 5 bis 15 Minuten. Fertig ist es, wenn am
Ende bei `rechnungswerk-web-1` **Started** steht. Fragt der Mac, ob Terminal oder Docker auf einen Ordner zugreifen
darf, erlaube es.

Öffne jetzt <http://localhost:8080> im Browser. Lädt die Seite nicht gleich, warte ein paar Sekunden und lade neu. Du
siehst die Seite **Einrichten**.

Du willst E-Rechnungen nur ansehen oder prüfen? Dafür brauchst du kein Konto – klick einfach auf **Anzeigen** oder
**Prüfen**.

## Schritt 4: Konto anlegen

1. Hol dir den einmaligen Einrichtungscode:

   ```
   docker compose exec web cat /data/setup-code.txt
   ```

   Er sieht ungefähr so aus: `7KQ2M-XW4PA-H9D3R-T6VNB`.
2. Trag auf der Seite **Einrichten** den Code, deine E-Mail-Adresse und ein Passwort ein. Das Passwort braucht
   mindestens 12 Zeichen, davon mindestens 5 verschiedene, darunter einen Kleinbuchstaben und eine Ziffer.
3. Darunter kommen die Angaben, die auf deinen Rechnungen stehen: Name, Anschrift, Steuernummer oder USt-IdNr.,
   Ansprechperson, Telefon und E-Mail. Die Bankverbindung kannst du auch später eintragen.
4. Klick auf **Einrichten** und melde dich mit E-Mail-Adresse und Passwort an.

Das war's. Ab jetzt öffnest du einfach <http://localhost:8080> und meldest dich an.

**Wichtig:** Heb dein Passwort gut auf, am besten in einem Passwort-Manager. Eine Funktion „Passwort vergessen“ gibt es
nicht – ohne Passwort kommst du nicht mehr an deine Rechnungen. Firmenangaben änderst du später unter **Einstellungen →
Firma**, das Passwort unter **Einstellungen → Konto**.

Klappt etwas nicht? Schau unter [Häufige Probleme](#häufige-probleme).

## Im Alltag

RechnungsWerk läuft im Hintergrund, solange Docker läuft – auch nach einem Neustart. Das Terminal kannst du zumachen.

Beenden und starten geht am einfachsten in Docker Desktop: unter **Containers** bei **rechnungswerk**. Oder im
Terminal im RechnungsWerk-Ordner:

- Beenden: `docker compose stop`
- Wieder starten: `docker compose up -d`

## Aktualisieren

Deine Daten bleiben beim Aktualisieren erhalten. Sie liegen nicht im RechnungsWerk-Ordner, sondern in Docker. Eine
[Datensicherung](#datensicherung) vorher schadet trotzdem nie.

**Mit ZIP-Datei:**

1. Beende die alte Version. Terminal im alten Ordner öffnen und eingeben: `docker compose down`
2. Lade die ZIP-Datei neu herunter und entpacke sie wie in [Schritt 2](#schritt-2-rechnungswerk-herunterladen) – in
   einen **neuen** Ordner, nicht über die alte Version. Wie der Ordner heißt, ist egal.
3. Hast du in der alten `docker-compose.yml` etwas geändert, zum Beispiel den Port, ändere es in der neuen genauso.
4. Öffne das Terminal im neuen Ordner und gib ein:

   ```
   docker compose build --pull
   docker compose up -d
   ```

5. Den alten Ordner kannst du jetzt löschen. Liegt darin eine Sicherung, kopiere sie vorher raus.

**Mit Git** im RechnungsWerk-Ordner:

```
docker compose down
git pull
docker compose build --pull
docker compose up -d
```

Meckert `git pull`, weil du etwas geändert hast (zum Beispiel den Port), gib `git stash`, `git pull` und
`git stash pop` ein und danach noch einmal die beiden letzten Befehle.

**Tipp:** `docker compose build --pull` holt auch Sicherheitsupdates für die Bausteine, auf denen RechnungsWerk läuft.
Führ `docker compose build --pull` und `docker compose up -d` deshalb ab und zu aus, auch ohne neue Version.

## Datensicherung

Alle Daten – Firmenprofil, Kunden, Artikel, Rechnungen, dein Konto und die Schlüssel für das gespeicherte
SMTP-Passwort – liegen in Docker im Speicher `rechnungswerk_rechnungswerk-data`.

**Achtung:** Time Machine und die Windows-Sicherung sichern diese Daten nicht mit. Und wer Docker Desktop deinstalliert
oder zurücksetzt, löscht sie. Mach deshalb regelmäßig eine Sicherung, etwa einmal im Monat.

**Sichern** – legt die Datei `rechnungswerk-daten.tgz` im RechnungsWerk-Ordner an:

```
docker compose stop web
docker run --rm -v rechnungswerk_rechnungswerk-data:/data -v "${PWD}:/backup" busybox sh -c "test -f /data/rechnungswerk.db && tar czf /backup/rechnungswerk-daten.tgz -C /data . && echo Sicherung erstellt"
docker compose start web
```

Steht da „Sicherung erstellt“, ist die Datei fertig. Kopiere sie an einen sicheren Ort außerhalb des Rechners, zum
Beispiel auf einen USB-Stick. Eine neue Sicherung überschreibt die alte Datei. Die Datei ist nicht verschlüsselt – pass
darauf auf wie auf deine Rechnungen.

**Zurückspielen** – ersetzt alle aktuellen Daten. Leg `rechnungswerk-daten.tgz` in den RechnungsWerk-Ordner und gib
dort ein:

```
docker compose stop web
docker run --rm -v rechnungswerk_rechnungswerk-data:/data -v "${PWD}:/backup" busybox sh -c "mkdir /neu && tar xzf /backup/rechnungswerk-daten.tgz -C /neu && test -f /neu/rechnungswerk.db && rm -rf /data/* && cp -a /neu/. /data/ && chown -R 1654:1654 /data && echo Sicherung eingespielt"
docker compose start web
```

Steht da „Sicherung eingespielt“, ist alles wieder da. Fehlt die Meldung, ist die Datei kaputt oder unvollständig –
deine bisherigen Daten bleiben dann, wie sie waren.

Umzug auf einen neuen Rechner: Dort zuerst Schritt 1 bis 3 machen, dann die Sicherung zurückspielen. Die Einrichtung im
Browser brauchst du dann nicht.

## Entfernen

1. Brauchst du die Daten noch? Dann mach erst eine [Datensicherung](#datensicherung) und kopiere die Datei aus dem
   RechnungsWerk-Ordner raus.
2. Gib im RechnungsWerk-Ordner ein:

   ```
   docker compose down --volumes --rmi all
   ```

   **Achtung:** Das löscht alle Daten von RechnungsWerk für immer.
3. Lösche den RechnungsWerk-Ordner.
4. Brauchst du Docker nicht mehr, deinstalliere es. Sonst gibt `docker builder prune --all` den Platz frei, den das
   Bauen belegt hat – das gilt für alle Projekte in Docker.

## Häufige Probleme

**„docker“ wird nicht erkannt oder Docker läuft nicht** („command not found“, „Cannot connect to the Docker daemon“,
„error during connect“, „permission denied … docker.sock“): Starte Docker Desktop und warte, bis **Engine running**
dasteht. Hast du Docker gerade erst installiert, öffne ein neues Terminal-Fenster. Unter Linux setz `sudo` davor; läuft
der Dienst nicht, starte ihn mit `sudo systemctl start docker`.

**„no configuration file provided“:** Das Terminal ist im falschen Ordner. Öffne es in dem Ordner, in dem
`docker-compose.yml` liegt.

**Docker Desktop startet unter Windows nicht:** Meistens ist die Virtualisierung aus. Öffne den Task-Manager
(Strg+Umschalt+Esc): Unter **Leistung → CPU** muss „Virtualisierung: Aktiviert“ stehen. Wenn nicht, schalte sie im
BIOS/UEFI ein – wie das geht, erklärt
[Microsoft](https://support.microsoft.com/de-de/windows/virtualisierung-unter-windows-aktivieren-c5578302-6e43-4b4b-a449-8ced115f58e1).
Meldet Docker ein Problem mit WSL, gib im Terminal (Administrator) `wsl --update` ein und starte den Rechner neu.
Deinstalliere Docker Desktop nicht und setz es nicht zurück – das löscht alle Daten von RechnungsWerk.

**Port 8080 ist belegt** („port is already allocated“, „address already in use“ oder „Ports are not available“): Ein
anderes Programm nutzt schon Port 8080. Öffne `docker-compose.yml` mit einem Texteditor (Windows: Rechtsklick →
**Öffnen mit → Editor**, Mac: **Öffnen mit → TextEdit**). Ändere in der Zeile `- "127.0.0.1:8080:8080"` die erste
`8080` in `8090`, speichere, gib `docker compose up -d` ein und öffne <http://localhost:8090>.

**Der erste Start dauert lange oder bricht ab:** Beim ersten Start und nach Updates baut Docker RechnungsWerk, das
dauert ein paar Minuten. Bricht es wegen des Internets ab, gib den Befehl einfach noch einmal ein – was schon geladen
ist, wird weiterverwendet.

**Zu wenig Speicherplatz** („no space left on device“): Nach Updates bleiben alte Versionen liegen. Diese Befehle
räumen auf, bestätige jeweils mit `y`:

```
docker image prune
docker builder prune --all
```

**Zu wenig Arbeitsspeicher** (der Rechner wird langsam oder das Bauen bricht mit „killed“ oder Code 137 ab):
RechnungsWerk braucht im Betrieb rund 1 GB, beim Bauen kurz mehr. Schließ andere Programme und versuch es noch einmal.
Brauchst du RechnungsWerk gerade nicht, beende es mit `docker compose stop`. Am Mac legst du in Docker Desktop unter
**Settings → Resources** fest, wie viel Arbeitsspeicher Docker nutzen darf – lass dort mindestens 4 GB.

**<http://localhost:8080> lädt nicht:** Läuft Docker Desktop? Starte es sonst und warte eine Minute. Direkt nach dem
Start braucht auch RechnungsWerk ein paar Sekunden. `docker compose ps` zeigt, ob `web` und `validator` laufen;
`docker compose logs web` zeigt, woran es hakt.

**Nach dem Aktualisieren ist RechnungsWerk leer:** Keine Sorge, deine Daten sind noch da. Frühere Versionen haben den
Daten-Speicher nach dem Ordner benannt, in dem RechnungsWerk lag; heute heißt er immer `rechnungswerk_rechnungswerk-data`.
`docker volume ls` zeigt den alten Namen, etwa `rechnungswerk-main_rechnungswerk-data`. So holst du die Daten zurück –
setz dabei den alten Namen ein. Was du seit dem Update eingegeben hast, wird ersetzt:

```
docker compose stop web
docker run --rm -v rechnungswerk-main_rechnungswerk-data:/alt -v rechnungswerk_rechnungswerk-data:/data busybox sh -c "test -f /alt/rechnungswerk.db && rm -rf /data/* && cp -a /alt/. /data/ && echo Daten kopiert"
docker compose start web
```

Steht da „Daten kopiert“, ist alles wieder da.

## Andere Geräte und E-Mail

**Andere Geräte:** RechnungsWerk ist nur auf diesem Rechner erreichbar. Für Handy, Tablet oder andere Rechner braucht
es einen Webserver mit HTTPS davor, damit Passwort und Rechnungen verschlüsselt übertragen werden. Das ist etwas für
Fortgeschrittene, siehe [README → HTTPS](../README.md#https).

**E-Mail-Versand:** Den Zugang zu deinem Postfach (SMTP) trägst du nach der Anmeldung unter **Einstellungen → Versand**
ein. Tipps zu Gmail, Outlook und den Ports stehen im [README → E-Mail-Versand](../README.md#e-mail-versand-smtp).

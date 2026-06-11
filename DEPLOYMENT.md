# Deployment — NOOSE-Website

Diese Anleitung beschreibt, wie die NOOSE-Website auf den Produktiv-Server ausgerollt wird,
wie der Server aufgebaut ist und wie man typische Probleme löst.

---

## 1. Überblick / Architektur

```
Browser ──HTTPS──> nginx (Port 443, TLS via Let's Encrypt)
                     │  Reverse-Proxy, leitet WebSockets (Blazor/SignalR) durch
                     ▼
                   Kestrel (127.0.0.1:5000)  ← systemd-Dienst "noose", User www-data
                     │
                     ▼
                   MariaDB (127.0.0.1:3306, DB "noose")  ← lokal auf demselben Server
```

| Was | Wert |
|-----|------|
| Server (SSH) | `root@195.20.225.12` (Ubuntu 24.04) |
| Domain | `noose.info` (+ `www`) → A-Record auf die Server-IP |
| App-Verzeichnis | `/var/www/noose` |
| systemd-Dienst | `noose` |
| Secrets/Env | `/etc/noose/noose.env` (chmod 600, nur root) |
| Zeitzone | `Europe/Berlin` (via `TZ` in `/etc/noose/noose.env`) — **zwingend**, sonst alle Zeiten 2 h zu früh |
| Datenbank | lokale **MariaDB**, DB `noose`, User `noose@localhost` / `noose@127.0.0.1` |
| nginx-Site | `/etc/nginx/sites-available/noose` |
| TLS | Let's Encrypt (certbot, erneuert sich automatisch) |
| Uploads/Schlüssel | `/var/www/noose/App_Data` (**bei Updates niemals löschen!**) |

Wichtige App-Mechanik (siehe `Program.cs` / `Data/DatabaseConnectionResolver.cs`):
- **Verbindungs-Auswahl:** Erst `ConnectionStrings:ProductionConnection`, sonst Fallback auf
  `DefaultConnection`. Auf dem Server zeigt `ProductionConnection` auf die lokale MariaDB.
- **Auto-Migration beim Start:** ausstehende EF-Migrationen werden automatisch angewendet —
  kein manuelles `dotnet ef database update` gegen Produktiv nötig.
- **Reverse-Proxy:** `UseForwardedHeaders()` + persistente Data-Protection-Schlüssel unter
  `App_Data/keys` (sonst werden bei jedem Neustart alle Nutzer ausgeloggt).

---

## 2. Routine-Deploy (der einfache Weg)

Nach Code-Änderungen einfach im Repo-Ordner ausführen:

```powershell
.\deploy.ps1
```

Das Skript macht alles: `dotnet publish` → mit **tar** packen → per `scp` hochladen → auf dem
Server Dienst stoppen, Dateien tauschen (**`App_Data` bleibt erhalten**), Rechte setzen, Dienst
starten, Health-Check. Am Ende im Browser **Strg+F5** (Asset-Cache leeren).

Optionen:
```powershell
.\deploy.ps1 -SkipPublish     # vorhandenen .\publish-Ordner nutzen
.\deploy.ps1 -Server root@andere.ip -Service noose -AppDir /var/www/noose
```

> **Wichtig:** Immer `tar` verwenden (macht das Skript). **Nie** `Compress-Archive` — das hat
> beim ersten Deploy Dateien als 0 Bytes gepackt (kaputtes MudBlazor-CSS / blockierte Skripte).

### SSH-Key (passwortloser Deploy, empfohlen)

Damit `deploy.ps1` nicht nach dem Passwort fragt, einmalig einen Schlüssel hinterlegen
(in PowerShell auf deinem PC):

```powershell
# Schlüssel erzeugen (falls noch keiner da ist) – Enter für Default-Pfad, leere Passphrase ok
ssh-keygen -t ed25519

# Öffentlichen Schlüssel auf den Server kopieren
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh root@195.20.225.12 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys"
```

Danach läuft `.\deploy.ps1` komplett ohne Passwort-Eingabe.

---

## 3. Manueller Deploy (Fallback, falls das Skript mal nicht geht)

**Auf dem PC** (Repo-Ordner):
```powershell
dotnet publish .\NOOSE-Website\NOOSE-Website.csproj -c Release -o .\publish
tar -czf noose-publish.tgz -C .\publish .
scp .\noose-publish.tgz root@195.20.225.12:/tmp/
```

**Auf dem Server:**
```bash
systemctl stop noose
# Alten Stand entfernen, aber App_Data (Uploads + Schlüssel) behalten:
find /var/www/noose -mindepth 1 -maxdepth 1 ! -name App_Data -exec rm -rf {} +
tar -xzf /tmp/noose-publish.tgz -C /var/www/noose
chown -R www-data:www-data /var/www/noose
systemctl start noose
rm -f /tmp/noose-publish.tgz
journalctl -u noose -f       # Logs prüfen (Strg+C beendet)
```

---

## 4. Betrieb / nützliche Befehle (auf dem Server)

```bash
journalctl -u noose -f                          # Live-Logs
systemctl status noose                          # Status
systemctl restart noose                         # Neustart
curl -s http://127.0.0.1:5000/health            # erwartet: Healthy

# Datenbank ansehen
mysql noose -e "SHOW TABLES;"

# TLS-Zertifikat: certbot erneuert automatisch; Test:
certbot renew --dry-run
```

### Backups der Datenbank

```bash
# Backup
mysqldump --single-transaction noose | gzip > ~/noose-backup-$(date +%F).sql.gz

# Restore
gunzip < ~/noose-backup-2026-06-11.sql.gz | mysql noose
```

Optional als täglicher Cronjob (`crontab -e`):
```
15 4 * * * mysqldump --single-transaction noose | gzip > /root/backups/noose-$(date +\%F).sql.gz
```

---

## 5. Einmalige Server-Einrichtung (Referenz / Disaster Recovery)

Falls der Server neu aufgesetzt werden muss — die komplette Erstinstallation in Kurzform.

### 5.1 Pakete
```bash
apt update && apt upgrade -y
# .NET 10 Runtime
apt install -y aspnetcore-runtime-10.0 unzip
# Datenbank
apt install -y mariadb-server
systemctl enable --now mariadb
# Webserver + TLS
apt install -y nginx certbot python3-certbot-nginx
```

### 5.2 Datenbank anlegen
```bash
mysql
```
```sql
CREATE DATABASE noose CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'noose'@'localhost' IDENTIFIED BY 'DEIN_DB_PASSWORT';
CREATE USER 'noose'@'127.0.0.1' IDENTIFIED BY 'DEIN_DB_PASSWORT';
GRANT ALL PRIVILEGES ON noose.* TO 'noose'@'localhost';
GRANT ALL PRIVILEGES ON noose.* TO 'noose'@'127.0.0.1';
FLUSH PRIVILEGES;
EXIT;
```

### 5.3 Secrets / Env-Datei
`/etc/noose/noose.env` (danach `chmod 600` + `chown root:root`):
```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
# Zeitzone des App-Prozesses. ZWINGEND: In Blazor Server nutzt .ToLocalTime() die
# Server-Zeitzone. Ohne dies läuft der Server in UTC und alle Zeiten sind 2 h zu früh
# (über Mitternacht sogar der falsche Tag). Nach Änderung: systemctl restart noose.
TZ=Europe/Berlin
ConnectionStrings__ProductionConnection=Server=127.0.0.1;Port=3306;Database=noose;User ID=noose;Password=DEIN_DB_PASSWORT;SslMode=None;
Authentication__Discord__ClientId=DEINE_DISCORD_CLIENT_ID
Authentication__Discord__ClientSecret=DEIN_DISCORD_CLIENT_SECRET
Bootstrap__AdminDiscordId=DEINE_DISCORD_ID
```

### 5.4 systemd-Dienst
`/etc/systemd/system/noose.service`:
```ini
[Unit]
Description=NOOSE Website (Blazor Server)
After=network.target

[Service]
WorkingDirectory=/var/www/noose
ExecStart=/usr/bin/dotnet /var/www/noose/NOOSE-Website.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=noose
User=www-data
EnvironmentFile=/etc/noose/noose.env

[Install]
WantedBy=multi-user.target
```
```bash
systemctl daemon-reload
systemctl enable --now noose
```

### 5.5 nginx
`/etc/nginx/sites-available/noose`:
```nginx
# WebSocket-Upgrade für Blazor Server (SignalR) — zwingend
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}
server {
    listen 80;
    server_name noose.info www.noose.info;
    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection $connection_upgrade;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 100s;
    }
}
```
```bash
ln -s /etc/nginx/sites-available/noose /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl reload nginx

ufw allow OpenSSH
ufw allow 'Nginx Full'
ufw --force enable
```

### 5.6 HTTPS (nachdem DNS auf den Server zeigt)
```bash
certbot --nginx -d noose.info -d www.noose.info
```
certbot ergänzt den 443-Block + http→https-Weiterleitung automatisch und erneuert sich selbst.

### 5.7 DNS (im STRATO-Kundenbereich)
| Typ | Host | Wert |
|-----|------|------|
| A | `@` | `195.20.225.12` |
| A | `www` | `195.20.225.12` |
| AAAA | `@` / `www` | löschen **oder** auf die Server-IPv6 setzen (sonst muss nginx auch auf `[::]:80/443` lauschen) |

### 5.8 Discord-Login
Im Discord Developer Portal → OAuth2 → Redirects eintragen:
```
https://noose.info/signin-discord
```

---

## 6. Optional: Deploy per GitHub Action

Wer lieber bei jedem Push automatisch deployen will, legt `.github/workflows/deploy.yml` an
und hinterlegt in den Repo-Secrets `DEPLOY_SSH_KEY` (privater SSH-Schlüssel; passender
öffentlicher Schlüssel muss in `~/.ssh/authorized_keys` auf dem Server liegen).

```yaml
name: Deploy
on:
  workflow_dispatch:          # manuell auslösbar
  push:
    branches: [ master ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish
        run: dotnet publish NOOSE-Website/NOOSE-Website.csproj -c Release -o publish

      - name: Pack (tar)
        run: tar -czf noose-publish.tgz -C publish .

      - name: SSH vorbereiten
        run: |
          mkdir -p ~/.ssh
          echo "${{ secrets.DEPLOY_SSH_KEY }}" > ~/.ssh/id_ed25519
          chmod 600 ~/.ssh/id_ed25519
          ssh-keyscan -H 195.20.225.12 >> ~/.ssh/known_hosts

      - name: Upload
        run: scp -i ~/.ssh/id_ed25519 noose-publish.tgz root@195.20.225.12:/tmp/

      - name: Deploy
        run: |
          ssh -i ~/.ssh/id_ed25519 root@195.20.225.12 \
            "systemctl stop noose \
             && find /var/www/noose -mindepth 1 -maxdepth 1 ! -name App_Data -exec rm -rf {} + \
             && tar -xzf /tmp/noose-publish.tgz -C /var/www/noose \
             && chown -R www-data:www-data /var/www/noose \
             && systemctl start noose \
             && rm -f /tmp/noose-publish.tgz"
```

> Voraussetzung: Der GitHub-Runner muss den Server per SSH erreichen (öffentliche IP, Port 22).
> `deploy.ps1` braucht das alles nicht — es ist der schnellste Weg ohne zusätzliche Einrichtung.

---

## 7. Troubleshooting

| Symptom | Ursache & Lösung |
|---------|------------------|
| **`Connect Timeout expired`** beim Start, Dienst im Neustart-Loop | DB nicht erreichbar. Die gemanagte STRATO-DB (`*.webspace-host.com`) ist vom V-Server aus **nicht** erreichbar → lokale MariaDB nutzen (Abschnitt 5.2) und `ProductionConnection` auf `127.0.0.1` zeigen lassen. |
| **`Kein Connection-String konfiguriert`** | Weder `ProductionConnection` noch `DefaultConnection` gesetzt/erreichbar → `/etc/noose/noose.env` prüfen, Dienst neu starten. |
| **Seite lädt, aber ohne CSS/Styling** | Assets als 0 Bytes ausgeliefert — kaputte ZIP von `Compress-Archive`. Mit **`tar`** neu deployen (`deploy.ps1`). Check: `curl -s -o /dev/null -w "%{http_code} %{size_download}\n" http://127.0.0.1:5000/_content/MudBlazor/MudBlazor.min.css` muss > 0 Bytes liefern. Browser mit Strg+F5 neu laden. |
| **`Failed to find a valid digest in the 'integrity' attribute`** (Konsole) | Gleiche Ursache: betroffene JS-/CSS-Datei kam mit 0 Bytes an → mit `tar` neu deployen. |
| **certbot scheitert mit IPv6-Adresse / `204`** | Alter `AAAA`-Eintrag zeigt auf STRATO-Parkserver. AAAA löschen (oder auf Server-IPv6 setzen), bis `getent ahosts noose.info` nur die `195.20.225.12` zeigt, dann certbot erneut. |
| **`Failed to determine the https port for redirect`** (Log) | Harmlos. Tritt nur bei direkten http-Anfragen an Kestrel auf; über nginx+TLS verschwindet die Warnung. |
| **Login: „invalid redirect_uri"** | Im Discord Developer Portal `https://noose.info/signin-discord` als Redirect eintragen. |
| **Zeiten 2 h zu früh / falscher Tag** | Server läuft in UTC. In Blazor Server nutzt `.ToLocalTime()` die Server-Zeitzone. `TZ=Europe/Berlin` in `/etc/noose/noose.env` ergänzen, dann `systemctl restart noose` (Neustart nötig — `TimeZoneInfo.Local` ist pro Prozess gecacht). |
| **502 Bad Gateway** | App läuft nicht → `systemctl status noose` + `journalctl -u noose -e`. |
| **Nutzer nach jedem Deploy ausgeloggt** | Data-Protection-Schlüssel weg → `App_Data` darf beim Deploy **nicht** gelöscht werden (das Skript behält es). |

#!/usr/bin/env bash
# One-shot, idempotent setup for the second (demo) NOOSE instance on the SAME server.
# Touches production ONLY by: reading its env, dumping its DB (backup), reloading nginx.
# Never modifies the prod DB / prod env / prod service / prod nginx file.
# Run as root on the server:  bash setup-demo.sh
set -euo pipefail

PROD_ENV=/etc/noose/noose.env
DEMO_ENV_DIR=/etc/noose-demo
DEMO_ENV="$DEMO_ENV_DIR/noose-demo.env"
DEMO_DIR=/var/www/noose-demo
BACKUP_DIR=/root/backups
TS="$(date +%F-%H%M)"

echo "==> [1/6] Backup der Produktiv-DB (noose)"
mkdir -p "$BACKUP_DIR"
mysqldump --single-transaction noose | gzip > "$BACKUP_DIR/noose-prod-$TS.sql.gz"
ls -lh "$BACKUP_DIR/noose-prod-$TS.sql.gz"

echo "==> [2/6] Demo-DB anlegen + Rechte (Prod-DB unberuehrt)"
mysql -e "CREATE DATABASE IF NOT EXISTS noose_demo CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -e "GRANT ALL PRIVILEGES ON noose_demo.* TO 'noose'@'localhost'; GRANT ALL PRIVILEGES ON noose_demo.* TO 'noose'@'127.0.0.1'; FLUSH PRIVILEGES;"

echo "==> [3/6] Env-Datei ableiten (Secrets aus Prod uebernehmen, nur Port + DB-Name aendern)"
test -f "$PROD_ENV" || { echo "FEHLER: $PROD_ENV fehlt"; exit 1; }
mkdir -p "$DEMO_ENV_DIR"
# copy prod env, swap DB name, force exactly one ASPNETCORE_URLS line on port 5001
grep -v '^ASPNETCORE_URLS=' "$PROD_ENV" \
  | sed 's/Database=noose;/Database=noose_demo;/' > "$DEMO_ENV"
echo 'ASPNETCORE_URLS=http://127.0.0.1:5001' >> "$DEMO_ENV"
chmod 600 "$DEMO_ENV"; chown root:root "$DEMO_ENV"
grep -q 'Database=noose_demo;' "$DEMO_ENV" || { echo "FEHLER: 'Database=noose;' nicht in $PROD_ENV gefunden – Connection-String pruefen"; exit 1; }
echo "    OK: $DEMO_ENV (Port 5001, DB noose_demo)"

echo "==> [4/6] App-Verzeichnis $DEMO_DIR"
mkdir -p "$DEMO_DIR/App_Data"
chown -R www-data:www-data "$DEMO_DIR"

echo "==> [5/6] systemd-Dienst noose-demo"
cat > /etc/systemd/system/noose-demo.service <<'UNIT'
[Unit]
Description=NOOSE Website DEMO (Blazor Server)
After=network.target

[Service]
WorkingDirectory=/var/www/noose-demo
ExecStart=/usr/bin/dotnet /var/www/noose-demo/NOOSE-Website.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=noose-demo
User=www-data
EnvironmentFile=/etc/noose-demo/noose-demo.env

[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable noose-demo >/dev/null 2>&1 || true
echo "    Dienst registriert (Start erfolgt beim ersten deploy.ps1, wenn die DLL da ist)."

echo "==> [6/6] nginx-Site demo.noose.info (HTTP; TLS folgt via certbot)"
# Re-uses the global 'map $http_upgrade $connection_upgrade' already defined in the prod site.
cat > /etc/nginx/sites-available/noose-demo <<'NGINX'
server {
    listen 80;
    server_name demo.noose.info;
    location / {
        proxy_pass         http://127.0.0.1:5001;
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
NGINX
ln -sf /etc/nginx/sites-available/noose-demo /etc/nginx/sites-enabled/noose-demo
nginx -t
systemctl reload nginx

echo ""
echo "==> FERTIG. Demo-Instanz vorbereitet (Backup, DB, Env, Dienst, nginx)."
echo "    Weiter auf dem PC:  .\\deploy.ps1 -AppDir /var/www/noose-demo -Service noose-demo"
echo "    Danach TLS:         certbot --nginx -d demo.noose.info"

-- Leert die "Im Rollenspiel bedeutet das ..."-Kaesten in allen Handbuchartikeln.
--
-- Der Kasten wird nur gerendert, wenn RollenspielHtml gefuellt ist
-- (HandbookArticleReader.razor) - NULL laesst ihn komplett verschwinden.
--
-- Ausfuehren auf dem Server:
--   set -a; . /etc/noose/noose.env; set +a
--   mysql --defaults-file=<(printf '[client]\nuser=...\npassword=...\n') <DB> < rollenspiel-leeren.sql
-- oder schlicht im Datenbank-Werkzeug der Wahl gegen die noose-Datenbank.

-- 1) Sicherung. Laeuft nur beim ersten Mal an; danach bleibt die alte Kopie stehen.
CREATE TABLE IF NOT EXISTS HandbuchArtikel_Rollenspiel_Sicherung (
    Id             varchar(255) NOT NULL,
    SeedSchluessel varchar(255) NULL,
    Slug           varchar(64)  NULL,
    RollenspielHtml longtext    NULL,
    GesichertAm    datetime(6)  NOT NULL,
    PRIMARY KEY (Id)
);

INSERT IGNORE INTO HandbuchArtikel_Rollenspiel_Sicherung (Id, SeedSchluessel, Slug, RollenspielHtml, GesichertAm)
SELECT Id, SeedSchluessel, Slug, RollenspielHtml, UTC_TIMESTAMP()
FROM   HandbuchArtikel
WHERE  RollenspielHtml IS NOT NULL AND RollenspielHtml <> '';

-- 2) Vorher zaehlen, damit die Zahl zum Ergebnis passt.
SELECT COUNT(*) AS betroffen
FROM   HandbuchArtikel
WHERE  RollenspielHtml IS NOT NULL AND RollenspielHtml <> '';

-- 3) Leeren. Auch soft-geloeschte Zeilen, damit ein Restore den Kasten nicht zurueckholt.
UPDATE HandbuchArtikel
SET    RollenspielHtml = NULL
WHERE  RollenspielHtml IS NOT NULL;

-- 4) Kontrolle: muss 0 ergeben.
SELECT COUNT(*) AS verbleibend
FROM   HandbuchArtikel
WHERE  RollenspielHtml IS NOT NULL AND RollenspielHtml <> '';

-- Zuruecknehmen, falls doch gewuenscht:
-- UPDATE HandbuchArtikel a
--   JOIN HandbuchArtikel_Rollenspiel_Sicherung s ON s.Id = a.Id
--   SET a.RollenspielHtml = s.RollenspielHtml;

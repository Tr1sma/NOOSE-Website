# NOOSE – Nachrichtendienst-Website · Projektplan

> Interne Web-Plattform für das **National Office of Security Enforcement (N.O.O.S.E.)** auf einem FiveM-/GTA-RP-Server.
> Ziel: Die bisher in Discord-Threads verstreuten Informationen werden durch eine zentrale, durchsuchbare und untereinander verknüpfte **Akten-Datenbank** ersetzt – mit Einstufungen, Verläufen, Freigabe-Workflows und auswertbaren Statistiken.

**Status der Datei:** lebendes Dokument. Aufgaben sind als Checkboxen geführt (`[ ]` offen, `[x]` erledigt). Phasen sind so geschnitten, dass nach jeder Phase etwas **Lauffähiges und Testbares** vorliegt.

---

## 1. Ziel & Kontext

- **Problem heute:** Alles läuft über Discord-Tickets/-Threads. Man muss erst Nachrichten lesen, weiß nicht, ob Infos aktuell sind, und kann nichts auswerten.
- **Lösung:** Pro **Person** und pro **Fraktion** eine zentrale Akte, in der alles zusammenläuft. Jede Verknüpfung ist in **beide Richtungen klickbar** (Person ↔ Fraktion ↔ Gruppe ↔ Partei ↔ Dok ↔ Quelle). Möglichst viel über **Vorlagen/Vorgaben**, damit Daten einheitlich und auswertbar sind.
- **Rollenverteilung im Bau:** Die Entwicklung übernimmt im Wesentlichen die KI; der Auftraggeber **testet** und macht kleine Anpassungen. Der Plan ist deshalb implementierungsnah und liefert je Phase klare **Abnahmekriterien** zum Testen.

---

## 2. Grundsatz-Entscheidungen (festgelegt)

| Thema | Entscheidung |
|---|---|
| **Nutzerkreis** | NOOSE pflegt alles. DoJ/LSPD/LSMD = **Partner mit Lesezugriff** auf freigegebene Inhalte (eigene, spätere Phase). |
| **Login** | **Discord-OAuth**. Selbst-Registrierung → **Admin-/Führungs-Freigabe** → Dienstgrad. |
| **Berechtigungen** | **Dienstgrad-basiert**. Führung beginnt ab *Supervisory Special Agent*. |
| **Aktenmodell** | **Zentrale Person** als Akte + beliebig viele **Personen-Doks** (Verhöre/Maßnahmen) + Einstufungs-Verlauf. |
| **Identität** | Interne ID + Name/Aliase + Foto (keine externe State-ID). |
| **Sichtbarkeit** | Alle aktiven NOOSE-Agenten sehen alle Akten (Ausnahme: Verschlusssachen). Löschen nur Führung/Admin. Alles wird protokolliert. |
| **Freigaben** | Echter **Antrags-/Posteingang-Workflow** (Hochstufungen, Taskforce-Genehmigung, Account-Freigabe, Beförderungen). |
| **Quellen** | Datei-Upload + Discord-/Web-Links + **interne Verknüpfungen** + Freitext. |
| **Suche** | **Volltext** über alle Inhalte + Tags/Filter + gespeicherte Suchen. |
| **Aktualität/Historie** | Vollständige Änderungs-Historie + „zuletzt aktualisiert" + Aktualitäts-Ampel/Wiedervorlage. |
| **Tech** | Blazor (.NET 10, Interactive Server) · **MariaDB/MySQL** · eigener VPS · eigenständige DB (keine Game-DB-Anbindung). |
| **Optik** | Dunkler **Geheimdienst-/Akten-Look**, Statusfarben, NOOSE-Wappen, **Desktop-Fokus** (responsiv). |
| **UI-Technik** | **MudBlazor** mit individuellem dunklem NOOSE-Theme. |
| **Aufbau** | Fraktionen & Parteien getrennt, gleiche Technik. Start mit **Personen-Akten**. **Dashboard**-Startseite. |
| **Altdaten** | Start bei null (keine Migration). |

---

## 3. Technischer Stack

- **Framework:** ASP.NET Core / **Blazor Web App, .NET 10**, Render-Modus **Interactive Server** (SignalR). Bereits aufgesetzt.
- **UI:** **MudBlazor** (dunkles Theme, DataGrids, Dialoge, Autocomplete für Verknüpfungen, Charts).
- **Datenbank:** **MariaDB/MySQL** über **EF Core** mit **Pomelo.EntityFrameworkCore.MySql**.
- **Auth:** **ASP.NET Core Identity** + **Discord-OAuth** (`AspNet.Security.OAuth.Discord`), optionaler **Discord-Rollen-Sync**.
- **Dateien/Bilder:** Upload in **geschützten Ordner außerhalb von `wwwroot`**, Auslieferung über autorisierten Endpoint.
- **Audit & Zugriffsprotokoll:** EF Core **SaveChanges-Interceptor** + Lese-Logging.
- **Hintergrund-Jobs:** geplante Aufgaben (Wiedervorlagen, automatischer Lagebericht) via Hosted Service / Quartz.NET.
- **Diagramme/Graph:** MudBlazor-/ApexCharts für Statistiken; JS-Interop (z. B. vis-network) für Beziehungsgraph & Pfadsuche.
- **Konfiguration/Secrets:** `appsettings.json` + **User Secrets** (lokal) / Umgebungsvariablen (Server) für Connection-String und Discord-Client-Secret.

---

## 4. Architektur-Überblick

**Schichten:**
- `Components/` – Blazor-Seiten & UI-Komponenten (Razor).
- `Data/` – `AppDbContext`, EF-Entitäten, Migrations.
- `Models/` – Domänenmodelle/Enums (Dienstgrad, Einstufung, Maßnahme-Ausgang …).
- `Services/` – Geschäftslogik (PersonService, FraktionService, SearchService, AuditService, RequestService, NotificationService, TaskService, ReportService …).
- `Authorization/` – Policies, Requirements, Handler (dienstgradbasiert, Verschlusssachen).
- `Infrastructure/` – Dateispeicher, Interceptoren, Hintergrund-Jobs, Seeding.

**Grundprinzipien:**
- Dünne Razor-Komponenten, Logik in Services (testbar, austauschbar).
- Generische Querschnitts-Module (Quellen/Anhänge, Kommentare, Tags, Verknüpfungen, Audit, Soft-Delete, Custom-Felder) die an **jede** Akte angehängt werden können.
- Eine zentrale **Verknüpfungs-Engine**: speichert Relationen zwischen Entitäten, rendert sie in beiden Richtungen klickbar und speist Graph, Pfadsuche und Vorschläge.

---

## 5. Datenmodell (Entitäten – Überblick)

> Detailfelder werden je Phase final ausmodelliert; hier der Bauplan.

- **Agent** (Nutzer): Discord-ID, Anzeigename, **Dienstgrad** (NOOSE) bzw. Partner-Rolle, **Status** (Ausstehend/Aktiv/Gesperrt), **TRU-Flag**, Taskforce-Mitgliedschaften (inkl. **Notfall-Sperre** & optionalem **Discord-Rollen-Sync**).
- **Person** (zentrale Akte): interne ID, Name, **Aliase**, **Telefonnummern**, **Fahrzeuge/Kennzeichen**, **bekannte Orte**, **Waffen**, **Foto-Galerie**, Lebensstatus, aktuelle **Einstufung**, **Verschlusssache-Flag**, Fraktionszugehörigkeit (verknüpft, mit Historie), Tags, Audit-Felder.
- **Personen-Dok** (Ereignis, n je Person): Datum/Uhrzeit, **Grund**, Fraktionszugehörigkeit, **Erhaltene Informationen**, **Wahrheitsserum** (Ja/Nein), **Beendigung der Maßnahme** (Spritze/offiziell/erschossen/laufen), Anhänge/Quellen, Ersteller. Optional aus **Vorlage**.
- **Einstufung** (Prüffall → Verdachtsfall → Gesichert staatsgefährdend) mit **Verlauf** (wer, wann, Begründung, ggf. Antragsbezug) – für **Person, Personengruppe, Partei** (Fraktion optional).
- **Person-Beziehung**: Person ↔ Person mit Typ (Familie/Verbündeter/Feind/Geschäftspartner …) → speist Beziehungsgraph & Pfadsuche.
- **Personengruppe**: Name, Beschreibung, Einstufung+Verlauf, Mitglieder (→ Personen), zugeteilte Agents, **Erfassungsfortschritt** „x/y Mitglieder mit Akte".
- **Fraktion**: Name/Art, Funk, Darkchat, Ausstellungszeiten, **Konflikte** (→ Fraktionen/Parteien), **Leaderschaft** (→ Personen), **Mitglieder** (→ Personen, mit Fraktions-Rang), **Erkennungsfarbe**, **Waffenbestand** (Liste), **Lagerbestand** (Liste), **Ränge** (Liste), Ziele, optionale Einstufung, **Bedrohungs-Score**.
- **Partei**: Name, **Leitung** (→ Person), Mitglieder (→ Personen), Einstufung+Verlauf, Ziele, Bemerkungen, **zugeteilte Special Agents** (→ Agents).
- **Taskforce**: Name, **Chefermittler**, **CID-Lead** (operative Leitung), **TRU-Lead** (taktisch-operative Leitung), Mitglieder (→ Agents), Sinn/Zweck, **Geltungsbereich** (überbehördlich/innerbehördlich), Genehmigungsstatus.
- **Operation/Einsatzbericht**: Titel, Zeitraum, Beteiligte (Agents/Personen/Fraktionen), Ablauf, Ergebnis, Verknüpfungen.
- **Fall/Vorgang (Case)**: übergeordnete Akte, die Personen, Doks, Operationen & Observationen zu einem Vorgang mit eigenem Status bündelt.
- **Observation**: Überwachungs-/Observationseintrag an einer Person (Zeit, Ort, Beobachtung, Agent) – getrennt von Verhör-Doks.
- **Aufgabe/Task**: Zuweisung an Agent(en) mit Fälligkeit/Status (z. B. „Person X beobachten").
- **Gespeicherte Suche/Smart-Liste**: gespeicherte Filterkombination, dynamisch aktualisiert.
- **Personalakte** (je Agent): Dienstgrad-Verlauf, Belobigungen, Disziplinarisches, Beförderungsanträge.
- **Antrag**: Typ (Hochstufung/Taskforce/Account-Freigabe/Beförderung), Bezug, Antragsteller, Begründung, Status, Entscheider, Zeitpunkt → **Posteingang**.
- **Quelle/Anhang** (generisch): Typ (Upload/Link/intern/Freitext), Ziel-Entität, Metadaten, Uploader.
- **Kommentar/Vermerk** (generisch, mit **@-Erwähnungen**), **Tag/Label** (generisch), **Watchlist-Eintrag**, **Benachrichtigung**, **News/Ankündigung**, **Broadcast/Rundnachricht**, **Termin** (Kalender), **Ort** (Karte).
- **Dokument/Datei (Bibliothek)**: zentrale, durchsuchbare Ablage (Formulare, SOPs, Vorlagen) mit Kategorien.
- **Rechtsgrundlage/Gesetz**: Paragraf/Norm mit Text, verknüpfbar mit Fällen/Doks.
- **Custom-Feld-Definition**: admin-definierte Zusatzfelder je Aktentyp.
- **Lagebericht (Archiv)**: automatisch erzeugte, archivierte Berichte.
- **Audit-Log** (Änderungen) & **Zugriffs-Log** (Ansichten), generisch über alle Entitäten. Alle Akten unterstützen **Soft-Delete** (Papierkorb).
- **Basisdaten/Lookups** (admin-editierbar): Fraktionsliste, Dienstgrade, Einstufungs-Optionen, Maßnahme-Ausgänge, Beziehungstypen, Geltungsbereiche, Tags, Dok-Vorlagen.

---

## 6. Rollen, Dienstgrade & Rechte

**NOOSE-Dienstgrade (aufsteigend):**
1. Junior Agent
2. Special Agent
3. Senior Special Agent
4. **Supervisory Special Agent** ← *ab hier Führung*
5. Deputy Director
6. Director

**Querschnitt:** **TRU (Tactical Response Unit)** = Flag, unabhängig vom Rang (jeder Rang kann rein). **Admin** = technische Systemrolle (Auftraggeber; ggf. an Director gekoppelt).

**Rechte-Matrix (NOOSE):**

| Aktion | Erlaubt ab |
|---|---|
| Akten lesen (alles außer Verschlusssachen) | jeder aktive Agent |
| Akten/Doks anlegen & bearbeiten | jeder aktive Agent |
| Einstufung *Prüffall* / *Verdachtsfall* setzen | jeder aktive Agent |
| Einstufung *Gesichert staatsgefährdend* setzen | **direkt ab Senior Special Agent**; darunter **per Antrag** an ≥1 Senior Special Agent |
| Verschlusssache sehen | nur Führung bzw. ausdrücklich zugewiesene Agenten |
| Akten löschen/archivieren (Papierkorb) | Führung (Supervisory+) / Admin |
| Account-Freigabe & Rangvergabe | Führung / Admin |
| Notfall-Sperre (Account/Sessions) | Führung / Admin |
| Taskforce genehmigen | Führung / Admin |
| Personalakten einsehen/pflegen | Führung / Admin |
| Beförderung vorschlagen / entscheiden | Vorschlag ab Supervisory · Entscheidung Deputy Director+/Admin |
| Broadcast/Rundnachricht senden | Führung / Admin |
| Basisdaten/Lookups & Vorlagen verwalten | Führung / Admin |
| Custom-Felder (Definitionen) verwalten | Führung / Admin |
| Theming, Wartungsmodus, Systemverwaltung | Admin |

**Partner (DoJ/LSPD/LSMD – Phase 9):** eigene Rollen, **nur Lesezugriff** auf als „für Partner freigegeben" markierte Inhalte.

---

## 7. Querschnittsthemen (gelten für alle Phasen)

- **Audit/Historie:** Jede Änderung wird mit Wer/Wann/Alt→Neu protokolliert; „zuletzt aktualisiert von/am" überall sichtbar.
- **Zugriffsprotokoll:** Aufrufe sensibler Akten werden geloggt (wer hat wann was angesehen).
- **Papierkorb/Soft-Delete:** Gelöschtes wird zunächst nur als gelöscht markiert (Papierkorb) und ist durch Führung wiederherstellbar – mit Pflicht-Begründung.
- **Verschlusssachen:** Einzelne Akten können als Verschlusssache markiert werden und sind dann nur für Führung/zugewiesene Agenten sichtbar – greift auch in Suche, Listen und Graph.
- **Wartungsmodus:** Admin kann die Seite kurzfristig sperren und ein Ankündigungsbanner schalten.
- **Sicherheit:** Login-Pflicht, Antiforgery, dienstgradbasierte Policies serverseitig erzwungen, Upload-Validierung (Typ/Größe), Secrets außerhalb des Codes, Rate-Limit auf Login.
- **Validierung:** Pflichtfelder & Plausibilitätsprüfung in allen Formularen.
- **Suche:** Jede neue Entität wird in die globale Volltextsuche aufgenommen.
- **Sichtbarkeit/Freigabe:** Datenfelder für den späteren Partner-Zugriff werden früh vorbereitet, aber erst in Phase 9 aktiviert.

---

## 8. Funktionsumfang

**Enthalten:** Discord-Login & Freigabe · **Discord-Rollen-Sync** · dienstgradbasierte Rechte · **Notfall-Sperre (Kill-Switch)** · Personen-Akten mit erweitertem Steckbrief (Aliase, Telefon, Fahrzeuge, Orte, Waffen) · Foto-Galerie · Personen-Doks · Einstufungen + Verlauf + Antragsworkflow · Person-zu-Person-Beziehungen · Personengruppen (mit Erfassungsfortschritt) · Fraktionen (Bestände, Ränge, Konflikte, Erkennungsfarbe) · Parteien · Taskforces · Operationen/Einsatzberichte · Fall-/Vorgangsakten · Überwachungs-/Observationsprotokoll · generische Quellen/Anhänge · interne Verlinkung · Kommentare (@-Erwähnungen) · Tags · globale Volltextsuche · gespeicherte Suchen/Smart-Listen · Command-Palette (Strg+K) · Änderungs-Historie & Zugriffsprotokoll · Papierkorb/Soft-Delete · Duplikat-Erkennung & Zusammenführen · Verschlusssachen-Stufe · Aktualitäts-Ampel + Wiedervorlage · Quick-Add · Vorlagen/Templates · **konfigurierbare Custom-Felder** · Watchlist · In-App-Benachrichtigungen · Aufgaben/To-Dos & Zuweisungen · News/Schwarzes Brett · **Behörden-Broadcast** · **Dokumenten-/Datei-Bibliothek** · **Gesetzbuch/Rechtsgrundlagen** · Dashboard mit Kennzahlen & Aktivitäts-Feed · Statistik-Reports/Export · **automatischer Lagebericht** · PDF-Export einzelner Akten · Beziehungs-/Netzwerk-Graph · Beziehungs-Pfad-Suche · Verknüpfungs-Vorschläge · Organigramm/Personalübersicht · Personalakte je Agent + Beförderungs-Workflow · Zeitstrahl/Timeline je Akte · Karte mit Orten · Kalender/Termine · automatischer Bedrohungs-Score · **Theming/Logo-Upload** · **Wartungsmodus + Banner** · Partner-Lesezugriff.

**Bewusst ausgelassen** (nicht gewählt, später ergänzbar): Discord-Push-Benachrichtigungen · „Wer ist online" · dediziertes „Most-Wanted"-Board · Informanten-/V-Personen-Verwaltung · Beweismittel-/Asservaten-Register · Dienstausweis-/Steckbrief-Generator · 2FA · Login-/Sitzungsprotokoll · Vier-Augen-Prinzip · interne Direktnachrichten · Finanzen & Eigentum · Kommunikations-Netzwerk · API/Webhooks.

---

## 9. Phasenplan

> Reihenfolge nach Abhängigkeiten. Jede Phase endet mit einem testbaren Stand.

### Phase 0 – Fundament & Projekt-Setup  ✅ ABGESCHLOSSEN
**Ziel:** Lauffähige, gestylte Hülle mit DB-Anbindung.
- [x] Template-Reste entfernt (Counter/Weather, Bootstrap), Projektstruktur/Ordner angelegt (`Data/ Models/ Services/ Authorization/ Infrastructure/ Theme/`).
- [x] NuGet: **MudBlazor 9.5.0**, **Pomelo MySQL 9.0.0** (EF Core 9, läuft auf .NET 10), EF Core Design + HealthChecks. *(Discord-OAuth & Identity bewusst auf Phase 1 verschoben, wo sie verdrahtet werden.)*
- [x] MudBlazor eingebunden + **dunkles NOOSE-Theme „Anthrazit + Cyan"** (`Theme/NooseTheme.cs`, Statusfarben grün/gelb/rot, NOOSE-Wappen `NooseIcon.png` in Topbar + Favicon).
- [x] Basis-Layout: MudAppBar (Topbar), MudDrawer (Seitennavigation), Inhaltsbereich; Dashboard-Gerüst.
- [x] `AppDbContext` + Connection-String, erste Migration `InitialCreate`, DB-Verbindung verifiziert (`__EFMigrationsHistory` angelegt).
- [x] Konfig/Secrets-Setup (User Secrets). `ServerVersion.AutoDetect` → passt sich Umgebung an (lokal MariaDB/XAMPP, Prod MySQL 8.0).
- [x] Health-Endpoint `/health` + Status-Seite `/status` (zeigt DB-Verbindung grün/rot, Server-Version).

**Abnahme (erfüllt):** App startet · dunkles Theme sichtbar · `/health` = Healthy · `/status` zeigt DB verbunden · Navigationsgerüst vorhanden.

> **⚠️ DB-Erkenntnis (wichtig für Phase 10):** Die gehostete **IONOS-MySQL** (`database-…​.webspace-host.com`) ist **nur aus dem IONOS-Hosting erreichbar**, nicht vom lokalen Rechner (DNS liefert keine öffentliche IP, Port 3306 von außen zu). Daher: **lokale Entwicklung gegen XAMPP-MySQL** (DB `noose`, User `root`), **Produktion gegen IONOS** (Connection-String dort als Umgebungsvariable). Der Code ist dank `AutoDetect` für beide identisch.

### Phase 1 – Auth, Accounts & Rechte  ✅ ABGESCHLOSSEN
**Ziel:** Sicherer Login mit Freigabe und dienstgradbasierten Rechten.
- [x] ASP.NET Core Identity + **Discord-Login** (Identity.EFCore 9.0.16, AspNet.Security.OAuth.Discord 10.0.0; eigene deutsche Blazor-Login-Seite, keine scaffolded UI).
- [x] `Agent`-Modell (`IdentityUser` + Dienstgrad, Status, TRU-Flag, Discord-Bezug).
- [x] Registrierungs-Flow: erster Discord-Login → Status **Ausstehend**; nur **Aktive** erhalten eine Sitzung.
- [x] **Freigabe-Posteingang** (`/admin/freigaben`, Führung): freischalten + Rang/TRU zuweisen, ablehnen.
- [x] Dienstgrad-Hierarchie + **Authorization-Policies** (AktiverAgent/Führung/Admin/HöchsteEinstufung/BeförderungEntscheiden) – Claims-basiert.
- [x] **Verschlusssachen-Policy** vorbereitet (Requirement + Stub-Handler; volle ressourcenbasierte Prüfung ab Akten-Phase).
- [x] **Notfall-Sperre (Kill-Switch)**: SecurityStamp + kurze Revalidierung (`RevalidatingServerAuthenticationStateProvider`) → Sperre beendet alle Sitzungen sofort.
- [ ] **Discord-Rollen-Sync** (optional): *Struktur vorbereitet (`DiscordRollenSyncAm`), Bot-Sync bewusst auf spätere Phase verschoben.*
- [x] Admin-Nutzerverwaltung (`/admin/agenten`, Admin): Liste, Rang/TRU/Admin ändern, sperren/entsperren.
- [x] Seeding: erster Admin via **Bootstrap-Admin-Discord-ID** (User Secrets) → Director+Admin+Aktiv; „Admin"-Rolle geseedet.
- [x] **Audit- & Zugriffs-Log-Infrastruktur** (`AuditSaveChangesInterceptor`, `AuditLog`/`ZugriffsLog`) + **Soft-Delete-Basis** (`ISoftDelete` + globaler Query-Filter) – Grundgerüst steht.

**Abnahme:** Discord-Login funktioniert; neue Accounts landen „Ausstehend"; Admin gibt frei und vergibt Rang; ohne Login kein Zugriff; gesperrter Account fliegt sofort raus; zu niedrige Aktion wird serverseitig verweigert.

> **Verifiziert (ohne Discord-Secrets):** Build 0/0 · Migration `AddIdentityAndAudit` angewandt (Identity- + Audit-Tabellen, `DiscordId` unique) · `/health`=Healthy · `/`→302 auf `/Account/Login` · `/status`→302 (geschützt) · Login-Seite rendert Discord-Button + Antiforgery-Token.
> **Zum Voll-Test nötig (Auftraggeber):** Discord-App anlegen, Redirect `https://localhost:7063/signin-discord`, dann per User Secrets setzen: `Authentication:Discord:ClientId`, `Authentication:Discord:ClientSecret`, `Bootstrap:AdminDiscordId` (eigene Discord-ID). Ohne diese Secrets läuft die App, der Login-Button zeigt aber einen Konfigurationshinweis.

### Phase 2 – Personen-Akten (MVP-Kern)  ✅ ABGESCHLOSSEN
**Ziel:** Der erste echte Mehrwert – Personen verwalten.
- [x] `Person`-Entität inkl. erweitertem **Steckbrief** (Aliase, Telefon, Fahrzeuge/Kennzeichen, Orte, Waffen, Lebensstatus) – mit lesbarem **Aktenzeichen** (`NOOSE-P-{Jahr}-{Nr}`, race-sicher per Zähler-Tabelle) + GUID-PK.
- [x] **Foto-Galerie**: Mehrfach-Upload (`MudFileUpload`), Speicherung geschützt außerhalb wwwroot (`App_Data/uploads`), Auslieferung nur an eingeloggte Agenten über autorisierten Minimal-API-Endpoint `/dateien/personen/foto/{id}`.
- [x] `Personen-Dok` mit allen Feldern; **Beendigung der Maßnahme**: „Erschossen" → Lebensstatus **Tot** (temporär, 20-Min-Respawn ab Maßnahme-Zeit); „Amnestie-Spritze" → Person lebt, nur Gedächtnisverlust.
- [x] **Einstufung** (Wert + Verlauf-Timeline), Rang-Gate „Gesichert staatsgefährdend" (ab Senior Special Agent/Admin, sonst Antrag-Stub → Phase 5) – serverseitig erzwungen.
- [x] **Papierkorb/Soft-Delete** durchgängig (Hard-Delete → Soft-Delete via Interceptor, Wiederherstellung nur Führung unter `/personen/papierkorb`).
- [x] **Duplikat-Erkennung** beim Anlegen (Warn-Dialog bei gleichem Namen/Telefon, „Trotzdem anlegen").
- [x] **Listenansicht** (MudDataGrid: Quick-Filter/Sortierung/Paging) + **Detail-/Aktenansicht** (Tabs: Steckbrief/Doks/Einstufung/Fotos/Historie).
- [x] Anlegen/Bearbeiten-Formulare mit Validierung (`MudForm`).
- [x] „Zuletzt aktualisiert" + **Änderungs-Historie** pro Akte (aus Audit-Log, inkl. Doks).
- [x] **Verschlusssache**-Flag je Akte: in Liste/Detail/Foto nur für Führung/Admin sichtbar.

**Abnahme:** Person anlegen, Doks hinzufügen, Fotos hochladen, Einstufung setzen (Rang wird geprüft), Historie einsehen, Liste durchsuchen/sortieren, Gelöschtes im Papierkorb wiederherstellen.

> **Verifiziert (ohne Discord-Login):** Build 0/0 · Migration `Phase2_PersonenAkten` angewandt (alle Tabellen + Unique-Index `Personen.Aktenzeichen`) · App startet sauber · `/health`=Healthy · `/personen`, `/personen/papierkorb`, `/dateien/personen/foto/…` → 302 auf Login (Auth + Policy greifen).
> **Voll-Test (Auftraggeber):** nach Discord-Login Person anlegen → Aktenzeichen wird vergeben; Dok „Erschossen" → „Tot · respawnt in 20 Min", nach Ablauf wieder „Lebend"; Foto-Upload/Galerie; Einstufung + Rang-Gate; Papierkorb/Wiederherstellen; Verschlusssache.

### Phase 3 – Verknüpfungen, Quellen, Suche, Tags, Kommentare ✅ ABGESCHLOSSEN
**Ziel:** Das Herzstück – alles wird verknüpfbar und auffindbar.
- [X] Generisches **Quellen/Anhang-System** (Upload/Link/intern/Freitext) für jede Akte.
- [X] **Interne Verlinkung** (bidirektional) + **Person-zu-Person-Beziehungen** mit Typen.
- [X] **Globale Volltextsuche** (MariaDB FULLTEXT) + Typ-/Tag-Filter; **Verschlusssachen-Flag** je Akte greift in Suche/Listen.
- [X] **Gespeicherte Suchen/Smart-Listen**.
- [X] **Command-Palette (Strg+K)** für Schnellzugriff auf Akten/Funktionen.
- [X] **Tags/Labels** (generisch) + Verwaltung.
- [X] **Kommentare/Vermerke** (generisch).

**Abnahme:** Quelle anhängen; zwei Akten verknüpfen und in beide Richtungen navigieren; Suche findet Inhalte (nicht nur Namen); Suche speichern; per Strg+K springen; taggen & filtern; Kommentar hinterlassen.

### Phase 4 – Fraktionen & Personengruppen ✅ ABGESCHLOSSEN
**Ziel:** Organisationen abbilden und mit Personen verzahnen.
- [X] **Fraktion**-Modul (alle Felder; Bestände als strukturierte Listen; Fraktions-Ränge; Konflikte-Links; Mitglieder/Leitung verknüpft; Erkennungsfarbe; optionale Einstufung).
- [X] **Personengruppe** (Mitglieder, Einstufung+Verlauf, zugeteilte Agents, **Erfassungsfortschritt x/y**).
- [X] Rück-Verknüpfungen auf der Personenakte (zugehörige Fraktionen/Gruppen).

**Abnahme:** Fraktion mit verknüpften Mitgliedern anlegen; auf der Person erscheinen die Rück-Links; Gruppe anlegen und Fortschritt sehen.

### Phase 5 – Fälle, Operationen, Personal & Antrags-Workflow ✅ ABGESCHLOSSEN
**Ziel:** Restliche Akten-Typen, Fallarbeit + echter Freigabe-Workflow.
- [X] **Partei**-Modul.
- [X] **Taskforce**-Modul (Leads, Mitglieder, Geltungsbereich, Genehmigung).
- [X] **Operationen/Einsatzberichte**-Modul.
- [X] **Fall-/Vorgangsakten (Cases)**: bündeln Personen/Doks/Operationen/Observationen, mit eigenem Status.
- [X] **Überwachungs-/Observationsprotokoll** an Personen.
- [X] **Personalakte je Agent** (Dienstgrad-Verlauf, Belobigungen, Disziplinarisches).
- [X] **Antrags-/Posteingang-Workflow** vollständig: Hochstufung, Taskforce-Genehmigung, Account-Freigabe **und Beförderungen** vereinheitlicht; Genehmigen/Ablehnen mit Begründung + Verlauf; Verknüpfung mit dem Rang-Gate aus Phase 2.

**Abnahme:** Junior stellt Hochstufungs-Antrag → Senior genehmigt im Posteingang; Taskforce/Beförderung brauchen Führungs-Genehmigung; Fall mit mehreren Personen/Operationen anlegen; Observation erfassen.

### Phase 6 – Dashboard, Statistiken, Aufgaben & Benachrichtigungen  ✅ ABGESCHLOSSEN
**Ziel:** Überblick, Zusammenarbeit und „schöne Stats".
- [x] **Dashboard**: Kennzahlen (Fälle nach Einstufung, Maßnahme-Ausgänge, Fraktionen nach Gefährdung, offene Anträge), Charts, „zuletzt bearbeitet", Schnellsuche, **Aktivitäts-Feed**.
- [x] **Aufgaben/To-Dos & Zuweisungen** (mit Fälligkeit/Erinnerung, eigene „Meine Aufgaben"-Ansicht).
- [x] **Watchlist** (Akten folgen) + **In-App-Benachrichtigungen** (Glocke: Antrag entschieden, gefolgte Akte geändert, Taskforce-/Aufgaben-Zuteilung).
- [x] **@-Erwähnungen** in Kommentaren/Vermerken + Benachrichtigung.
- [x] **News/Schwarzes Brett** + **Behörden-Broadcast** (gezielte Rundnachricht der Führung an alle/eine Gruppe, optionale Quittierung).

**Abnahme:** Dashboard zeigt korrekte Live-Zahlen & Diagramme; Aufgabe zuweisen → erscheint beim Empfänger + Benachrichtigung; einer Akte folgen → Änderung erzeugt Benachrichtigung; @-Erwähnung benachrichtigt; News/Broadcast erreicht Zielgruppe.

> **Verifiziert:** Alle fünf Bausteine sind implementiert und in `Program.cs` registriert.
> Dashboard (`Components/Pages/Home.razor`, `Services/DashboardService.cs`, `Models/Dashboard/`).
> Aufgaben (`Data/Entities/Aufgaben/`, `Services/AufgabeService.cs`, `Components/Pages/Aufgaben/` inkl. Kanban + „Nur meine").
> Watchlist + Benachrichtigungen (`Data/Entities/Watchlist/`, `Data/Entities/Benachrichtigungen/`, `Services/WatchlistService.cs`, `Services/NotificationService.cs`, Glocke `Components/Layout/BenachrichtigungGlocke.razor`, Live-Updates via `NotificationBroadcaster`/`WatchlistAenderungInterceptor`).
> @-Erwähnungen (`Services/MentionService.cs`/`MentionParser.cs`, VS-gefiltert).
> Brett + Broadcast (`Data/Entities/Ankuendigungen/`, `Services/AnkuendigungService.cs`, `Components/Pages/Brett/` inkl. Zielgruppe + optionaler Quittierung).
> Migrationen: `Phase6_Benachrichtigungen`, `Phase6_Watchlist`, `Phase6_Aufgaben`, `Phase6_Ankuendigungen`.

### Phase 7 – Vorlagen, Admin, Wissensbasis & Komfort ✅ ABGESCHLOSSEN
**Ziel:** Effizienz, Datenqualität und Anpassbarkeit.
- [x] **Dok-Vorlagen/Templates** (admin-definierte Erfassungsmasken) – setzt eure „Vorgaben" um.
- [x] **Konfigurierbare Custom-Felder** je Aktentyp (Admin, ohne Code).
- [x] **Aktualitäts-Ampel + Wiedervorlage** (Ampel grün/gelb/rot je Aktentyp – Schwellwerte im Admin unter `/admin/aktualitaet`; terminierte Wiedervorlagen je Akte mit Hintergrund-Job → Benachrichtigung an Zuständigen + Follower; veraltete Akten + fällige Wiedervorlagen im Dashboard).
- [x] **Quick-Add** Schnellerfassung.
- [x] **Duplikat-Zusammenführen (Merge)** zweier Personenakten (inkl. Verknüpfungen/Doks).
- [x] **Dokumenten-/Datei-Bibliothek** (zentrale, durchsuchbare Ablage: Formulare, SOPs, Vorlagen).
- [x] **Gesetzbuch/Rechtsgrundlagen-Modul** (Paragrafen, verknüpfbar mit Fällen/Doks).
- [x] **Theming/Logo-Upload** im Admin (Farben/Wappen ohne Code ändern).
- [x] **Wartungsmodus + Ankündigungsbanner**.
- [x] **PDF-/Druck-Export** von allem (Druckansicht via Browser-Druckdialog → „Als PDF speichern").
- [x] **Basisdaten-/Lookup-Adminbereich** (Fraktionsliste, Dienstgrade, Einstufungen, Maßnahme-Ausgänge, Beziehungstypen, Geltungsbereiche, Tags, Vorlagen).

**Abnahme:** Vorlage + Custom-Feld anlegen und nutzen; alte Akte wird als „evtl. veraltet" geflaggt + Erinnerung; Quick-Add in Sekunden; zwei Doppel-Akten zusammenführen; Datei in Bibliothek ablegen & finden; Gesetz mit Fall verknüpfen; Logo/Theme im Admin ändern; Wartungsmodus testen; Akte als PDF exportieren.

> **Verifiziert (Abschluss Phase 7):**
> Quick-Add (`Components/Querschnitt/Shared/QuickAddDialog.razor`, Plus-Knopf in der Topbar) legt Person/Fraktion/Gruppe/Partei/Operation/Vorgang/Taskforce/Aufgabe mit Minimalfeldern an.
> Merge (`Services/PersonMergeService.cs`, Dialog `PersonMergeDialog` auf /personen, nur Führung) überführt sämtliche Kind-/Querschnittsdaten und parkt die Quell-Akte im Papierkorb.
> Datei-Bibliothek (`Data/Entities/Querschnitt/BibliothekDatei.cs`, Reiter „Dateien" auf /dokumente, Download `/dateien/bibliothek/{id}`).
> Gesetzbuch (`Data/Entities/Querschnitt/Gesetz.cs`, Seiten /gesetze + /gesetze/{id}, in Verknüpfungs-Engine + globaler Suche integriert).
> Theming/Logo + Wartungsmodus + Banner (`SystemEinstellung`-Tabelle, `Services/SystemEinstellungService.cs`, Admin-Seite /admin/system, Logo-Endpoint /system/logo, Gate im MainLayout).
> Druck-/PDF-Export (`DruckButton` auf allen Detailseiten + @media-print-Regeln in app.css).
> Basisdaten (/admin/basisdaten: Links auf pflegbare Stammdaten + dokumentierte Wertelisten).
> Migration: `Phase15_Phase7Abschluss` (SystemEinstellungen, Gesetze, BibliothekDateien).

### Phase 8 – Visualisierungen & erweiterte Module ✅ ABGESCHLOSSEN
**Ziel:** Die „Wow"-Features. 
- [x] **Beziehungs-/Netzwerk-Graph** (interaktiv, aus Verknüpfungen/Beziehungen) via JS-Interop.
- [x] **Beziehungs-Pfad-Suche** („wie hängen A und B zusammen?").
- [x] **Verknüpfungs-Vorschläge** (zusammenhängende Akten automatisch vorschlagen – gleiche Tags/Fraktion/Telefon).
- [x] **Zeitstrahl/Timeline je Akte** (alle Ereignisse chronologisch).
- [x] **Organigramm/Personalübersicht** (NOOSE-Struktur, TRU, Taskforce-Besetzung).
- [x] **Kalender/Termine** (Gerichtstermine, Operationen, Überwachungsfenster).
- [x] **Automatischer Bedrohungs-Score** (Personen/Fraktionen; Sortierung/Priorisierung).
- [x] **Statistik-Reports/Export** (CSV/PDF) + **automatischer Lagebericht** (geplant erzeugt + archiviert).

**Abnahme:** Graph zeigt Verbindungen; Pfadsuche findet Kette zwischen zwei Personen; Vorschläge erscheinen; Timeline korrekt; Organigramm korrekt; Kalender zeigt Termine; Score wird berechnet und ist sortierbar; Monats-Lagebericht wird automatisch erzeugt. *(Die „Karte mit Orten" wurde vom Auftraggeber gestrichen.)*

> **Block A umgesetzt (Build 0/0 verifiziert; Voll-Test durch Auftraggeber offen):**
> Phase 8 wird in 4 testbaren Blöcken geliefert; **Block A = Beziehungsgraph + Pfadsuche + Verknüpfungs-Vorschläge**.
> Backend: `Services/Graph/GraphService.cs` (Graph aus `Verknuepfung` + `PersonBeziehung`, Umkreis-/Pfad-BFS, Knoten-Auflösung mit Verschlusssachen-/Taskforce-Sichtbarkeit; Gesamtgraph auf die 250 stärksten Knoten gedeckelt mit Hinweis), `Services/Graph/VerknuepfungVorschlagService.cs` (Signale: gleiche Telefonnummer/Fraktion/Gruppe/Tag + gemeinsame Verknüpfung; schließt bereits Verknüpfte/VS aus). Modelle in `Models/Graph/`.
> Frontend: Seite `/graph` (`Components/Pages/Graph/Beziehungsgraph.razor`) mit vis-network (self-hosted unter `wwwroot/lib/vis-network/`, guarded Lazy-Loader `wwwroot/js/graph.js?v=1`): Fokus-/Gesamtmodus, Tiefe, Typ-/Art-Filter, Foto-Knoten, Einstufungs-Randfarbe (rot = staatsgefährdend), Vollbild/PNG-Export, Doppelklick → Akte. Pfadsuche zeichnet die gefundene Kette. Wiederverwendbarer `GraphAktePicker`. Vorschlags-Panel auf der Personen-Akte (Reiter „Verknüpfungen") mit 1-Klick-Verknüpfen; „Im Beziehungsgraph anzeigen" auf jeder Akte. Nav-Eintrag „Beziehungsgraph" aktiviert.
> **Keine Migration** (rein lesend). Offen: Blöcke **B** (Zeitstrahl/Organigramm), **C** (Karte/Kalender), **D** (Bedrohungs-Score/Statistik-Reports/Lagebericht).
>
> **Block B – Teil 1 umgesetzt (Build 0/0 verifiziert; Voll-Test durch Auftraggeber offen):** vereinheitlichter **Zeitstrahl** je Akte.
> Der bisherige technische „Historie"-Reiter (reines Audit-Log) wurde auf allen 8 Detailseiten (Person/Fraktion/Personengruppe/Partei/Operation/Vorgang/Taskforce/Aufgabe) durch einen Reiter **„Zeitstrahl"** ersetzt, der Audit-Ereignisse (Anlage/Änderung/Mitgliedschaft/Zuteilung – inkl. Feldänderungen) mit semantischen Domänen-Ereignissen zusammenführt: Einstufungs-Verlauf, Kommentare, Quellen, Wiedervorlagen, Verknüpfungen (mit aufgelöster, sichtbarkeitsgeprüfter Gegenseite) sowie Person-spezifisch Observationen/Fotos/Beziehungen und Fraktions-Aktivitäten. Chronologisch absteigend, mit **Kategorie-Filter-Chips**.
> Backend: `Services/ZeitstrahlService.cs` (rein lesend, `IDbContextFactory`, sequenzielle flache Pomelo-Queries; Sichtbarkeits-Gate = Detailseite, Aufgaben zusätzlich über `AufgabeSichtbarkeit`). Modelle in `Models/Zeitstrahl/`. Generisches `Components/Querschnitt/Shared/ZeitstrahlPanel.razor` (Params `EntitaetTyp`/`EntitaetId`/`User`). `?tab=historie`-Deeplinks bleiben gültig (Slug unverändert). **Keine Migration.**
> Die alten `*HistorieTimeline.razor` + `GetHistorieAsync`-Dienstmethoden sind damit ungenutzt (Cleanup später).
>
> **Block B – Teil 2 umgesetzt (Build 0/0 verifiziert; Voll-Test durch Auftraggeber offen):** Seite **`/organigramm`** (Personalübersicht).
> Für ALLE aktiven Agenten: die Dienstgrad-Hierarchie als **CSS-Org-Chart** (Wurzel „N.O.O.S.E." → Ebenen Director→Junior, Boxen + Verbindungslinien, horizontal scrollbar/druckbar; scoped `Organigramm.razor.css`), darunter TRU-Block und Taskforce-Besetzungs-Karten (Leitung hervorgehoben). Klarname nur für Führung; der Taskforce-Abschnitt respektiert die Taskforce-Sichtbarkeit (Nicht-Führung sieht nur zugeteilte, genehmigte Taskforces); RP-unsichtbare Teamleitung ist überall ausgeblendet; Avatar = Codename-Initialen (keine externen Bilder). Backend: `Services/OrganigrammService.cs` (rein lesend, 3 flache Queries, kein N+1). Modelle in `Models/Organigramm/`. Wiederverwendbare `Components/Pages/Organigramm/Shared/AgentKachel.razor`. Nav-Eintrag „Organigramm" aktiviert. **Keine Migration.**
> Damit ist **Block B vollständig**. Offen: Blöcke **C** (Karte/Kalender), **D** (Bedrohungs-Score/Statistik-Reports/Lagebericht).
>
> **Block C umgesetzt (Build 0/0 verifiziert; Voll-Test durch Auftraggeber offen):** Seite **`/kalender`** + eigene **Termin-Akte**.
> Die **Karte mit Orten wurde vom Auftraggeber gestrichen** (zurückgestellt). Geliefert wurde der **Kalender**: ein self-gehostetes **FullCalendar** (v6 Global-Build unter `wwwroot/lib/fullcalendar/` + deutsche Locale; guarded Lazy-Loader `wwwroot/js/kalender.js?v=1` nach dem Graph-Muster, Monat/Woche/Liste, Klick → Akte). Er **aggregiert rein lesend** (`Services/KalenderService.cs`, sequenzielle flache Pomelo-Queries, je Fenster gedeckelt): eigene **Termine** + Operationen (Beginn/Ende) + Überwachungsfenster (Observationen) + fällige Aufgaben + offene Wiedervorlagen (Eltern sichtbarkeitsgeprüft aufgelöst) + Fraktions-Aktivitäten. Jede Quelle behält ihre kanonische Sichtbarkeit (Verschlusssache bzw. Aufgaben-/Termin-„Eingeschränkt"); farbcodierte Legende; abgesagte/verschobene Termine durchgestrichen.
> Neue **Termin-Akte** (CRUD, Papierkorb, Verknüpfungen, Kommentare/Quellen/Zeitstrahl, Teilnehmer-Zuteilung) – Sichtbarkeit **wie eine Aufgabe**: Kippschalter **`IstEingeschraenkt`** (nur Ersteller + zugeteilte Teilnehmer + Aufsicht; sonst alle), zentral über `Services/TerminSichtbarkeit.cs`. **Kein** Verschlusssache-/Einstufungs-Konzept. Backend `Services/TerminService.cs`, Entities `Data/Entities/Termine/` (`Termin` + `TerminZuweisung`), Modelle `Models/Termine/` + `Models/Kalender/`, Seiten `Components/Pages/Kalender/` (Kalender/Detail/Neu/Bearbeiten/Papierkorb + Shared). In `AktenReferenz`/`Sichtbarkeit`/`ZeitstrahlService`/`SuchNavigation` integriert (Verknüpfungen/@-Mentions/Zeitstrahl). Nav-Eintrag „Kalender" aktiviert. **Migration `Phase17_Termine`** (Tabellen `Termine` + `TerminZuweisungen`; greift per Startup-Auto-Migrate).
> Offen: nur noch Block **D** (Bedrohungs-Score/Statistik-Reports/Lagebericht).
>
> **Block D – Bedrohungs-Score umgesetzt** (Fraktion + Person, „EHK-Score", admin-konfigurierbar – Details in `AlgoPlan.md` + Memory).
>
> **Block D – Statistik-Reports/Export = Schritt 1 umgesetzt (Build 0/0 verifiziert; Voll-Test durch Auftraggeber offen):**
> Neue Seite **`/statistik`** („Statistik & Auswertungen", für alle aktiven Agenten; alle Zahlen aus Sicht des Aufrufers **VS-gefiltert**). `Services/Statistik/StatistikService.cs` aggregiert rein lesend (Muster `DashboardService`, reuse `GetKennzahlenAsync`): Verteilungen (Personen nach Einstufung/Gefährdung/Lebensstatus, Fraktionen nach Gefährdung, Maßnahme-Ausgänge, Vorgänge nach Status) als Donuts (`VerteilungChart`), ein **12-Monats-Verlauf** (Maßnahmen + Neuzugänge) als Linien-Diagramm (`ZeitverlaufChart`, MudChart) und die Top-Listen der gefährlichsten Personen/Fraktionen. **CSV-Export** (UTF-8-BOM + `;` für DE-Excel) über Minimal-API `/statistik/export/{verteilungen|personen|fraktionen}.csv`; **PDF** über Browser-Druck (`/statistik/druck`, `DruckRahmen`). Dazu Dashboard-Kachel „Personen nach Gefährdung". **Keine Migration** (rein lesend).
> **Block D – automatischer Lagebericht = Schritt 2 umgesetzt (Build 0/0; Migration `Phase21_Lagebericht`; end-to-end verifiziert):**
> Neue Akte **`Lagebericht`** (`Data/Entities/Querschnitt/Lagebericht.cs`, IAuditable+ISoftDelete) speichert je Monat einen eingefrorenen `StatistikReport`-Schnappschuss als JSON. `Services/Statistik/LageberichtService.cs` erzeugt ihn (über `IStatistikService` mit **voller** Lage inkl. VS-Aggregaten – daher **Führung vorbehalten**), archiviert ihn und benachrichtigt die Führung (Glocke). Der Hintergrund-Dienst `Infrastructure/Statistik/LageberichtDienst.cs` (`BackgroundService`, täglich tickend) erzeugt automatisch den Bericht des abgeschlossenen Vormonats, sobald er fehlt – die Existenz des Berichts ist der Merker. Archiv-Seiten **`/lageberichte`** (Liste, Führung; „diesen Monat erzeugen"-Knopf) und **`/lageberichte/{id}`** (Dokumentansicht im `DruckRahmen` → PDF). Damit ist **Phase 8 / Block D vollständig** (offen nur die Kalibrierung der Scores am Echtbestand).

### Phase 9 – Partner-Zugriff (DoJ / LSPD / LSMD)
**Ziel:** Kontrollierter Lesezugriff für Partnerbehörden.
- [ ] Partner-Rollen + **„für Partner freigegeben"-Flag** je Akte (Felder früh vorbereitet).
- [ ] Partner-Login (Discord) → **eingeschränkte, schreibgeschützte** Ansichten (nur Freigegebenes), eigene Navigation.
- [ ] Zugriffsprotokoll greift auch hier.

**Abnahme:** Partner loggt sich ein, sieht ausschließlich freigegebene Inhalte (nur lesen); NOOSE kann Freigabe pro Akte umschalten.

### Phase 10 – Deployment, Betrieb & Härtung
**Ziel:** Stabil und sicher auf dem eigenen VPS.
- [X] VPS-Setup (Reverse-Proxy, .NET-Runtime), **HTTPS** (Zertifikat), MariaDB-Prod-Konfig.
- [X] Migrations & Admin-Seed beim Deploy.
- [ ] **Backups** (DB-Dump + Upload-Dateien) + dokumentierte Wiederherstellung.
- [ ] Härtung: HSTS, Login-Rate-Limit, Upload-Validierung, Logging/Monitoring.
- [ ] **Betriebs-/Onboarding-Doku** (README: Agenten anlegen, Rechte, Backups).

**Abnahme:** Über die Domain per HTTPS erreichbar; Backup läuft; Test-Restore erfolgreich.

---

## 10. Annahmen & offene Punkte

Sinnvolle Standardannahmen (jederzeit korrigierbar):
- UI-Sprache **Deutsch**.
- „Grund" und „Erhaltene Informationen" = **Freitext**.
- Fraktionszugehörigkeit wird **aus der Fraktionsliste** gewählt (mit Historie).
- Maßnahme-Ausgang „erschossen"/„Spritze" setzt Personenstatus auf **verstorben**.
- Uploads sind **nur für eingeloggte Nutzer** abrufbar.

Noch zu klären (kann während Phase 2/4 beantwortet werden):
- Genaue **Felddetails** für Steckbrief & Fraktions-Bestände.
- Gibt es ein **NOOSE-Wappen/Logo** in guter Auflösung?
- Exakte **Domain** und Server-Umgebung (Linux vs. Windows) für Phase 10.

---

## 11. Glossar

- **NOOSE** – National Office of Security Enforcement (Nachrichtendienst).
- **Dok** – Personen-Dokument: Protokoll eines Verhörs / einer Maßnahme.
- **Einstufung** – Prüffall → Verdachtsfall → Gesichert staatsgefährdend.
- **Verschlusssache** – Akte mit eingeschränkter Sichtbarkeit (nur Führung/zugewiesene Agenten).
- **Fall/Vorgang** – übergeordnete Akte, die Personen/Doks/Operationen/Observationen bündelt.
- **TRU** – Tactical Response Unit (rangübergreifende Einheit).
- **CID** – ermittelnde/operative Leitung; **DoJ** – Department of Justice; **LSPD/LSMD** – Polizei/Medizin.
- **Partner** – DoJ/LSPD/LSMD mit Lesezugriff auf Freigegebenes.

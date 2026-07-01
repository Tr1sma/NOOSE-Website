# Design: Versionsnummer auf System-Status-Seite

> Stand: 2026-07-01 · Sprache: Deutsch

## 1. Ziel & Kontext

Auf der bestehenden System-Status-Seite (`/status`, `Components/Pages/Status.razor`) soll eine
Versionsnummer angezeigt werden, damit nach einem Deploy sichtbar ist, ob der neue Stand tatsächlich
läuft.

## 2. Festgelegte Entscheidungen

| Thema | Entscheidung |
|---|---|
| Format | Semantische Version `1.0.<Build>` (Major.Minor fix, Patch = Build-Zähler) |
| Erhöhung | Automatisch bei **jedem echten Build** (`dotnet build`/`watch`/`publish`), nicht nur bei Deploys |
| Zähler-Persistenz | Datei `NOOSE-Website/BuildNumber.txt`, **in Git eingecheckt** |
| Speicherort Version | `.csproj` `<Version>`-Property, zur Laufzeit über `AssemblyInformationalVersionAttribute` ausgelesen |
| Anzeige | Neue Zeile "Version" im "Anwendung"-Panel auf `/status`, oberste Zeile vor "Umgebung" |

## 3. Architektur-Überblick

Ein MSBuild-`Target` in `NOOSE-Website.csproj` läuft vor der Assembly-Info-Generierung
(`BeforeTargets="GenerateAssemblyInfo"`):

1. Liest die aktuelle Zahl aus `BuildNumber.txt` (Default `0`, falls Datei fehlt/leer).
2. Erhöht sie um 1.
3. Schreibt die neue Zahl zurück in `BuildNumber.txt`.
4. Setzt `$(Version)` = `1.0.<neue Zahl>`.

**Guard gegen IDE-Design-Time-Builds:** `Condition="'$(DesignTimeBuild)' != 'true' and '$(BuildingProject)' == 'true'"`.
IntelliSense-Auswertungen in IDEs lösen sonst bei jedem Tastendruck einen MSBuild-Durchlauf aus, ohne
dass tatsächlich kompiliert wird — das würde den Zähler unkontrolliert hochtreiben. Echte
`dotnet build`/`dotnet watch`/`dotnet publish`-Aufrufe zählen weiterhin.

Auf `/status` wird die Version zur Laufzeit gelesen:

```csharp
Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
```

Das entspricht exakt dem `.csproj`-Wert, ohne die 4-Segment-Padding-Eigenheiten von
`AssemblyVersion`/`FileVersion`.

## 4. Konsequenz für Git-Workflow

Da `BuildNumber.txt` bei jedem echten Build verändert wird und eingecheckt ist, taucht sie nach jedem
lokalen Build in `git status`. Sie muss zusammen mit inhaltlichen Änderungen committed werden, sonst
bleibt der lokale Zählerstand hinter dem committeten Stand zurück (kein funktionaler Schaden, nur
Diff-Rauschen).

## 5. Betroffene Dateien

- `NOOSE-Website/NOOSE-Website.csproj` — neues `<Target>`, `BuildNumber.txt` als Startwert.
- `NOOSE-Website/BuildNumber.txt` — neu, Startwert `0`.
- `NOOSE-Website/Components/Pages/Status.razor` — neue "Version"-Zeile im "Anwendung"-Panel.

## 6. Out of Scope

- Kein Major/Minor-Bump-Mechanismus (bleibt manuell, falls je gewünscht).
- Kein Anzeigen der Version auf anderen Seiten (nur `/status`, admin-only).
- Kein Git-Commit-Hash in der Version (reiner Build-Zähler reicht für den Zweck "lief der neue Stand?").

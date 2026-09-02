# UI / Blazor — Detailregeln

> **Lies das, bevor du eine Route entfernst oder verschiebst (V1.5-Sammelseiten,
> `LegacyRouteRedirect`) oder `Components/Layout/NavMenu.razor` anfasst.**
> Die Routen-Tabelle der Sammelseiten und die Bausteine-Tabelle stehen in `CLAUDE.md`.

## V1.5-Sammelseiten: Legacy-Routen, `MergedPageSections`, strengere Abschnitte

- **Alte Routen leben weiter** in **einer** Shell: `Components/Common/Navigation/LegacyRouteRedirect.razor`
  trägt alle ~38 entfernten `@page`-Direktiven und schlägt das Ziel in `Navigation/LegacyRoutes.cs` nach.
  Abschnitte, die eine **überlebende** Seite verlassen, brauchen zusätzlich `LegacyRoutes.MovedSettingsTab`
  (`Target()` matcht nur ganze entfernte Routen) — so leitet `/einstellungen?tab=protokoll` auf `/nachweis`.
  **Beim Entfernen einer Route: `@page` erst in die Shell eintragen, wenn die alte Datei im selben Schritt
  gelöscht wird** — zwei Komponenten auf derselben Route werfen erst zur *Laufzeit*, nicht beim Kompilieren.
- `Navigation/MergedPageSections.cs` hält die Slug-Listen der Sammelseiten; `LegacyRoutesTests` prüft
  darüber, dass jedes Redirect-Ziel einen existierenden `?tab=`-Abschnitt benennt.
- **Strengere Abschnitte** werden in `<AuthorizeView>` gewickelt. Weil `RecordSection.OnInitialized` die
  Registrierung auslöst, registriert sich ein nicht gerenderter Abschnitt gar nicht erst — der Rail-Button
  fehlt, und ein manipuliertes `?tab=` fällt auf den ersten *erlaubten* Abschnitt zurück.

## Drawer: Icon-Leiste + Panel

`Components/Layout/NavMenu.razor` ist zweispaltig: links eine 64-px-Leiste mit Bereichs-Icons, rechts die
Einträge genau eines Bereichs. Ein Icon-Klick **navigiert nicht**, er wechselt nur das Panel.

- **Zwei orthogonale Achsen:** `NavSection` trägt die *Policy* (`NavSectionPolicy.For`), `NavArea` die
  *Darstellung*. Sie divergieren bewusst (`organigramm` ist `Analyse`/`Dienststelle`, `bewerbungen` ist
  `VerwaltungBewerbungen`/`Verwaltung`) — nicht zusammenlegen.
- Das rechte Panel **muss** in `<MudNavMenu Color="Color.Primary" Bordered="true">` bleiben: MudBlazor
  scopet die farbige Aktiv-Markierung auf `.mud-navmenu-primary .mud-nav-link.active`.
- Die Leiste ist von Hand gebaut (Flex + `MudIconButton`), weil MudBlazor kein Nav-Primitive hat, das
  auswählt ohne zu navigieren. `DrawerVariant.Mini` ist **nicht** die Lösung.
- Blazors `NavLink` setzt **nie** `aria-current` — das wird über `NavCatalog.ByRoute` selbst berechnet.
- **Bereichs-Sichtbarkeit** kommt aus einem Snapshot: `IAuthorizationService` einmal je *distinkter Policy*
  in `OnInitializedAsync` (nicht je Eintrag). Ein Bereichs-Icon erscheint nur, wenn mindestens ein Eintrag
  erlaubt ist. Der Snapshot darf veralten — Rang-/Flag-Änderungen rotieren den `SecurityStamp` und erzwingen
  ohnehin ein Re-Login.
- **`NavPreferences.CollapsedGroups` ist tot, bleibt aber als Property stehen**: der Service macht
  Read-Modify-Write über den ganzen JSON-Blob, ein Entfernen würde die Daten beim nächsten Speichern
  stillschweigend löschen. Aktiv ist stattdessen `LastArea`.
- Favoriten auf V1.5-verschmolzene Seiten werden über `LegacyRoutes.AliasKey` auf ihr neues Ziel gemappt.

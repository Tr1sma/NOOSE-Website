# CachePlan.md — Caching & Ladezeiten

Arbeitsdokument für das seitenweite Caching-System. Wird während der Umsetzung fortgeschrieben;
gemessene Zahlen landen unten in **Messungen**.

## Warum

DB-Zugriffe minimieren, Ladezeiten senken. Drei Befunde bestimmen den Zuschnitt:

1. **Es gibt bereits 13 Cache-Stellen** in zwei nicht abgestimmten Idiomen, mit handgeschriebener
   Invalidierung an 12 Stellen — zwei davon mit echten Bugs. „Vollständig" heißt deshalb zuerst:
   den Bestand unter *eine* Schicht bringen, nicht eine dritte danebenstellen.
2. **Die größten Ladezeit-Gewinne sind kein Caching.** Das Dashboard ruft sieben Dienste seriell
   (`Home.razor:291-300`). Die Befehlspalette feuert pro Tastendruck 11 sequenzielle
   Provider-Abfragen (`CommandPalette.razor:21-25`, kein `DebounceInterval`). Drei Listenseiten laden
   Volltabellen und filtern im Speicher.
3. **Caching ist hier überdurchschnittlich gefährlich.** Die Sicherheit kommt aus Lese-Filterung pro
   Betrachter, nicht aus Endpunkt-Gates. Ein Schlüssel, dem eine Sichtbarkeits-Achse fehlt, ist ein
   stilles Leck — und `ViewerScope` enthält weder `PartnerRank` noch `IsDemo`.

## Entscheidungen

| Achse | Entscheidung |
|---|---|
| Umfang | Cache **+** Query-Fixes. Kein Listen-Umbau. |
| Frische | **Sofort** — Tag-Invalidierung beim Schreiben, TTL nur Rückfallnetz. |
| Risiko | **Konservativ** — Listen, Partner-Pfade, Chronik/Papierkorb, gerenderte Klarnamen und Labels bleiben ungecacht. |
| Altbestand | **Alle 13 migrieren**, danach Wächter „kein `IMemoryCache` außerhalb der Cache-Schicht". |
| Wächter | **Voll** — 5 Guard-Tests. |
| Lieferung | **In Wellen**, jede einzeln baubar und testbar. |

## Leitprinzipien

1. **TTL ist der Korrektheits-Boden, Tags sind die Verbesserung.** Jeder Eintrag hat eine erzwungene,
   begrenzte TTL. Ein vergessener Tag degradiert zu „höchstens N Sekunden alt", nie zu „für immer
   falsch". Über-Invalidierung ist immer sicher.
2. **Ein Schlüssel ist `(was, für wen)`.** Ein `CacheKey` lässt sich ohne `ScopeCacheKey` nicht bauen.
   Wer den Betrachter weglässt, tut das als benannten Eintrag mit Begründung.
3. **Entweder vor der Sichtbarkeitsfilterung cachen (globaler Rohsatz) oder danach mit vollständigem
   Betrachter-Fingerabdruck. Nie dazwischen.** Vorbild `LeadService`: ein Eintrag, N korrekte Antworten.
4. **Akten-Caches auf minimalem Privileg erzeugen** (`DossierScope.ForRecord`), pro Betrachter beim
   Lesen anreichern.
5. **Ein `catch`-Ergebnis wird nie gecacht** (`SystemSettingService:66-69`) — sonst pinnt ein
   DB-Aussetzer sichere Vorgabewerte für die ganze TTL.
6. **Ein Bearbeiten-Formular liest nie aus dem Cache** (`ThreatScoreConfigService.GetEditableAsync`),
   sonst schreibt der Admin einen alten Wert zurück.

## Wellen

| # | Welle | Art | Zustand |
|---|---|---|---|
| 0 | Messinfrastruktur (`QueryCounterInterceptor`, Laufzeit-Karte, Cache leeren) | additiv | in Arbeit |
| 1 | Demo-Schreibrecht schließen (`Permission.RequireWriteAccess` → `!MayWrite()`) | Sicherheit | offen |
| 2 | Cache-Kern `Infrastructure/Caching/` + 5 Wächter + 5. Interceptor | Kern | offen |
| 3 | 13 Altbestand-Caches migrieren + LeadService-Leck + Statistik-Achsen + Label-Staleness | Migration | offen |
| 4 | ~82 Bulk-Write-Stellen invalidieren bzw. allowlisten | Invalidierung | offen |
| 5 | Dashboard `Task.WhenAll` + `MaximumPoolSize=64` | Fix | offen |
| 6 | Befehlspalette Debounce, `AsSplitQuery`, gezieltes `AsNoTracking` | Fix | offen |
| 7 | Globale Referenz-Caches (Stichworte, Agentenverzeichnis, Wertelisten, Graph-Kanten) | Cache | offen |
| 8 | N+1 auflösen (`Visibility.VisibleIdsAsync`, `ResolveManyAsync`) | Fix | offen |
| 9 | Doku und Abschlussmessung | Doku | offen |

## Wird bewusst NICHT gecacht

| Nicht cachen | Grund |
|---|---|
| Listenseiten | Acht Sichtbarkeits-Helfer enthalten `MeId`; ein Rollen-Schlüssel wäre falsch, ein Benutzer-Schlüssel hätte fast keine Trefferquote. Und es sind die Seiten, die man bearbeitet und sofort wieder öffnet |
| Partner-Lesepfade | Filter ist pro **Konto** (`PartnerShares`, selbst verfasste Dokumente); einstellige Kontozahl, kein Perf-Fall |
| Ergebnis von `Visibility.IsRecordVisibleAsync` | Fail-open-Schwanz `_ => true`: ein gecachtes `true` für einen unbekannten Typ hat keinen Invalidierungs-Tag |
| Alles über den Bool-Shim `Visibility.cs:237-241` | Erzeugt für denselben Menschen einen zweiten Fingerabdruck |
| Gerenderte Klarnamen | `AgentNameDisplay.Pick` backt den Klarnamen ein; `MayRealName` ist nicht rekonstruierbar. Paare cachen, beim Rendern wählen |
| Gerenderte Enum-/Werteliste-Labels | `EnumLabelText` ist prozess-global und wird ohne Cache-Berührung getauscht |
| Alles `now`-Abhängige | Agenda-Freigabe (+2 h), 20-Minuten-Tot-Fenster, `FactionRecency.Reference` |
| Abwesenheits-Detailfelder | `MayReadPrivateFields` ist pro Zeile, nicht pro Betrachter |
| Chronik und Papierkorb | `IgnoreQueryFilters`-Pfade; ihr Zweck ist der aktuelle Stand |
| Nachweis und Zugriffe | Die Beweis-Oberfläche |
| Suchergebnisse und Schnellsuche | Unbegrenzter Schlüsselraum; `SearchResults.Incomplete` würde die Lücke mitcachen |
| NOOSEI-Unterhaltungen und Kontingente | Besitzer-privat, eigener `RechteStempel`-Mechanismus, geldnah |
| `CurrentUserInfo` | Wird pro `SaveChanges` frisch gebaut; beide Interceptoren hängen daran |
| Ergebnis eines `catch`-Blocks | Siehe Leitprinzip 5 |
| `AgentSelection`-Rosters | Drei Prädikate über ~30 Zeilen; ein geteilter Cache lässt sie zusammendriften |

## Messprotokoll

Als Admin einloggen, `/einstellungen?tab=status` in Tab A offen halten.

1. **Zähler zurücksetzen** drücken.
2. In Tab B die Zielroute aufrufen, warten bis der Ladebalken weg ist.
3. Tab A neu laden, **Kommandos** und **Ø ms** notieren.
4. Dreimal wiederholen, **Median** nehmen. Die Kommandozahl ist deterministisch und ist die
   eigentliche Zahl; die Wanduhr ist nur Plausibilitätsprüfung.

Routen: `/dashboard` kalt · `/dashboard` zweiter Besuch · Strg+K mit 12 Zeichen · `/fraktionen` ·
`/personen` · `/graph` + eine Wegsuche · `/nachweis` mit einem Filterwechsel · `/suche` zweiwortig
(**Kontrolle — darf sich nicht ändern**).

Zusammenführen nur, wenn (a) alle Tests grün, (b) die Zielroute die behauptete Kommandozahl erreicht,
(c) **keine andere Route regressiert hat** — (c) fängt eine übereifrige Tag-Invalidierung.

## Messungen

| Route | Basis (Kommandos / Ø ms) | nach Welle | Stand |
|---|---|---|---|
| `/dashboard` kalt | — | — | ausstehend |
| `/dashboard` zweiter Besuch | — | — | ausstehend |
| Strg+K, 12 Zeichen | — | — | ausstehend |
| `/fraktionen` | — | — | ausstehend |
| `/personen` | — | — | ausstehend |
| `/graph` + Wegsuche | — | — | ausstehend |
| `/nachweis` + Filterwechsel | — | — | ausstehend |
| `/suche` zweiwortig (Kontrolle) | — | — | ausstehend |

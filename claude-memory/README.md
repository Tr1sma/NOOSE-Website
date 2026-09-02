# claude-memory

Bereichswissen für die NOOSE-Website, ausgelagert aus `CLAUDE.md`.

`CLAUDE.md` wird in **jeder** Session geladen und hält deshalb nur, was für **jede** Änderung gilt.
Die Dateien hier werden **auf Zuruf** gelesen — bevor im jeweiligen Bereich etwas geändert wird.

Sie sind keine Zusammenfassungen: sie enthalten die Begründung hinter jeder Regel („warum es so ist,
und was schiefging, als es anders war"). Genau das verhindert, dass eine Regel beim nächsten Umbau
als vermeintliche Schlamperei wegoptimiert wird.

| Datei | Inhalt | Lies sie, bevor du … |
|---|---|---|
| [`oeffentlich-grundlagen.md`](oeffentlich-grundlagen.md) | Bürgerkonto, Modul-Schalter, Not-Aus, `PublicVisibility`, `PublicRoutes`, redaktionelle Seiten, Migrationsnamen | **irgendetwas** im öffentlichen Bereich anfasst |
| [`oeffentlich-fahndung.md`](oeffentlich-fahndung.md) | Phase 4, 5, 13a, 13b — Ausschreibung, Board, Archiv, Poster, Sachfahndung, Einspruch | an `PublicWantedService`, `/gesucht`, `/gefasst` oder dem Foto-Endpoint arbeitest |
| [`oeffentlich-geld.md`](oeffentlich-geld.md) | Phase 6, 9 — Kopfgeld, Belohnung, Kassenbuchung, Beleg | einen Pfad anfasst, der Geld bucht |
| [`oeffentlich-buergerkontakt.md`](oeffentlich-buergerkontakt.md) | Phase 7, 8a, 8b, 10, 11 — Hinweise, Triage, Übernahme, Ticket-Chat, Vorlagen | etwas anfasst, das einen Bürger adressiert — **inkl. Anonymitätszusage** |
| [`oeffentlich-redaktion.md`](oeffentlich-redaktion.md) | Phase 12, 14a–c, 15a–b — Organisationen, Presse, Warnungen, Recht, Berichte, Gefahrenlage, Zahlen | redaktionelle Außendarstellung änderst |
| [`oeffentlich-suche.md`](oeffentlich-suche.md) | Phase 16 — öffentliche Suche, NOOSEI-Anbindung nach außen, interne KPIs | einen öffentlichen Suchprovider oder `PublicSearchService` anfasst |
| [`noosei.md`](noosei.md) | Gateway, Werkzeuge, Sichtbarkeits-Gates, Kontingente, Kosten-Sichtbarkeit, Editor-Korrektur | an der KI-Integration arbeitest |
| [`services-details.md`](services-details.md) | `AgentSelection`-Ausnahmen, Bestenlisten-Rangboden, die sechs Suchregeln | `AgentSelection` „aufräumen" willst oder eine Suchkategorie hinzufügst |
| [`ui-details.md`](ui-details.md) | Legacy-Routen der V1.5-Sammelseiten, Drawer-Mechanik | eine Route entfernst/verschiebst oder `NavMenu.razor` anfasst |

## Pflege

- Eine Regel gehört **hierher**, wenn sie nur in einem Bereich gilt; nach `CLAUDE.md`, wenn sie überall gilt.
- Wächst eine Datei über ~500 Zeilen, teile sie entlang der Bereichsgrenze, nicht entlang der Phasen.
- Die Phasen-Überschriften stammen aus `PublicPlan.md` und bleiben als Fundstelle stehen — der Text
  darunter beschreibt den **gebauten** Stand, nicht die Planung.

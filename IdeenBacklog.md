# IdeenBacklog

Ergebnis einer strukturierten Ideensammlung fuer die NOOSE-Website (11.09.2026). Fuenf unabhaengige
Blickwinkel haben die Codebase erkundet und Vorschlaege gemacht, drei unabhaengige Juroren haben jeden
davon bewertet, und **jeder einzelne Vorschlag wurde danach angenommen, vorgemerkt oder abgelehnt**.

Diese Datei ist die Roadmap nach Handbuch und Changelog. Sie existiert, damit die Analyse nicht
wiederholt werden muss: zu jedem Vorschlag steht hier, was er tut, welches Problem er loest, **an
welchen Dateien er ansetzt** und was die Jury dazu gesagt hat.

> Die Pfade in den "Setzt an bei"-Zeilen stammen aus der Erkundung vom 11.09.2026. Pruefe einen
> Pfad, bevor du dich darauf verlaesst - die Codebase bewegt sich.

## Stand

| | Anzahl |
|---|---|
| Vorschlaege insgesamt | 66 |
| **Angenommen** - beschlossene Bauliste | **28** |
| Vorgemerkt - gute Idee, spaeter entscheiden | 31 |
| Abgelehnt - Frage ist beantwortet | 3 |
| Zusammengefuehrt - Doppler zweier Blickwinkel | 4 |

Der **Jury-Schnitt** ist der Mittelwert aus drei Bewertungen von 0 bis 10: Alltagsnutzen,
Architektur-Passung und Verhaeltnis aus Aufwand und Dauerlast. Er ist ein Hinweis, keine Rangfolge -
die Reihenfolge unten wurde entschieden, nicht errechnet.

---

## 1. Angenommen (28) - in der beschlossenen Reihenfolge

Erst die Aussenwirkung, dann die Benutzbarkeit, dann die Aufmerksamkeitskanaele, dann neuer Inhalt,
zuletzt die Architektur-Grenze.

### Gruppe A - Aussenwirkung

*Fast geschenkt, wirkt ab dem naechsten Deploy.*

#### 1. Link-Vorschau fuer oeffentliche Seiten

**Aufwand:** klein | **Jury-Schnitt:** 8.7 | **Blickwinkel:** Reichweite und Aussenwelt

> **Umgesetzt am 14.09.2026.** Eine `<LinkPreview>`-Zeile je Seite
> (`Components/Common/Shared/LinkPreview.razor`), Text und Adresse aus `Services/Public/LinkPreviewText.cs`,
> Bild ist das veroeffentlichte Fahndungsfoto oder die Behoerdenmarke. Die Regeln dahinter stehen in
> [`claude-memory/oeffentlich-grundlagen.md`](claude-memory/oeffentlich-grundlagen.md) unter „Link-Vorschau".

**Was es tut.** Oeffentliche Seiten bekommen Vorschau-Angaben (Titel, Kurztext, Bild), damit ein Link im Discord als Karte mit Foto erscheint statt als nackte Adresse.

**Warum.** Die Seite postet ihre Links selbst ins Discord - und sie sehen dort aus wie Spam. Ein Fahndungslink ohne Bild und ohne Namen wird nicht angeklickt, obwohl genau das der Zweck der Ausschreibung ist.

**Setzt an bei.** Der Kopfbereich hat heute nur Zeichensatz, Viewport und Favicon - keine einzige Vorschau-Angabe (NOOSE-Website/Components/App.razor Zeilen 4-18); das <HeadContent>-Muster wird schon fuer noindex genutzt (NOOSE-Website/Components/Pages/Public/WantedProfile.razor:16). Das Vorschaubild existiert bereits als anonym abrufbare Datei: NOOSE-Website/Components/Public/PublicWantedFileEndpointRouteBuilderExtensions.cs (/gesucht/{Aktenzeichen}/foto). Inhalte liefern NOOSE-Website/Services/Public/PublicWantedService.cs, PressReleaseService.cs und PublicReportService.cs; die Basis-Adresse liegt schon als SystemSettingKeys.SiteBaseUrl vor (NOOSE-Website/Models/Common/SystemConfiguration.cs).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (8/10):* Ein halber Tag Arbeit, und jeder einzelne Link, den die Seite ohnehin schon taeglich ins Discord postet, sieht ab sofort nach Behoerde statt nach Spam aus — das beste Aufwand-Wirkungs-Verhaeltnis der Liste.
- *Architektur-Passung (9/10):* Die öffentlichen Seiten rendern statisch ohne Circuit, PublicRoutes ist bereits die einzige Wahrheit darüber, was nach außen gehört, und als og:image steht der anonyme Fahndungsfoto-Endpoint schon bereit — es sind Meta-Tags in einer HeadContent-Sektion.
- *Aufwand und Dauerlast (9/10):* Ein paar Meta-Angaben auf statisch gerenderten Seiten mit ohnehin öffentlichen Bildern — einmal gesetzt, nie wieder angefasst.

</details>

### Gruppe B - Benutzbarkeit im Alltag

*Macht die taegliche Arbeit schneller. Ueberwiegend neue Formen fuer Daten, die schon da sind.*

#### 2. Score-Verlauf als Mini-Kurve in Listen

**Aufwand:** klein | **Jury-Schnitt:** 8.3 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

> **Umgesetzt am 17.09.2026.** Verdrahtet in allen vier genannten Ansichten – `PeopleList`, `FactionsList`,
> `HazardList` (Lagezentrum) und `MyBeobachteten`; je Seite **eine** gebündelte Abfrage
> (`GetSparklinesAsync`), nicht eine je Zeile. Die Beschriftung kommt aus
> `Services/Threat/ThreatTrendText.cs` und nennt Anfang, Ende und Differenz statt eines Richtungsworts –
> eine Kurve, die steigt und wieder fällt, endet dort, wo sie begann.
>
> Drei Schranken: eine Staatsfraktion trägt keinen Score und darum auch keine Kurve; in den beobachteten
> Akten bekommt nur eine Zeile eine Kurve, die der Betrachter auch öffnen darf; und **Partner bleiben dort
> ganz außen vor**, weil das zurückgelesene Zugänglichkeits-Kennzeichen nur die Einstufung wiegt und eine
> nachträglich entzogene Freigabe nicht bemerkt (die Listen haben diese Lücke nicht — ihre Zeilen kommen
> aus einer partner-gefilterten Abfrage).
>
> `ScoreTrendWiringTests` hält die Verdrahtung fest — beide Teile lagen vorher gebaut und ungenutzt herum,
> ohne dass etwas rot war.
>
> **Bekannte Kante, bewusst nicht geändert:** `GetSparklinesAsync` liest die *ganze* Verlaufshistorie der
> angefragten Akten und behält davon acht Punkte je Akte. Weil der tägliche Lauf nur bei echter Änderung
> eine Zeile schreibt, der Score aber täglich abklingt, wächst die Historie einer aktiven Akte um etwa eine
> Zeile pro Tag — bei 200 Personen und einem Jahr also rund 73.000 gelesene Zeilen für 1.600 gezeigte.
> Ein Zeitfenster würde das deckeln, aber eine Akte, die sich lange nicht bewegt hat, verlöre damit ihre
> (flache, korrekte) Kurve. Genau die Abwägung gehört entschieden, bevor jemand sie still trifft.

**Was es tut.** Jede Personen-, Fraktions- und Watchlist-Zeile bekommt eine kleine Verlaufskurve des Bedrohungs-Scores neben der Zahl, damit man sofort sieht: steigt, fällt oder liegt still.

**Warum.** Der aktuelle Score sagt nichts über die Richtung. Heute muss man jede Akte einzeln öffnen, um den Verlauf zu sehen. Sowohl die Komponente als auch die dafür gebaute Sammel-Abfrage existieren und werden von keiner einzigen Stelle benutzt – die Arbeit ist zu 90 Prozent schon getan.

**Setzt an bei.** NOOSE-Website/Components/Common/Shared/ThreatSparkline.razor (fertige Inline-SVG-Komponente, 0 Verwendungen), NOOSE-Website/Services/Threat/ThreatTrendService.cs → GetSparklinesAsync (gebündelte Abfrage, im Kommentar ausdrücklich "list sparklines", 0 Aufrufer), Einbauorte: NOOSE-Website/Components/Pages/People/PeopleList.razor, NOOSE-Website/Components/Pages/Factions/FactionsList.razor, NOOSE-Website/Components/Common/Shared/HazardList.razor (Dashboard), NOOSE-Website/Components/Pages/Watchlist/MyBeobachteten.razor

**Achtung.** Nachgemessen: `Components/Common/Shared/ThreatSparkline.razor` und `IThreatTrendService.GetSparklinesAsync` existieren beide fertig, letztere mit eigenem Test in `NOOSE-Website.Tests/Services/Integration/ThreatTrendServiceTests.cs` - und haben **null Aufrufer in der Anwendung**. Es bleibt reines Verdrahten.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Zu 90 Prozent bereits gebaut und in jeder Liste sichtbar — 'steigt oder faellt' ist echter Informationsgewinn gegenueber einer nackten Zahl, aendert aber an niemandes Handeln viel.
- *Architektur-Passung (10/10):* ThreatSparkline.razor existiert und wird von keiner Datei referenziert, IThreatTrendService.GetSparklinesAsync existiert und wird von niemandem aufgerufen — es bleibt buchstäblich das Verdrahten zweier fertiger Teile in die Listenzeilen.
- *Aufwand und Dauerlast (10/10):* Komponente und Sammel-Abfrage liegen fertig und ungenutzt herum — reines Verdrahten ohne neuen Zustand, ohne Worker, ohne jede Folgelast.

</details>

#### 3. Funkplan und Frequenzverzeichnis

**Aufwand:** klein | **Jury-Schnitt:** 8.3 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Eine Tabelle, wer auf welchem Kanal funkt: NOOSE-Kanaele (Fuehrung, TRU, je Taskforce), Partnerbehoerden und die bereits erfassten Fraktions-Frequenzen. Dazu die Rueckwaertssuche 'wem gehoert 411.7?'.

**Warum.** Faction.Funk wird seit jeher erfasst, ist aber nirgends abfragbar - eine aufgeschnappte Frequenz laesst sich also nicht zuordnen. Umgekehrt stehen die eigenen Kanaele in keiner Zeile Code; jeder Neuzugang fragt sie im Discord ab. Kleiner Aufwand, sofort im Rollenspiel benutzbar.

**Setzt an bei.** Die Fraktionsseite liegt schon vor: [Column("Funk")] Radio in NOOSE-Website/Data/Entities/Factions/Faction.cs, angezeigt in NOOSE-Website/Components/Pages/Factions/Shared/FactionForm.razor. Eigene Kanaele haengen an NOOSE-Website/Data/Entities/Taskforces/Taskforce.cs. Rueckwaertssuche als Provider neben NOOSE-Website/Services/Search/Providers/RecordSearchProviders.cs, registriert ueber NOOSE-Website/Services/Search/SearchCatalog.cs; Label/Icon ueber NOOSE-Website/Services/RecordTypeDisplay.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Winziger Aufwand auf bereits erfassten Daten, und Funk ist auf einem FiveM-Server taegliches Handwerkszeug — die Rueckwaertssuche auf eine aufgeschnappte Frequenz ist sofort im Spiel benutzbar.
- *Architektur-Passung (9/10):* Faction.Funk wird längst erfasst, die Rückwärtssuche ist eine SearchCatalog-Zeile plus Provider, und die eigenen Kanäle sind eine weitere Sektion im ohnehin 34-teiligen Einstellungen-Hub — minimale neue Fläche auf vorhandenen Daten.
- *Aufwand und Dauerlast (9/10):* Eine Tabelle plus Rückwärtssuche auf einem bereits erfassten Feld, kein Hintergrundprozess, kein Risiko, faktisch keine Dauerlast.

</details>

#### 4. Abwesenheit im Agenten-Picker anzeigen

**Aufwand:** klein | **Jury-Schnitt:** 6.7 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Wer in einer Auswahlliste steht und für den Zeitraum abgemeldet ist, bekommt einen Hinweis neben den Namen (»abgemeldet bis 14.09.«) — bei Aufgaben, Wiedervorlagen, Terminen und Zuweisungen.

**Warum.** Abmeldungen werden heute nur vom Kalender und von den Besprechungen ausgewertet. Eine Aufgabe, eine Wiedervorlage oder ein Termin lässt sich stillschweigend an jemanden hängen, der für zwei Wochen weg ist — auffallen tut das erst, wenn die Frist gerissen ist. Der Zuweisende müsste dafür vorher eine andere Seite aufmachen.

**Setzt an bei.** Die Abfrage ist fertig: NOOSE-Website/Services/AbsenceVisibility.cs (Covering(day)-Erweiterung), so schon benutzt in NOOSE-Website/Services/MeetingService.cs und NOOSE-Website/Infrastructure/Meetings/MeetingReminderWorker.cs. Die Picker sitzen in NOOSE-Website/Components/Pages/Jobs/Shared/JobForm.razor, NOOSE-Website/Components/Common/Shared/FollowupDialog.razor und NOOSE-Website/Components/Pages/Calendar/Shared/AppointmentForm.razor; die Kandidatenmenge kommt überall aus NOOSE-Website/Services/AgentSelection.cs (OnlySelectable) — der Hinweis ist reine Anzeige, die Auswahlmenge darf sich nicht ändern.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Winziger Eingriff, der stille Fehlzuweisungen verhindert — bleibt aber eine Randnotiz, die erst auffaellt, wenn sie einmal eine gerissene Frist verhindert hat.
- *Architektur-Passung (7/10):* AgentSelection ist der dokumentierte Engpass für jede Auswahlliste und AbsenceService liefert die Daten, aber der Hinweis muss in jeder Picker-Variante (MudSelect/MudAutocomplete/Roster) einzeln gerendert werden und der Zeitraum will je Ziel-Datum ausgewertet sein.
- *Aufwand und Dauerlast (8/10):* Ein zusätzlicher Join und ein Hinweis-Suffix in den vorhandenen Pickern, danach passiert das von allein — nur mehrere Einbaustellen.

</details>

#### 5. Tastatur-Kurzbefehle

**Aufwand:** klein | **Jury-Schnitt:** 6 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Ein kleiner Satz Kürzel neben Strg+K: Schrägstrich springt ins Suchfeld, n legt auf einer Liste einen neuen Eintrag an, e öffnet die Akte im Bearbeiten-Modus, f folgt ihr, g gefolgt von einem Buchstaben springt in einen Bereich — und Fragezeichen zeigt eine Übersicht.

**Warum.** Strg+K ist der einzige Tastenbefehl der ganzen Seite. Alles andere geht über die Maus durch Menü, Seite, Abschnitt. Wer nebenbei im Spiel ist, braucht kurze Wege; die Sprungziele stehen ohnehin schon maschinenlesbar bereit.

**Setzt an bei.** NOOSE-Website/wwwroot/app.js registriert bereits genau einen globalen keydown-Handler (registerCommandPalette) — dort kommt die Tabelle dazu, und alle Importstellen brauchen dasselbe ?v= (CommandPalette.razor und FinancingCatalogPanel.razor, siehe CLAUDE.md). Ziele und Rechte-Gates liefert NOOSE-Website/Navigation/NavCatalog.cs fertig, ebenso die Anlege-Routen in CommandPalette.razor (CreateCommands). Die Übersicht kann ein MudDialog wie NOOSE-Website/Components/Common/Shared/QuickAddDialog.razor sein.

**Achtung.** Das Fragezeichen kollidiert mit dem geplanten Hilfeknopf des Handbuchs. Eines von beiden muss weichen.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (4/10):* Billig und elegant, aber ausser drei Poweruser-Agenten wird sie niemand lernen; die Maus ist fuer Gelegenheitsnutzer schneller als ein Kuerzel, das man nachschlagen muss.
- *Architektur-Passung (6/10):* app.js ist das einzige global geladene Modul und NavCatalog liefert die Sprungziele maschinenlesbar, aber kontextabhängige Tasten (bearbeiten, folgen) brauchen Verdrahtung auf jeder Detailseite plus die ?v=-Disziplin an beiden Importstellen.
- *Aufwand und Dauerlast (8/10):* Kleines clientseitiges Modul ohne Serveranteil; nur die Sprungziele wollen bei Routenänderungen gelegentlich nachgezogen werden.

</details>

#### 6. Schnellerfassung, die mehr kann als anlegen

**Aufwand:** mittel | **Jury-Schnitt:** 7 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Die Schnellerfassung aus der Kopfzeile bekommt neben »neue Akte« die zwei Einträge, die täglich anfallen: einen Vermerk an eine gesuchte Akte hängen und eine Dienst-Aktivität protokollieren — jeweils mit Suchfeld für die Zielakte.

**Warum.** Der Knopf in der Kopfzeile legt heute ausschließlich leere Akten mit einem Namen an. Die häufigste tägliche Handlung ist aber nicht »neue Akte«, sondern »ich habe gerade etwas beobachtet und will es an der richtigen Akte festhalten« — und das kostet aktuell Suche, Akte öffnen, Abschnitt finden, Dialog öffnen.

**Setzt an bei.** NOOSE-Website/Components/Common/Shared/QuickAddDialog.razor ist der Dialog, der erweitert wird (er kennt bereits acht Aktentypen und die Dienste dahinter). Die Zielakten-Suche liefert NOOSE-Website/Services/SearchService.cs (QuickSearchAsync, so schon in CommandPalette.razor benutzt) zusammen mit SearchNavigation/SearchCatalog in NOOSE-Website/Services/Search/. Die Schreibpfade bestehen: NOOSE-Website/Services/CommentService.cs und NOOSE-Website/Services/AgentActivityService.cs (mit ActivityTemplateService für die Vorlagen). Texteingabe über MentionInput.razor, damit @-Verlinkung und Bild-Einfügen erhalten bleiben.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Trifft die haeufigste Tageshandlung ('ich habe etwas beobachtet') statt der seltenen ('neue Akte') und spart jedes Mal vier Klicks — solider Alltagsgewinn bei kleinem Eingriff.
- *Architektur-Passung (8/10):* QuickAddDialog, der polymorphe CommentService, MentionInput samt Bild-Paste und AgentActivityService mit Vorlagen sind alle vorhanden; es kommt nur ein Aktenwähler über die bestehende Suche dazu, ohne jede Schemaänderung.
- *Aufwand und Dauerlast (7/10):* Zwei zusätzliche Einträge, die vorhandene Dialoge und Dienste wiederverwenden; einmal verdrahtet ändert sich daran nichts mehr.

</details>

#### 7. Persönliche Textbausteine

**Aufwand:** mittel | **Jury-Schnitt:** 6.3 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Eine kleine eigene Schnipsel-Sammlung, abrufbar direkt aus dem Kommentar-, Chat- und Dok-Feld: Baustein wählen, Text wird eingefügt, Platzhalter werden dabei ersetzt.

**Warum.** Vorlagen gibt es für Dokumente, Aktivitäten, Personalakte, Kasse und Bürger-Nachrichten — also für alles, was die Führung pflegt. Ausgerechnet die Felder, in die ein Agent täglich zehnmal tippt (Vermerk an der Akte, Taskforce-Chat, Dok-Text), haben keine. Die immer gleichen Formulierungen (»Observation ohne Feststellung«, »Kontakt bestätigt durch…«) werden jedes Mal neu geschrieben oder aus einer alten Akte kopiert.

**Setzt an bei.** Einfügepunkte: NOOSE-Website/Components/Common/Shared/MentionInput.razor (hat bereits eine Adornment-Schaltfläche für den @-Picker, dieselbe Mechanik) und RichTextEditor.razor; genutzt von CommentPanel.razor, Taskforces/Shared/TaskforceChatPanel.razor und People/Shared/DocDialog.razor/ObservationDialog.razor. Dienst-Vorbild: NOOSE-Website/Services/ActivityTemplateService.cs bzw. DocTemplateService.cs. Platzhalter-Ersetzung steht fertig in NOOSE-Website/Services/PlaceholderService.cs ({{Name}}, {{Datum}}, {{Agent}}) — aufpassen, das ist das Dokument-Token-System und darf nicht mit BewerbungTemplateRenderer vermischt werden.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Trifft die Felder, in die tatsaechlich getippt wird, spart aber pro Einsatz nur Sekunden — und wer wenig schreibt, legt sich nie Bausteine an.
- *Architektur-Passung (7/10):* Fünf Vorlagen-Tabellen und PlaceholderService zeigen das Muster bis ins Detail, und MentionInput ist der eine Eingabe-Baustein, in den der Abrufknopf muss — Preis ist ein sechstes Vorlagensystem in einem Haus, das schon vier getrennte Token-Welten sauber halten muss.
- *Aufwand und Dauerlast (7/10):* Kleine Tabelle je Agent, deren Inhalt der Nutzer selbst pflegt, eingebunden an drei Feldern — kein Betrieb, kein Risiko, überschaubare Fläche.

</details>

#### 8. Listen-Ansichten speichern

**Aufwand:** mittel | **Jury-Schnitt:** 6 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** »Diese Ansicht merken« in jeder Listen-Kopfzeile: Route plus gesetzte Filter werden als benannter Eintrag gespeichert und erscheinen in den Favoriten und im Schnellzugriff (Strg+K).

**Warum.** Listenfilter landen zwar über QueryState in der URL, aber jeder Einstieg über das Menü setzt sie zurück. Wer täglich dieselbe Auswahl braucht (»Personen, Verdachtsfall, Aktualität rot«), stellt sie jeden Tag neu ein. Gespeicherte Suchen gibt es bereits — aber nur auf /suche, nicht auf den Listen, auf denen tatsächlich gearbeitet wird. Das Aufgaben-Board schreibt seine Filter nicht einmal in die URL, dort überlebt nicht mal der Zurück-Button.

**Setzt an bei.** NOOSE-Website/Components/Common/Shared/QueryState.cs schreibt die Filter schon; NOOSE-Website/Components/Pages/People/PeopleList.razor zeigt das Muster (q/einstufung/aktualitaet). Speichern passt ohne neue Tabelle in NOOSE-Website/Models/Navigation/NavPreferences.cs: NavFavorite trägt bereits ein freies Route-Feld, es braucht nur ein Kind »view«. Anheften-Knopf sitzt schon in NOOSE-Website/Components/Common/Shared/NavBreadcrumbs.razor, Auflistung in NOOSE-Website/Components/Common/Navigation/NavCustomizeDialog.razor und NOOSE-Website/Components/Common/Shared/CommandPalette.razor. Nachziehen: NOOSE-Website/Components/Pages/Jobs/JobsList.razor auf QueryState umstellen; NOOSE-Website/Components/Common/Shared/RecentsTracker.razor schneidet den Query-Teil derzeit ab.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Spuerbare Erleichterung, aber nur fuer die Handvoll Leute, die taeglich dieselbe gefilterte Liste braucht; die meisten Agenten oeffnen eine Liste ohnehin nur gelegentlich.
- *Architektur-Passung (7/10):* SavedSearch (AgentId + Name + SuchparameterJson) lässt sich fast wörtlich wiederverwenden und Favoriten/Strg+K sind die fertigen Oberflächen, aber der Knopf muss in rund zwanzig Listenköpfe und das Aufgaben-Board erst auf QueryState umgestellt werden.
- *Aufwand und Dauerlast (6/10):* Machbar, aber das Aufgaben-Board schreibt seine Filter nicht einmal in die URL, und gespeicherte Ansichten veralten still, sobald sich ein Filter umbenennt.

</details>

#### 9. Entwurf geht nicht mehr verloren

**Aufwand:** mittel | **Jury-Schnitt:** 5.3 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Lange Texte werden während des Tippens im Browser zwischengespeichert; nach Verbindungsabbruch oder versehentlichem Weg-Navigieren bietet die Seite an, den Entwurf wiederherzustellen.

**Warum.** Blazor Server hängt am SignalR-Kreis — es gibt nicht umsonst ein eigenes Wiederverbinden-Fenster. Reißt die Verbindung während eines langen Doks, eines Einsatzberichts oder eines Bibliotheks-Dokuments ab, ist der Text weg. Im ganzen Projekt wird bisher kein localStorage benutzt, es gibt also keinerlei Netz darunter. Für Leute, die nebenbei spielen und die Seite minutenlang offen liegen lassen, ist das der teuerste Einzelverlust überhaupt.

**Setzt an bei.** NOOSE-Website/Components/Common/Shared/RichTextEditor.razor und MentionInput.razor sind die zwei Stellen, durch die praktisch jeder lange Text läuft — beide haben bereits ein eigenes JS-Modul und sauberes IAsyncDisposable/JSDisconnectedException-Handling. Ablage über NOOSE-Website/wwwroot/app.js oder richtext.js (bei Änderung ?v= hochzählen, siehe CLAUDE.md). Der Abbruch ist schon sichtbar über NOOSE-Website/Components/Layout/ReconnectModal.razor. Betroffene Editoren u. a. People/Shared/DocDialog.razor, Operations/, Documents/.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Passiert selten, ist dann aber der teuerste Einzelmoment ueberhaupt: ein verlorener langer Bericht ist der Grund, aus dem jemand danach gar nichts mehr auf der Seite schreibt.
- *Architektur-Passung (4/10):* Im gesamten Projekt gibt es keine einzige localStorage-Nutzung und kein Muster dafür; das einzige Vorbild (Entwurfs-Sicherung im Eignungstest) ist bewusst serverseitig gelöst, also wäre das eine neue Browser-Persistenz-Konvention, die in jede lange Texteingabe einzeln eingezogen werden muss.
- *Aufwand und Dauerlast (5/10):* Die erste localStorage-Nutzung überhaupt bringt Feinheiten (veraltete Entwürfe, falsches Wiederherstellen) und legt nebenbei VS-Text unverschlüsselt im Browser ab — Nutzen groß, Sorgfaltsbedarf ebenso.

</details>

#### 10. Neu seit deinem letzten Besuch

**Aufwand:** mittel | **Jury-Schnitt:** 6.3 | **Blickwinkel:** Alltag des einzelnen Agenten

> **Umgesetzt am 24.09.2026** – ohne neue Tabelle. Der letzte Besuch kommt aus dem Zugriffsprotokoll, das
> jede Akte ohnehin schreibt (neu ist nur ein Index), und Aufrufe mit weniger als 30 Minuten Abstand gelten als
> ein Besuch. Zeile und *Neu*-Markierung stehen auf den sieben Akten, die Besuche protokollieren und einen
> Zeitstrahl-Abschnitt haben. Einzelheiten in `docs/superpowers/plans/2026-09-23-neu-seit-besuch.md`.

**Was es tut.** Beim Öffnen einer Akte markiert der Zeitstrahl, was seit dem eigenen letzten Aufruf dazugekommen ist, und eine Zeile oben fasst es zusammen (»3 neue Vermerke, 1 Dok, Einstufung geändert«).

**Warum.** Eine gut gepflegte Personenakte hat 15 Abschnitte. Wer sie nach einer Woche wieder aufmacht, muss jeden Abschnitt einzeln nach Zeitstempeln absuchen, um zu erkennen, was passiert ist. Für beobachtete Akten kommt zwar eine Benachrichtigung, aber sie sagt nur »geändert«, nicht was.

**Setzt an bei.** Das Muster existiert bereits an einer Stelle und funktioniert: NavPreferences.ChronikLastSeenUtc in NOOSE-Website/Models/Navigation/NavPreferences.cs treibt genau so einen Trenner in der Chronik. Der Besuch wird ohnehin schon protokolliert — NOOSE-Website/Services/AccessLogService.cs (LogViewAsync), aufgerufen aus NOOSE-Website/Components/Pages/People/PersonDetail.razor. Die Ereignisse liefert NOOSE-Website/Services/TimelineService.cs, dargestellt über NOOSE-Website/Components/Common/Shared/TimelinePanel.razor und NOOSE-Website/Services/TimelineDisplay.cs.

**Achtung.** Braucht einen Zeitstempel je Agent UND Akte - die einzige neue Tabelle unter den angenommenen Vorhaben. `NavPreferences.ChronikLastSeenUtc` ist das Vorbild, reicht hier aber nicht, weil pro Akte gezaehlt wird.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Loest das echte Problem, dass eine 15-Abschnitte-Akte nach einer Woche nicht mehr auswertbar ist — ueberschneidet sich allerdings deutlich mit dem Rueckkehrer-Block auf dem Lagezentrum.
- *Architektur-Passung (8/10):* Der eigene letzte Aufruf steht bereits im Zugriffsprotokoll (AccessLog mit EntitaetTyp/EntitaetId/Zeitpunkt) und der Zeitstrahl je Akte ist gebaut; die Zusammenfassungszeile verlangt allerdings die Kind-Typ-Aufschlüsselung in TimelineService, also genau die Stelle mit dem bekannten Fan-out-Fallstrick.
- *Aufwand und Dauerlast (5/10):* Braucht einen Besuchsstempel je Agent und Akte — eine Tabelle, die mit Nutzern mal Akten wächst und eine Aufräumregel verlangt — plus Diff-Logik über 15 Abschnitte.

</details>

#### 11. Mehrfachauswahl in der Suche

**Aufwand:** mittel | **Jury-Schnitt:** 6 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Suchtreffer bekommen Auswahlkästchen und eine Leiste am Fuß: die markierten Akten gemeinsam mit einem Vorgang verknüpfen, mit einem Stichwort versehen oder auf die Beobachtungsliste nehmen.

**Warum.** Eine Recherche endet heute in einer Trefferliste, aus der man jede Akte einzeln öffnen und einzeln verknüpfen muss – bei acht Treffern acht Rundreisen. Die Verknüpfungs-, Stichwort- und Beobachtungs-Dienste sind alle da, nur der Weg von einem Suchergebnis dorthin fehlt.

**Setzt an bei.** NOOSE-Website/Components/Pages/Search/SearchPage.razor (Trefferliste, gespeicherte Suchen, Facetten), NOOSE-Website/Services/Search/SearchCatalog.cs (Trefferform, Route, IsRoutable – entscheidet, welche Treffer überhaupt auswählbar sind), NOOSE-Website/Services/RecordTypeDisplay.cs (Label/Icon je Typ), NOOSE-Website/Services/LinkService.cs (KnownTypes, CreateAsync), NOOSE-Website/Components/Common/Shared/LinkDialog.razor und TagPickerDialog.razor (fertige Zielauswahl), NOOSE-Website/Services/WatchlistService.cs

**Achtung.** Massen-Schreibpfade umgehen die SaveChanges-Interceptors. `Permission.RequireWriteAccess` muss dann explizit aufgerufen und je Akte ein `ManualAudit.Row` geschrieben werden - sonst fehlt die Aenderung im Nachweis.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Deutlich zielgenauer und billiger als Mehrfachauswahl in allen Listen, weil es genau am Uebergang Recherche→Handlung sitzt — aber acht Treffer auf einmal zu verknuepfen ist kein taeglicher Vorgang.
- *Architektur-Passung (7/10):* Suchtreffer haben genau die (Typ, Id)-Form, die LinkService, TagService und WatchlistService erwarten, und es gibt nur eine Trefferoberfläche statt zwanzig Listen — zu beachten ist, dass nicht jede Kategorie routing- bzw. verknüpfungsfähig ist (SearchCatalog.IsRoutable).
- *Aufwand und Dauerlast (6/10):* Nur eine Seite statt aller Listen, aber Massenaktionen über gemischte Aktentypen müssen Rechte und Audit je Treffer einzeln einhalten.

</details>

### Gruppe C - Aufmerksamkeitskanaele

*Voraussetzung dafuer, dass alles andere ueberhaupt gesehen wird. Eine Glocke, die immer rot ist, wird nicht gelesen.*

#### 12. Benachrichtigungs-Posteingang

**Aufwand:** klein | **Jury-Schnitt:** 8.3 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Eine vollwertige Seite /benachrichtigungen mit Filter nach Kategorie und Zeitraum, Blättern über die letzten 20 hinaus und »als ungelesen markieren«.

**Warum.** Die Glocke zeigt nur die 20 neuesten Einträge, und ein Klick markiert sofort als gelesen und navigiert weg. Wer zwei Tage im Spiel war, hat keine Chance mehr, älteres nachzulesen: die 21. Benachrichtigung ist in der UI schlicht nicht mehr erreichbar, obwohl sie in der Datenbank steht. Und wer versehentlich klickt, verliert den Merker.

**Setzt an bei.** NOOSE-Website/Components/Layout/NotificationBell.razor (Dropdown-Logik, Live-Refresh über NotificationBroadcaster) und NOOSE-Website/Services/NotificationService.cs — GetOwnAsync akzeptiert bereits max bis 100, es fehlt nur Paging und ein Typ-Filter. Kategorie-Namen und Icons kommen fertig aus NotificationTypeDisplay in NOOSE-Website/Models/Enums/NotificationType.cs. Filter-in-URL über NOOSE-Website/Components/Common/Shared/QueryState.cs.

**Achtung.** Nachgemessen: `NotificationBell.razor:75` ruft `GetOwnAsync(User, 20)` - fest verdrahtet. Der Dienst kann bereits bis 100; es fehlen nur Blaettern und Typ-Filter.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Schliesst einen echten Datenverlust: die 21. Meldung ist in der UI schlicht nicht erreichbar, und ein Fehlklick loescht den Merker — billig zu bauen, sofort spuerbar fuer jeden, der zwei Tage weg war.
- *Architektur-Passung (9/10):* Die Notification-Tabelle trägt alles Nötige bereits, Filter in der URL macht QueryState, und "als ungelesen" ist eine Methode am bestehenden Dienst — eine reine Seite über vorhandenen Daten.
- *Aufwand und Dauerlast (9/10):* Nur Oberfläche mit Filter und Blättern auf Daten, die ohnehin schon in der Datenbank stehen — kein Zustand, kein Worker, keine Pflege.

</details>

#### 13. Benachrichtigungen selbst steuern

**Aufwand:** mittel | **Jury-Schnitt:** 8 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Ein Abschnitt im eigenen Profil, in dem jeder Agent pro Benachrichtigungs-Kategorie festlegt, ob sie die Glocke klingeln lässt — plus eine Ruhe-Option während einer eingetragenen Abmeldung.

**Warum.** Es gibt 36 NotificationType-Kategorien und keinerlei persönliche Steuerung: Wer nur Personenakten führt, bekommt trotzdem jede Finanzierung, jedes Feedback, jede Pressemitteilung und jeden Lagebericht in dieselbe Glocke. Eine Glocke, die immer rot ist, wird nicht mehr gelesen — damit gehen die wirklich wichtigen Erwähnungen und Aufgaben unter.

**Setzt an bei.** NOOSE-Website/Models/Enums/NotificationType.cs (Kategorien inkl. Label und Icon in NotificationTypeDisplay) — die Filterung gehört zentral in NOOSE-Website/Services/NotificationService.cs, wo alle fünf Zustellwege (NotifyAsync, NotifyOnceAsync, NotifyManyAsync, NotifyManyOnceAsync, FanOutMentionsAsync) zusammenlaufen. Für die Speicherung ist das Muster schon da: JSON-Spalte am Agenten wie NOOSE-Website/Models/Navigation/NavPreferences.cs + NOOSE-Website/Services/NavPreferencesService.cs (inkl. 30s-MemoryCache) — also ohne neue Tabelle. UI-Anker: NOOSE-Website/Components/Pages/Account/MyProfil.razor. Abwesenheitsfenster liefert NOOSE-Website/Services/AbsenceVisibility.cs (Covering).

**Achtung.** Nachgemessen: 36 Eintraege in `Models/Enums/NotificationType.cs`. Die Filterung gehoert zentral in `NotificationService`, wo alle fuenf Zustellwege zusammenlaufen - nicht in die Glocke.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (8/10):* 36 Kategorien in einer einzigen Glocke bedeuten, dass sie dauerhaft rot ist und deshalb niemand mehr hinschaut — das entwertet still das komplette Benachrichtigungssystem, inklusive der wirklich wichtigen Erwaehnungen.
- *Architektur-Passung (9/10):* NotificationService ist der eine Engpass, durch den alles läuft, und NavPreferencesJson auf Agent zeigt bereits, wie persönliche Einstellungen ohne neue Tabelle abgelegt werden; die Ruhe-Option liest schlicht AbsenceService.
- *Aufwand und Dauerlast (7/10):* Eine kleine Präferenztabelle über 36 Kategorien, die bei jedem neuen Typ einen Handgriff kostet, dafür aber die Glocke davor bewahrt, dauerhaft ignoriert zu werden.

</details>

#### 14. Persoenliche Ping-Einstellungen und Tages-Digest

**Aufwand:** mittel | **Jury-Schnitt:** 5 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Jeder Agent legt selbst fest, wofuer er angepingt werden will, und bekommt den Rest einmal taeglich als eine gebuendelte Nachricht statt als zehn Einzelpings.

**Warum.** Heute entscheidet allein der Admin-Schalter, welche Kategorie in welchen Kanal geht - der Einzelne kann nichts abschalten. Wer zu oft gepingt wird, stellt den Kanal stumm, und dann kommt auch das Wichtige nicht mehr an.

**Setzt an bei.** Benachrichtigungen entstehen zentral in NOOSE-Website/Services/NotificationService.cs (NotifyAsync/NotifyManyAsync) und werden dort schon nach Discord weitergereicht; die Routing-Tabelle je Kategorie liegt in NOOSE-Website/Models/Common/DiscordWebhookModels.cs (DiscordRouting). Das Zusammenfassen ist im Kern schon erfunden - NotifyOnceAsync faltet offene Meldungen auf dieselbe Zeile. Ein persoenliches Einstellungs-Feld kann wie NavPreferencesJson als JSON am Agenten haengen (NOOSE-Website/Data/Entities/Agent.cs, NavEinstellungen). Digest-Worker nach dem Muster von NOOSE-Website/Infrastructure/Jobs/JobDueSoonWorker.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Ein stummgeschalteter Kanal ist genauso schlimm wie gar kein Kanal, und der Digest rettet die Restinformation — ueberschneidet sich allerdings konzeptionell stark mit der In-App-Steuerung der Glocke.
- *Architektur-Passung (4/10):* Das Discord-Modell ist Kategorie→Kanal mit Rollen-Ping; wer pro Agent steuern will, braucht Direktnachrichten und damit einen Bot-Token, denn in einer geteilten Kanalnachricht lässt sich ein einzelner Empfänger nicht stummschalten — und die In-App-Hälfte doppelt Idee 15.
- *Aufwand und Dauerlast (5/10):* Persönliche Pings setzen Direktnachrichten und damit einen Bot voraus, und der Digest-Worker muss Kategorien, Ausfälle und geschlossene DMs dauerhaft mitführen.

</details>

#### 15. Meine Schicht — persönlicher Einstieg

**Aufwand:** mittel | **Jury-Schnitt:** 8.7 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Eine eigene Seite (/meine-schicht), die nur zeigt, was HEUTE an mir hängt: meine offenen Aufgaben, fällige und anstehende Wiedervorlagen, ungelesene Erwähnungen, quittierungspflichtige Brett-Einträge, neue Nachrichten in meinen Taskforces und meine beobachteten Akten mit Änderung. Über NavPreferences.StartRoute als Startseite wählbar.

**Warum.** Das Lagezentrum (/dashboard) ist eine Behörden-Bilanz: Gesamtzahl Personenakten, Verteilungen, Gefährdungslisten. Für die Frage »was mache ich jetzt« liefert es genau eine Kachel (fällige Wiedervorlagen). Jeder Agent klappert deshalb beim Einloggen 5-6 Seiten ab (/aufgaben, /brett, /watchlist, jede Taskforce, Glocke), um zu sehen, ob etwas auf ihn wartet.

**Setzt an bei.** Alle Datenquellen existieren bereits und müssten nur zusammengezogen werden: NOOSE-Website/Services/FollowupService.cs (GetMyDueAsync), NOOSE-Website/Services/IJobService.cs (GetTeamBoardAsync mit onlyMy=true), NOOSE-Website/Services/NotificationService.cs (GetOwnAsync/GetUnreadCountAsync), NOOSE-Website/Services/IAnnouncementService.cs (GetOpenAcknowledgmentsCountAsync), NOOSE-Website/Services/WatchlistService.cs (GetFollowedResolvedAsync). Layout-Bausteine: NOOSE-Website/Components/Common/Shared/StatTile.razor, EmptyState.razor, RecordSectionRail.razor; Vorbild für den Seitenaufbau ist NOOSE-Website/Components/Pages/Home.razor. Startseiten-Wahl steckt schon in NOOSE-Website/Models/Navigation/NavPreferences.cs (StartRoute).

**Achtung.** Nimmt den Rueckkehrer-Block - was seit dem letzten Besuch im Haus passiert ist - als eigenen Abschnitt auf. Die urspruenglich getrennte Idee wurde hier eingegliedert, sonst waeren es zwei Einstiege fuer dieselbe Frage.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (9/10):* Jeder Agent, bei jedem Login, ersetzt fuenf bis sechs Seiten durch eine — die Daten liegen alle bereit, es fehlt nur die Zusammenfuehrung, und genau das ist die erste Frage jedes Spielers.
- *Architektur-Passung (9/10):* Reine Aggregation über bereits vorhandene Dienste (FollowupService.GetMyDueAsync, JobService, NotificationService, GetOpenAcknowledgmentsCountAsync, TaskforceChat, WatchlistService) und NavPreferences.StartRoute ist als Startseiten-Schalter schon da — kein Schema, keine Migration.
- *Aufwand und Dauerlast (8/10):* Reine Aggregation über sechs vorhandene Dienste ohne neues Datenmodell; einzige Dauerlast ist, die Seite mitzuziehen, wenn eine Quelle sich ändert.

</details>

#### 16. Ungelesen-Marker im Taskforce-Chat

**Aufwand:** mittel | **Jury-Schnitt:** 8 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Pro Agent und Taskforce ein Lese-Zeitstempel: die Taskforce-Liste zeigt einen Zähler neuer Nachrichten, im Chat trennt eine Linie »neu seit deinem letzten Besuch«.

**Warum.** Der Taskforce-Chat verschickt nur bei einer @-Erwähnung eine Benachrichtigung. Alles andere ist unsichtbar — man muss jede eigene Taskforce einzeln öffnen und die Zeitstempel der letzten Nachricht selbst vergleichen, um zu sehen, ob etwas passiert ist. Genau das treibt die Leute zurück zu Discord.

**Setzt an bei.** Das Muster ist im Haus schon gebaut: NOOSE-Website/Data/Entities/Public/TicketBeteiligter.cs (LastReadAt) und NOOSE-Website/Data/Entities/Public/Ticket.cs (CitizenLastReadAt/AgentLastReadAt). Andockpunkte: NOOSE-Website/Services/TaskforceChatService.cs, NOOSE-Website/Infrastructure/Chat/TaskforceChatBroadcaster (Live-Push steht), NOOSE-Website/Components/Pages/Taskforces/Shared/TaskforceChatPanel.razor und TaskforceCard.razor/TaskforceList.razor für das Abzeichen. Als Trenner-Vorbild dient NavPreferences.ChronikLastSeenUtc in NOOSE-Website/Models/Navigation/NavPreferences.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (8/10):* Ohne Ungelesen-Zaehler verliert der Taskforce-Chat jeden Vergleich mit Discord, wo genau das seit Jahren Standard ist — das ist die Grundbedingung dafuer, dass der Chat ueberhaupt benutzt wird.
- *Architektur-Passung (8/10):* Ein Lese-Zeitstempel je (Agent, Taskforce) ist eine winzige Tabelle neben dem vorhandenen TaskforceAgent, der Zähler-Push läuft über den schon gebauten TaskforceChatBroadcaster, und das Schreiben beim Lesen folgt der dokumentierten ExecuteUpdate-Ausnahme (wie PublicWantedService.CountViewAsync).
- *Aufwand und Dauerlast (8/10):* Ein Zeitstempel je Agent und Taskforce ist winzig, pflegt sich selbst und beseitigt genau den Grund, warum die Absprachen nach Discord abwandern.

</details>

#### 17. Nachfassen bei offener Lesebestätigung

**Aufwand:** klein | **Jury-Schnitt:** 7.7 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Eine quittierungspflichtige Ankündigung erinnert nach einer einstellbaren Frist automatisch alle, die noch nicht bestätigt haben, und zeigt der Führung eine Liste der Überfälligen.

**Warum.** Das Schwarze Brett schnappt sich beim Anlegen eine Empfängerliste und zählt Quittierungen – aber niemand fasst nach. Der Ersteller sieht nur eine Zahl "7 von 19" und müsste selbst über Discord hinterherlaufen. Genau dafür gibt es bereits einen Worker-Bauplan und einen Discord-Kanal für Ankündigungen.

**Setzt an bei.** NOOSE-Website/Services/AnnouncementService.cs + IAnnouncementService.cs (AcknowledgmentRow mit AcknowledgedAt == null ist die Offen-Liste, GetOpenAcknowledgmentsCountAsync existiert), NOOSE-Website/Components/Pages/Board/Shared/AcknowledgmentPanel.razor, NOOSE-Website/Infrastructure/Announcements/AcknowledgmentBroadcaster.cs (Live-Push steht), NOOSE-Website/Infrastructure/Followups/FollowupDueWorker.cs (Fälligkeits-Worker als Vorlage, inkl. NotifiedAt-Stempel gegen Doppelmeldung), Announcement ist in Models/Common/DiscordWebhookModels.cs bereits routingfähig

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Billig, und es nimmt dem Ersteller genau das ab, was er heute im Discord von Hand hinterherlaeuft — Worker und Kanal sind bereits vorhanden.
- *Architektur-Passung (9/10):* Empfängerliste, Quittierungstabelle und Zähler stehen fertig am Schwarzen Brett, der Ankündigungs-Discord-Kanal existiert, und die Erinnerung ist ein Worker im Stil von MeetingReminderWorker — reine Ergänzung ohne neuen Mechanismus.
- *Aufwand und Dauerlast (8/10):* Ein kleiner Worker auf einer bereits gespeicherten Empfängerliste und einem vorhandenen Discord-Kanal — danach erinnert sich das System selbst.

</details>

#### 18. Gespeicherte Suche als Wach-Auftrag

**Aufwand:** mittel | **Jury-Schnitt:** 4.7 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Eine gespeicherte Suche bekommt einen Schalter "überwachen". Ein Worker führt sie regelmäßig im Namen ihres Besitzers aus und meldet nur Treffer, die beim letzten Lauf noch nicht dabei waren – per Glocke und optional in den Discord-Kanal.

**Warum.** Gespeicherte Suchen existieren bereits, sind aber reine Lade-Knöpfe: Der Agent muss selbst auf die Seite gehen und klicken. Wer wissen will, ob eine neue Akte zu "Ballas" oder zu einem Kennzeichen auftaucht, muss täglich manuell suchen – und tut es deshalb nicht. Beobachten kann man heute nur einzelne, bereits bekannte Akten (Watchlist), nicht ein Thema.

**Setzt an bei.** NOOSE-Website/Services/SavedSearchService.cs + ISavedSearchService.cs (Speichern/Laden pro Agent steht), NOOSE-Website/Models/Common/SearchModels.cs (SearchCriteria wird schon als JSON persistiert), NOOSE-Website/Services/SearchService.cs (Orchestrator, sichtbarkeitsgefiltert pro ClaimsPrincipal), NOOSE-Website/Components/Account/AgentClaimsPrincipalFactory.cs (baut den Principal, den der Worker pro Besitzer braucht), NOOSE-Website/Infrastructure/Followups/FollowupDueWorker.cs (Worker-Muster inkl. Sichtbarkeitsprüfung je Empfänger), NOOSE-Website/Services/NotificationService.cs (NotifyOnceAsync faltet Wiederholungen), NOOSE-Website/Services/DiscordWebhookService.cs, NOOSE-Website/Components/Pages/Search/SearchPage.razor (UI-Anker)

**Achtung.** Die eigentliche Arbeit ist nicht der Hintergrundlauf, sondern dass Rechteaenderungen des Besitzers mitgezogen werden. Eine Suche im Namen eines Abwesenden wendet seine Rechte ausserhalb einer Sitzung an; der ScopeStamp-Gedanke aus `NooseiChatService` ist das Vorbild.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Macht aus einem toten Knopf einen Waechter und erweitert Beobachtung erstmals von der Akte aufs Thema — trifft aber nur die wenigen Agenten, die ueberhaupt Suchen speichern.
- *Architektur-Passung (6/10):* SavedSearch speichert die Kriterien bereits als JSON und Worker sind erstklassig etabliert, aber ein Lauf "im Namen des Besitzers" muss einen ClaimsPrincipal außerhalb eines Requests bauen (Claims kommen sonst aus dem Login-Cookie), und 67 Provider unter Wanduhr-Budget je Agent regelmäßig zu fahren ist echte Last.
- *Aufwand und Dauerlast (3/10):* Ein Worker, der 67 Suchanbieter regelmäßig im Namen fremder Konten ausführt, kostet dauerhaft Rechenzeit und verlangt eine Stellvertreter-Identität, deren Rechte nach jeder Rangänderung veralten können.

</details>

#### 19. Gegenaufklärungs-Regeln melden sich selbst

**Aufwand:** mittel | **Jury-Schnitt:** 7 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Die selbstgebauten Auffälligkeits-Regeln werden von einem Hintergrund-Worker regelmäßig ausgewertet. Neue Funde landen als Benachrichtigung bei der Führung statt darauf zu warten, dass jemand die Seite öffnet.

**Warum.** Der komplette Regel-Baukasten und der Evaluator sind gebaut, werden aber nur ausgewertet, wenn zufällig jemand /nachweis?tab=gegenaufklaerung aufruft. Eine Insider-Auffälligkeit, die niemand anschaut, ist wertlos – genau dieser Bereich lebt von Zeitnähe.

**Setzt an bei.** NOOSE-Website/Services/CounterIntel/CounterIntelService.cs (GetFlagsAsync – nur von Panels aufgerufen), NOOSE-Website/Services/CounterIntel/CounterIntelRuleEvaluator.cs + CounterIntelEventLoader.cs (Auswertung steht komplett), NOOSE-Website/Components/Pages/Monitoring/Shared/CounterIntelFlagsPanel.razor, NOOSE-Website/Infrastructure/Followups/FollowupDueWorker.cs (Worker-Muster), NOOSE-Website/Services/NotificationService.cs, NOOSE-Website/Models/Enums/NotificationType.cs (neue Kategorie + Routing analog Models/Common/DiscordWebhookModels.cs). Gleiches Muster wäre 1:1 auf ILlmAnomalyService (Services/Llm/LlmAnomalyService.cs) übertragbar.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Ein fertig gebauter Bereich, der nur auswertet, wenn zufaellig jemand hinschaut, ist praktisch abgeschaltet — der Nutzen bleibt aber auf die Fuehrung und ein seltenes Ereignis beschraenkt.
- *Architektur-Passung (9/10):* CounterIntelRuleEvaluator ist eine reine statische Auswertung über geladene Ereignisse, die CounterIntelService heute nur bei Seitenaufruf anstößt — ein Hosted Service davor plus Melde-Dedup ist fast mechanisch, das Worker-Muster gibt es zehnfach.
- *Aufwand und Dauerlast (7/10):* Der Evaluator ist gebaut, es fehlt ein Worker drumherum; einzige echte Arbeit ist die Entdopplung, damit dieselbe Auffälligkeit nicht täglich neu meldet.

</details>

### Gruppe D - Rollenspiel-Inhalt und Fuehrungsarbeit

*Neuer Inhalt und Werkzeuge fuer die Fuehrung.*

#### 20. Ehrungen und Urkunden

**Aufwand:** klein | **Jury-Schnitt:** 7.3 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Die Fuehrung verleiht benannte Ehrenzeichen mit Verleihungstext und Datum. Sie erscheinen am Profil, in der Personalakte und optional am oeffentlichen Fuehrungsprofil, werden auf dem Schwarzen Brett und ueber Discord bekanntgegeben und lassen sich als Urkunde drucken - ebenso Ernennungs- und Befoerderungsurkunden aus dem Rang-Verlauf.

**Warum.** Auszeichnungen sind heute ausschliesslich automatische Meilenstein-Abzeichen ('Aktenfuchs', '25 Akten angelegt'). Die Fuehrung hat kein Mittel, tatsaechliche Leistung sichtbar zu wuerdigen, und es gibt keinen einzigen zeremoniellen Moment - obwohl die Verleihungstabelle mit Datum und Notiz bereits existiert.

**Setzt an bei.** Die Tabelle traegt schon VerliehenAm und Notiz: NOOSE-Website/Data/Entities/Gamification/AgentBadge.cs; bislang speist sie nur der Sweep aus NOOSE-Website/Services/Gamification/BadgeCatalog.cs + GamificationService.cs. Anzeige NOOSE-Website/Components/Common/Shared/AgentBadgesPanel.razor und NOOSE-Website/Components/Pages/Account/MyProfil.razor (Abschnitt 'Auszeichnungen', Z. 176). Befoerderungsdaten: NOOSE-Website/Data/Entities/Personnel/AgentRankHistory.cs. Urkundentext ueber NOOSE-Website/Services/PlaceholderService.cs ({{Agent}}, {{Dienstgrad}}, {{Datum}}), Druck ueber NOOSE-Website/Components/Common/Shared/PrintFrame.razor. Bekanntgabe ueber NOOSE-Website/Services/DiscordWebhookService.cs und NOOSE-Website/Services/AnnouncementService.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Zeremonielle Momente binden RP-Communities spuerbar, und die Verleihungstabelle existiert schon — es bleibt aber ein seltenes Ereignis, kein taeglicher Nutzen.
- *Architektur-Passung (8/10):* AgentNoteKind.Commendation, AgentBadge, Schwarzes Brett mit Discord-Kanal und die Druckinfrastruktur existieren; neu sind nur ein Ehrenzeichen-Katalog (Werteliste) und eine Urkunden-Druckansicht.
- *Aufwand und Dauerlast (8/10):* Kleine Entität plus Druckansicht auf einer schon vorhandenen Verleihungstabelle, null Betriebskosten — einziges Risiko ist eine Führung, die nie etwas verleiht.

</details>

#### 21. Inaktivitäts-Radar für Agenten

**Aufwand:** klein | **Jury-Schnitt:** 7.7 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Eine Liste der Agenten ohne Schreib- oder Lesespur seit N Tagen, ohne genehmigte Abmeldung im Zeitraum, gestuft nach Dauer (nachfassen, Vermerk, Kündigungsvorschlag) mit den passenden Knöpfen direkt daneben.

**Warum.** Karteileichen halten Dienstgrade, Taskforce-Plätze, Aktenzuordnungen und Auswahllisten besetzt und verfälschen jede Kennzahl zur Mitarbeit. Die Rohdaten liegen in zwei Tabellen bereit, es fragt sie nur niemand in dieser Richtung ab — man sieht, wer viel tut, nie wer gar nichts tut.

**Setzt an bei.** Infrastructure/Audit/AuditLog.cs und Infrastructure/Audit/AccessLog.cs (beide mit AgentId + Zeitpunkt — ein MAX je Agent genügt); Services/CounterIntel/CounterIntelEventLoader.cs liest dieselben Tabellen bereits agentenweise gebucketet; Services/AbsenceService.cs für die legitimen Ausnahmen; Services/AgentManagementService.cs BlockAsync/TerminateAsync und Services/PersonnelFileService.cs NoteCreateAsync für die Folgeaktion; Components/Pages/Admin/Agents.razor als Ort.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Karteileichen verfaelschen jede Kennzahl und blockieren Dienstgrade und Taskforce-Plaetze, und die Rohdaten liegen bereit — es bleibt aber eine monatliche Aufraeumrunde der Fuehrung.
- *Architektur-Passung (9/10):* AuditLog und AccessLog liefern Schreib- und Lesespur, Absence den Ausnahmefilter, und die Folgeaktionen sind AgentNote plus AgentManagementService — eine Abfrage und eine Seite, sonst nichts.
- *Aufwand und Dauerlast (9/10):* Eine Abfrage in die andere Richtung auf zwei bereits vorhandene Tabellen, ohne Worker, ohne Speicher, ohne Folgekosten.

</details>

#### 22. NOOSEI-Antwortentwurf für Bürgerhinweise

**Aufwand:** klein | **Jury-Schnitt:** 7.7 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Der Entwurfs-Knopf, den es im Ticket-Schriftwechsel schon gibt, kommt auch in den Hinweis-Dialog mit dem Bürger und in den Einspruchsbescheid.

**Warum.** Antworten an Bürger sind die langweiligste und am häufigsten liegengebliebene Arbeit. Im Ticket gibt es dafür bereits einen NOOSEI-Entwurf mit Token-Vorschau; im Hinweis-Chat – wo deutlich mehr Nachrichten auflaufen – tippt die Führung weiter von Hand. Dieselbe Mechanik, anderer Datenlieferant.

**Setzt an bei.** NOOSE-Website/Services/Public/TicketAssistService.cs (komplette Vorlage: Gateway-Aufruf, Kontingent, Permission-Guard, Mention-Token-Bereinigung), NOOSE-Website/Components/Pages/Tickets/Shared/TicketMessagePanel.razor (UI-Muster inkl. Kostenanzeige), Ziel: NOOSE-Website/Components/Pages/Tips/Shared/TipMessagePanel.razor (heute ohne jede Assistenz), NOOSE-Website/Services/Public/TipService.cs, NOOSE-Website/Services/Public/ObjectionService.cs, NOOSE-Website/Services/Public/PublicTemplateRenderer.cs (Bürger-Platzhalter)

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Dieselbe fertige Mechanik an die Stelle mit dem groessten Nachrichtenaufkommen und der groessten Liegenbleib-Quote — billigster Weg, damit Buerger ueberhaupt eine Antwort bekommen.
- *Architektur-Passung (9/10):* TicketMessagePanel hat den Entwurfs-Knopf samt Token-Vorschau bereits, TipMessagePanel ist das Schwesterpanel, und Gateway, Kontingent und PublicTemplateRenderer bleiben unverändert — dieselbe Mechanik an einer zweiten Datenquelle.
- *Aufwand und Dauerlast (7/10):* Dieselbe Mechanik an zwei weiteren Datenquellen, Kontingent und Abrechnung greifen bereits — es entstehen nur die ohnehin gedeckelten Token-Kosten.

</details>

#### 23. Maßnahmen aus der Besprechung

**Aufwand:** mittel | **Jury-Schnitt:** 7.7 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Ein Tagesordnungspunkt bekommt neben "erledigt" den Knopf "Maßnahme": daraus entsteht eine Aufgabe oder Wiedervorlage mit Verantwortlichem, verknüpft mit der Besprechung, und der Punkt zeigt dauerhaft ihren Status.

**Warum.** Besprechungen mit Tagesordnung, Protokoll, Anwesenheit und Folgetermin sind gebaut – aber was beschlossen wurde, verschwindet im Protokolltext. Beim nächsten Termin weiß niemand, was aus den Beschlüssen wurde. Aufgaben, Wiedervorlagen und die polymorphe Verknüpfung existieren bereits alle.

**Setzt an bei.** NOOSE-Website/Services/MeetingService.cs + IMeetingService.cs (AgendaItemNoteAsync mit done-Flag, GetAgendaLinksAsync – es gibt keinerlei Job-Bezug), NOOSE-Website/Components/Pages/Meetings/Shared/MeetingAgendaPanel.razor, NOOSE-Website/Services/JobService.cs, NOOSE-Website/Services/FollowupService.cs, NOOSE-Website/Services/LinkService.cs, NOOSE-Website/Components/Common/Shared/FollowupDialog.razor und JobCreateButton.razor (fertige Dialoge)

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Beschluesse verschwinden heute im Protokolltext, und Aufgaben, Wiedervorlagen und die polymorphe Verknuepfung liegen alle bereit — betrifft aber nur die, die zu Besprechungen erscheinen.
- *Architektur-Passung (9/10):* MeetingAgendaItem trägt Erledigt-Status und sogar schon eine Übernahme-Referenz, Aufgaben und Wiedervorlagen samt polymorpher Verknüpfung sind gebaut — ein Knopf, der einen vorhandenen Dienst aufruft und den Status zurückspiegelt.
- *Aufwand und Dauerlast (8/10):* Aufgaben, Wiedervorlagen und die polymorphe Verknüpfung existieren alle — hier fehlt nur der Knopf dazwischen, danach pflegt sich der Status von selbst.

</details>

#### 24. Dubletten-Radar für Personenakten

**Aufwand:** mittel | **Jury-Schnitt:** 7.7 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Die Dublettenwarnung beim Anlegen prüft künftig auch klanggleiche Namen und Aliase statt nur exakte Gleichheit – und ein neuer Ermittlungshinweis listet vorhandene Dublettenpaare im Bestand mit Direktsprung ins Zusammenführen.

**Warum.** Heute schlägt die Warnung nur bei buchstabengleichem Namen oder gleicher Telefonnummer an. Auf einem RP-Server schreiben fünf Agenten denselben Namen fünffach unterschiedlich – die Akte zersplittert, und der Zusammenführen-Dialog wird nie gefunden, weil niemand weiß, dass es zwei Akten gibt. Die phonetische Maschine und das Zusammenführen sind beide fertig, sie kennen einander nur nicht.

**Setzt an bei.** NOOSE-Website/Services/PersonService.cs → FindDuplicatesAsync (Zeile 79, heute reiner Kleinschreib-Vergleich + Telefon), NOOSE-Website/Components/Pages/People/Shared/DuplicateCheck.cs + DuplicateDialog.razor (Dialog steht), NOOSE-Website/Components/Pages/People/Shared/PersonMergeDialog.razor + Services/PersonMergeService.cs (Zusammenführen fertig), NOOSE-Website/Services/ColognePhonetic.cs + Services/TextSimilarity.cs + Services/SearchIndexProjection.cs (Aliase zahlen bereits auf die Person ein), NOOSE-Website/Services/LeadService.cs + Models/Enums/LeadKind.cs (Hinweis-Feed: eine weitere Art)

**Achtung.** Enthaelt zwei Haelften: (a) die Warnung beim Anlegen auf klangaehnliche Namen umstellen, (b) ein Feed vorhandener Dublettenpaare im Bestand. Abgelehnte Paare muessen dauerhaft wegbleiben, sonst wird der Feed nach zwei Wochen ignoriert.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Praevention im Moment des Anlegens ist ungleich wirksamer als spaeteres Zusammenfuehren, und die phonetische Maschine wie der Merge-Dialog existieren beide schon — sie kennen einander nur nicht.
- *Architektur-Passung (9/10):* Die bestehende FindDuplicatesAsync vergleicht nachweislich nur exakt (p.Name.ToLower() == nameLower), ColognePhonetic und der phonetische Suchindex liegen daneben, und der Bestandsbefund ist eine vierte LeadKind-Zeile im schon gebauten Ermittlungshinweis-Feed mit Sprung in den fertigen Merge.
- *Aufwand und Dauerlast (7/10):* Die billigere Hälfte derselben Idee: phonetische Maschine und Zusammenführen sind fertig, das Ergebnis hängt sich an die bestehenden, ignorierbaren Ermittlungshinweise.

</details>

#### 25. Dienstübergabe beim Ausscheiden

**Aufwand:** mittel | **Jury-Schnitt:** 5 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Beim Kündigen — und optional bei einer längeren Abmeldung — zeigt ein Dialog alles, was an dem Agenten hängt: offene Wiedervorlagen, Aufgaben, Taskforce-, Vorgangs- und Operations-Zuordnungen, offene Finanzierungsanträge. Alles lässt sich in einem Schritt auf Nachfolger umhängen.

**Warum.** AgentManagementService.TerminateAsync setzt nur Status, Sperre und Personalvermerk; sämtliche Zuordnungen bleiben auf einem Konto stehen, das danach in keiner Auswahlliste mehr auftaucht (AgentSelection filtert nicht-aktive Konten raus). Die Arbeit verschwindet damit still, statt übergeben zu werden.

**Setzt an bei.** Services/AgentManagementService.cs: TerminateAsync ab Zeile ~625 ist der Einbaupunkt, und DeleteAccountAsync (ab ~790) zählt bereits alle betroffenen Junction-Tabellen auf (JobAssignments, TaskforceAgenten, CaseAgents, OperationAgents, AppointmentAssignments …) — dieselbe Liste, nur umhängen statt löschen; Services/FollowupService.cs, Services/JobService.cs (AgentAssignAsync/AgentRemoveAsync), Services/AbsenceService.cs; Components/Pages/Personnel/Shared/TerminationDialog.razor als Dialograhmen.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (3/10):* Behebt ein echtes stilles Datenleck, tritt aber nur ein paar Mal im Monat ein und wird ausser der Fuehrung von niemandem bemerkt.
- *Architektur-Passung (6/10):* TerminateAsync ist der eine Ausgang, an den der Dialog gehört, und AgentSelection erklärt genau, warum die Zuordnungen danach unerreichbar werden — dafür muss die Übergabe rund acht Dienste einzeln abfragen und umschreiben, jeder mit eigener Signatur.
- *Aufwand und Dauerlast (6/10):* Ein Dialog über sechs Zuordnungsarten, selten benutzt und deshalb billig im Betrieb, aber er muss bei jeder neuen Zuordnungsart mitwachsen, sonst übergibt er unvollständig.

</details>

#### 26. Dienstaufsichts-Blatt je Agent

**Aufwand:** mittel | **Jury-Schnitt:** 5.7 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Ein Abschnitt in der Personalakte, der alles Bewertbare zu einem Agenten bündelt: letzte Aktivität, Schreibleistung pro Woche, Wiedervorlage-Pünktlichkeit, Besprechungs-Fehlzeiten, Abmeldungstage, Punkte und Abzeichen, Gegenaufklärungs-Auffälligkeiten — ein Blatt statt fünf Seiten.

**Warum.** Alle Zahlen existieren bereits, aber verstreut über Statistik-Panels, Bestenliste und Gegenaufklärung, und dort immer als Vergleich über alle Agenten. Für ein Beförderungs-, Ermahnungs- oder Kündigungsgespräch muss die Führung heute vier bis fünf Seiten zusammensuchen und selbst filtern.

**Setzt an bei.** Services/Statistics/WorkforceStatisticsService.cs (GetWorkloadAsync, GetMissedMeetingsAsync, GetAbsenceCalendarAsync — liefern bereits pro Agent, nur als Matrix aggregiert); Services/Gamification/GamificationService.cs GetStatsAsync und GetBadgesAsync; Services/CounterIntel/CounterIntelService.cs GetAgentProfileAsync mit Components/Pages/Monitoring/Shared/CounterIntelProfilePanel.razor als fertigem Panel; Services/AbsenceService.cs; eingebaut in Components/Pages/Personnel/PersonnelFileDetail.razor über Components/Common/Shared/RecordSectionRail.razor. Klarnamen-Regel aus Authorization/AgentPrincipalExtensions.cs beachten.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (4/10):* Spart der Fuehrung vor einem Gespraech eine halbe Stunde Zusammensuchen aus fuenf Seiten, aber solche Gespraeche gibt es einige wenige im Monat, nicht taeglich.
- *Architektur-Passung (7/10):* Alle Zahlen existieren tatsächlich (Gamification, Followup, Meeting-Anwesenheit, Absence, CounterIntel, AuditLogQuery) und das Blatt ist ein RecordSection in der Personalakte — nur liefern diese Dienste heute Vergleiche über alle Agenten, also braucht jeder eine Ein-Agenten-Variante.
- *Aufwand und Dauerlast (6/10):* Reine Zusammenstellung vorhandener Zahlen ohne neues Datenmodell, dafür fest an fünf Quellen gekoppelt, die bei jeder Änderung nachgezogen werden müssen.

</details>

#### 27. Akten-Export für Listen und Dossiers

**Aufwand:** mittel | **Jury-Schnitt:** 5 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Aus jeder Aktenliste die gefilterte Auswahl als CSV ziehen, und aus einer Einzelakte ein vollständiges Dossier (Stammdaten, Doks, Quellen, Kommentare, Verknüpfungen) als Datei — sichtbarkeitsgefiltert und jeder Export im Zugriffsprotokoll vermerkt.

**Warum.** Export gibt es ausschliesslich für die Statistik-Seite. Wer Zahlen für eine Besprechung braucht, eine Akte an eine Partnerbehörde übergeben oder einen Stand ausserhalb der Seite sichern will, muss abtippen oder die Druckansicht missbrauchen. Für die Leitung ist das der Unterschied zwischen 'die Daten sind da' und 'ich komme an die Daten ran'.

**Setzt an bei.** Infrastructure/Export/CsvHelper.cs und Components/Common/StatisticsExportEndpointRouteBuilderExtensions.cs sind direkt kopierbar: Endpoint-Gruppe mit RequireAuthorization(Policies.ActiveAgent, Policies.InternalAgent), Ergebnis als Results.File und ein IAccessLogService.LogViewAsync je Export; Components/Pages/People/PersonPrint.razor und Components/Pages/Factions/FactionPrint.razor stellen das Dossier inhaltlich bereits zusammen; Services/Visibility.cs und Services/ViewerScope.cs für die Filterung; Services/RecordTypeDisplay.cs für die Spaltenköpfe.

**Achtung.** Ein Export nimmt Daten aus dem Rechtesystem heraus. Die Zeile im Zugriffsprotokoll (`IAccessLogService`) ist Bedingung, nicht Zierde - und der Export muss dieselben Sichtbarkeits-Praedikate verwenden wie die Liste, nicht eigene.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (4/10):* Fuer die Leitung gelegentlich wertvoll, fuer alle anderen nicht — und die Druckansichten decken den haeufigsten Fall (etwas vorzeigen) bereits ab.
- *Architektur-Passung (7/10):* CsvHelper und der Endpoint-Baustein der Statistik sind da, und DossierContextBuilder stellt das sichtbarkeitsgefilterte Dossier für NOOSEI längst zusammen — die Listenhälfte scheitert aber daran, dass keine Liste ihre gefilterte Abfrage nach außen anbietet.
- *Aufwand und Dauerlast (4/10):* Export muss je Aktentyp gepflegt werden und driftet bei jeder Feldänderung, und ein vollständiges Dossier als Datei ist der bequemste Weg, Daten aus dem Rechtesystem herauszutragen.

</details>

### Gruppe E - beruehrt eine Architektur-Grenze

*Zuletzt, weil es eine bewusst gesetzte Grenze der Architektur beruehrt.*

#### 28. NOOSEI darf nach Rückfrage schreiben

**Aufwand:** gross | **Jury-Schnitt:** 4.3 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** NOOSEI bekommt Werkzeuge, die keine Änderung ausführen, sondern eine vorschlagen: Wiedervorlage setzen, Stichwort vergeben, Aufgabe anlegen, Akte beobachten, Verknüpfung ziehen. Im Chat erscheint eine Karte mit "Ausführen" – erst der Klick des Agenten schreibt.

**Warum.** Alle 15 Werkzeuge sind rein lesend. NOOSEI kann sagen "diese Akte ist seit 90 Tagen unberührt", aber der Agent muss danach selbst durch drei Seiten klicken. Der Bruch zwischen Erkenntnis und Handlung ist die eigentliche Reibung. Weil der Klick die Schreibrechte des Agenten benutzt, greifen Permission-Guards, ReadOnlyBarrier und Audit unverändert.

**Setzt an bei.** NOOSE-Website/Services/Llm/Tools/INooseiTool.cs (Registry + Werkzeugvertrag), NOOSE-Website/Services/Llm/NooseiGateway.cs (Werkzeug-Schleife, Kontingent, Protokoll), NOOSE-Website/Services/Llm/NooseiChatService.cs + NooseiSchemas.cs, NOOSE-Website/Components/Pages/Ki/NooseiChatPanel.razor (Chat-UI für die Bestätigungskarte), Schreibdienste liegen fertig vor: Services/FollowupService.cs, Services/JobService.cs, Services/TagService.cs, Services/WatchlistService.cs, Services/LinkService.cs, Guards in Services/Permission.cs

**Achtung.** Beruehrt die Architektur-Grenze "NOOSEI liest, Agenten schreiben" (siehe `claude-memory/noosei.md`). Der Vorschlagsweg wahrt sie formal, aber jede vorgeschlagene Aenderung braucht ihre eigene Rechtepruefung beim Ausfuehren - nicht beim Vorschlagen.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Der Bruch zwischen Erkenntnis und Handlung ist richtig erkannt und der Rueckfrage-Entwurf sicherheitstechnisch elegant geloest, aber es ist grosser Aufwand fuer ein Werkzeug, das taeglich nur wenige oeffnen.
- *Architektur-Passung (5/10):* Die Vorschlags-statt-Schreiben-Idee respektiert die Guards sauber und das Werkzeug-Register ist erweiterbar, durchbricht aber den ausdrücklich festgeschriebenen Grundsatz "Schreibwerkzeuge existieren nicht" und verlangt eine neue Ergebnis- und Kartenmechanik im Chat.
- *Aufwand und Dauerlast (3/10):* Schreibvorschläge öffnen eine neue Werkzeugklasse, die dauerhaft gegen Modellverhalten nachjustiert werden muss, und jedes weitere Werkzeug vergrößert die Fläche erneut.

</details>

---

## 2. Vorgemerkt (31)

Gute Ideen, die nicht verworfen sind - die Entscheidung wurde vertagt. Vier Buendel darin gehoeren
zusammengebaut, wenn sie drankommen:

- **Discord-Bot + Rollen-Abgleich + Bewerber-Nachrichten** - alle drei brauchen denselben Bot-Zugang.
  Dreimal einzeln zu bauen hiesse, den Zugang dreimal zu bauen.
- **Persoenliche Wiedervorlagen-Seite + Wiedervorlage-Rueckstand der Fuehrung** - ein Dienst, zwei Sichten.
- **Einarbeitungsplan mit Pate** - erst sinnvoll, wenn die Einarbeitungs-Checkliste aus dem
  Handbuch-Auftrag laeuft und sich zeigt, ob sie allein reicht.
- **Dienstanweisungen mit Kenntnisnahme** - erst entscheiden, wenn das Handbuch steht; die beiden
  liegen nah beieinander (das Handbuch erklaert, eine Dienstanweisung ordnet an).

### Fahndungsmeldung als Steckbrief-Karte

**Aufwand:** klein | **Jury-Schnitt:** 8.7 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Statt des generischen Einzeilers postet eine neue oeffentliche Fahndung eine richtige Karte ins Discord: Foto, Name, Aktenzeichen, Kopfgeld, Warnhinweise, Link.

**Warum.** Die wichtigste Aussennachricht der Behoerde sieht im Discord am unscheinbarsten aus - ein Satz plus Adresse. Spieler scrollen daran vorbei, und die Fahndung erreicht die Leute nicht, die sie erfuellen sollen.

**Setzt an bei.** Das Karten-Format ist schon zweimal gebaut und muss nur nachgenutzt werden: PushPersonnelEntryAsync und PushTerminationAsync in NOOSE-Website/Services/DiscordWebhookService.cs (Felder, Farbe, Fusszeile, SendEmbedAsync). Alle Inhalte liegen fertig auf einer Zeile: NOOSE-Website/Data/Entities/Public/OeffentlicheFahndung.cs (AnzeigeName, Aktenzeichen, Foto, Art) plus Kopfgeld aus NOOSE-Website/Services/Public/BountyService.cs und Warnhinweise aus NOOSE-Website/Data/Entities/Public/FahndungWarnhinweis.cs. Die Kategorien PublicWantedPublished und PublicWantedBountyRaised sind bereits routingfaehig (NOOSE-Website/Models/Common/DiscordWebhookModels.cs).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (8/10):* Die wichtigste Aussennachricht der Behoerde sieht ausgerechnet am unscheinbarsten aus; eine Karte mit Foto und Kopfgeld entscheidet darueber, ob die Fahndung die Spieler erreicht, die sie erfuellen sollen.
- *Architektur-Passung (9/10):* DiscordWebhookService baut für zwei Kategorien bereits Embeds, PublicWantedPublished ist ausdrücklich routingfähig, und Foto, Aktenzeichen, Kopfgeld und Warnhinweise liegen am Publikations-Snapshot — ein zweiter Embed-Komponist, sonst nichts.
- *Aufwand und Dauerlast (9/10):* Nur ein reichhaltigeres Embed im vorhandenen Webhook-Pfad, keine neue Infrastruktur und keinerlei Folgeaufwand.

</details>

### Wiedervorlagen planen statt nur abarbeiten

**Aufwand:** klein | **Jury-Schnitt:** 7.7 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Eine eigene Seite für die eigenen Wiedervorlagen mit den Rubriken überfällig / heute / diese Woche / später, dazu Verschieben um einen Klick (+1 Tag, +1 Woche) und Erledigen direkt aus der Liste.

**Warum.** Wiedervorlagen sind der wichtigste persönliche Merkzettel im System, aber sie tauchen nur an zwei Stellen auf: in der Akte selbst und als »heute fällig«-Kachel auf dem Dashboard. Was nächste Woche ansteht, sieht niemand, und zum Verschieben muss man die Akte öffnen, den richtigen Abschnitt finden und den Dialog aufmachen.

**Setzt an bei.** NOOSE-Website/Services/FollowupService.cs hat mit GetMyDueAsync bereits die eigene, sichtbarkeitsgefilterte Auflösung auf Name und Verweis — es fehlt nur eine Variante ohne Fälligkeits-Schranke und mit Zeitfenster; CompleteAsync/RefreshAsync/ReopenAsync sind da. Dialoge und Darstellung bestehen: NOOSE-Website/Components/Common/Shared/FollowupPanel.razor und FollowupDialog.razor. Die Kachel auf NOOSE-Website/Components/Pages/Home.razor verlinkt dann dorthin. Fälligkeits-Push läuft bereits über NOOSE-Website/Infrastructure/ (FollowupDueWorker).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Billig und sinnvoll fuer die, die Wiedervorlagen wirklich nutzen, aber es erschliesst keine neue Nutzergruppe — wer sie heute verrotten laesst, tut es auch mit einer huebscheren Liste.
- *Architektur-Passung (9/10):* FollowupService hat Fälligkeit, Erledigen und GetMyDueAsync schon; es fehlt eine zweite Abfrage mit Zeitkübeln und eine Seite — kein Schema, kein neuer Mechanismus.
- *Aufwand und Dauerlast (9/10):* Eine Seite mit vier Rubriken über einen fertigen Dienst, kein neues Datenmodell und keinerlei laufender Betrieb.

</details>

### Begründungspflicht bei kritischen Änderungen

**Aufwand:** klein | **Jury-Schnitt:** 7.3 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Herabstufen einer Einstufung, Aufheben einer Verschlusssache, Löschen einer Akte und Aufheben einer Fahndung verlangen einen Grund, der als eigene Protokollzeile an der Akte landet — nicht nur als nackter Feldwechsel.

**Warum.** Das Änderungsprotokoll zeigt 'Einstufung: Verdachtsfall → Prüffall' ohne jedes Warum. Für die Dienstaufsicht ist genau das Warum die eigentliche Information; ohne sie lässt sich eine Herabstufung weder nachvollziehen noch beanstanden. Der Begründungsdialog existiert bereits, hängt aber nur an Personal-, Geld- und Ticket-Aktionen, nie an Aktenänderungen.

**Setzt an bei.** Components/Common/Shared/ReasonDialog.razor inklusive MandatoryReason — heute verdrahtet in Components/Pages/Admin/Agents.razor, Admin/Shares.razor, Financing/FinancingRequestDetail.razor, Tickets/TicketsDetail.razor und Common/Shared/BountyPanel.razor; Services/ManualAudit.cs (Row und Change im {Feld:[alt,neu]}-Format) für die Protokollzeile gegen die Akte; Infrastructure/Audit/AuditRedaction.cs, falls ein Grund nicht ins für alle lesbare /nachweis darf; Services/TimelineService.cs und Services/TimelineDisplay.cs für die Anzeige; Einbauorte sind Components/Pages/People/Shared/ClassificationPanel.razor, Data/Entities/People/ClassificationHistory.cs und Components/Pages/People/Shared/WantedPanel.razor.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Das 'Warum' ist bei einer Herabstufung tatsaechlich die eigentliche Information und der Dialog existiert schon — es ist aber auch zusaetzliche Reibung genau an den Stellen, die ohnehin selten passieren.
- *Architektur-Passung (8/10):* ReasonDialog wird an elf Stellen schon so benutzt und ManualAudit.Row ist der dokumentierte Weg, eine Begründungszeile gegen die Akte zu schreiben; es sind nur vier Schreibpfade, durch die der Grund durchgereicht werden muss.
- *Aufwand und Dauerlast (9/10):* Ein vorhandener Begründungsdialog an vier Stellen plus je eine Protokollzeile — minimaler Bau, danach gar keine Pflege und ein spürbarer Gewinn für die Dienstaufsicht.

</details>

### QR-Code auf dem Fahndungsposter

**Aufwand:** klein | **Jury-Schnitt:** 7.3 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Das Druckposter bekommt einen QR-Code und einen kurzen Merk-Link, der direkt zum Steckbrief fuehrt - inklusive Hinweisformular.

**Warum.** Poster werden im Spiel aufgehaengt und abfotografiert. Danach bricht die Spur ab: niemand tippt ein Aktenzeichen wie NOOSE-FA-2026-0042 von Hand ab, also kommt auch kein Buergerhinweis zurueck.

**Setzt an bei.** Das Poster existiert als eigene Druckseite: NOOSE-Website/Components/Pages/Public/WantedPoster.razor (/gesucht/{CaseNumber}/druck) mit eigenem Layout und Druckknopf. Ziel-Adresse und Aktenzeichen stehen dort bereits zur Verfuegung (_entry.CaseNumber), die Basis-Adresse kommt aus SystemSettingKeys.SiteBaseUrl (NOOSE-Website/Models/Common/SystemConfiguration.cs). Im Repo gibt es bislang keinerlei QR-Erzeugung - der Code kann serverseitig als SVG erzeugt und direkt eingebettet werden, ohne neue JS-Datei. Als Modul-Schalter existiert PublicModules.WantedPrint bereits (NOOSE-Website/Services/Public/PublicModules.cs).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Poster werden ingame aufgehaengt und abfotografiert, und genau dort bricht heute die Spur ab — niemand tippt ein Aktenzeichen ab, ein QR-Code kostet fast nichts und schliesst den Kreis zum Buergerhinweis.
- *Architektur-Passung (7/10):* Die Posterseite existiert und self-hosted Bibliotheken unter wwwroot/lib mit ?v=-Disziplin sind das etablierte Muster; neu sind eine QR-Erzeugung und ein Kurz-Link, der die 404-Einheitsantwort der öffentlichen Fahndung nicht aufweichen darf.
- *Aufwand und Dauerlast (8/10):* Eine selbst gehostete Generator-Bibliothek und eine Kurzlink-Route, danach entsteht der Code bei jedem Druck von allein.

</details>

### Anordnungen und Beschluesse zum Vorzeigen

**Aufwand:** mittel | **Jury-Schnitt:** 7.3 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Durchsuchungs-, Beschlagnahme- und Festnahmeanordnungen: Ein Agent beantragt, die Fuehrung zeichnet, das Papier bekommt ein Aktenzeichen, eine Gueltigkeitsdauer und eine Druckansicht, die man im Rollenspiel vorzeigen kann. Rechtsgrundlage wird aus dem Gesetzbuch verknuepft, Widerruf und Ablauf sind sichtbar.

**Warum.** NOOSE handelt im Rollenspiel ohne jedes Papier. Es gibt nichts, was man einem Anwalt, dem DoJ oder einem Fraktionsspieler vorlegen koennte - und damit auch keinen Hebel fuer die schoenste Sorte Konflikt-Rollenspiel. Das Gesetzbuch ist da, nur ohne Akt, der sich darauf beruft.

**Setzt an bei.** Antrag/Entscheidung inkl. Posteingang liegt fertig in NOOSE-Website/Data/Entities/Requests/Request.cs + NOOSE-Website/Models/Enums/RequestType.cs und RequestStatus.cs. Aktenzeichen ueber NOOSE-Website/Services/CaseNumberService.cs (neues Praefix, Transaktion beachten). Rechtsgrundlage: NOOSE-Website/Data/Entities/Common/Law.cs, Anzeige NOOSE-Website/Components/Pages/Laws/LawView.razor. Druckstueck ueber NOOSE-Website/Components/Common/Shared/PrintFrame.razor + NOOSE-Website/Components/Layout/PrintLayout.razor. Guard: Permission.RequireLeadershipNoReader in NOOSE-Website/Services/Permission.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (8/10):* Das ist der einzige Vorschlag, der RP fuer Leute ausserhalb der NOOSE erzeugt: ein vorzeigbarer Beschluss gibt Anwaelten, DoJ und Fraktionsspielern etwas zum Anfechten, und macht aus dem toten Gesetzbuch endlich einen Hebel.
- *Architektur-Passung (8/10):* Aktenzeichen über CaseNumberCounter, Antrag/Entscheidung über RequestType + Posteingang, Gültigkeitsablauf wie PublicWantedExpiryWorker, Druckansicht über PrintFrame, Gesetzes-Verknüpfung über LinkService — ein neuer Aktentyp, aber jeder Mechanismus hat eine fertige Schablone.
- *Aufwand und Dauerlast (6/10):* Neue Entität mit Workflow, Aktenzeichen, Ablauf und Druckansicht ist solide Arbeit, danach erzeugt das Rollenspiel den Inhalt selbst und es bleibt nichts zu pflegen.

</details>

### Akte gezielt in Kanal teilen

**Aufwand:** klein | **Jury-Schnitt:** 7 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Ein Teilen-Knopf an Akte, Vorgang oder Fahndung, der einen kurzen Hinweis samt Link in einen auswaehlbaren Discord-Kanal postet - mit Sperre fuer Verschlusssachen.

**Warum.** Ein Agent, der Kollegen auf einen Fall aufmerksam machen will, kopiert heute die Adresse von Hand ins Discord. Dabei geht der Zusammenhang verloren, und Verschlusssachen landen im falschen Kanal, weil niemand es prueft.

**Setzt an bei.** Der Versand-Weg ist fertig und wird bislang nur intern genutzt: IDiscordWebhookService.PushCustomAsync (NOOSE-Website/Services/IDiscordWebhookService.cs, Umsetzung in DiscordWebhookService.cs) postet freien Text plus Link in die konfigurierte Kategorie. Die Kanaele sind bereits pflegbar (NOOSE-Website/Components/Pages/Admin/Shared/DiscordPanel.razor). Ob eine Akte geteilt werden darf, beantworten vorhandene Helfer: NOOSE-Website/Services/Visibility.cs, NOOSE-Website/Services/RecordVisibility.cs und NOOSE-Website/Services/Permission.cs. Titel und Symbol des Akten-Typs liefert NOOSE-Website/Services/RecordTypeDisplay.cs, der Knopf passt in NOOSE-Website/Components/Common/Shared/PageHeader.razor (Actions).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Billig, trifft eine alltaegliche Handlung (Kollegen auf einen Fall stossen) und die VS-Sperre verhindert genau den Fehler, den heute niemand prueft, wenn er die Adresse von Hand kopiert.
- *Architektur-Passung (7/10):* Die Webhook-Konfiguration ist eine Kategorie→URL-Abbildung, aus der sich eine Kanalauswahl bauen lässt, und die VS-Sperre kommt aus Visibility bzw. DocumentViewerScope — neu ist, dass eine Handnachricht am Kategorie-Routing vorbei einen eigenen Konfigurationseintrag braucht.
- *Aufwand und Dauerlast (8/10):* Knopf plus Kanalauswahl auf der vorhandenen Webhook-Konfiguration; einzige bleibende Sorgfaltspflicht ist die VS-Sperre am Teilen-Pfad.

</details>

### Partner-Freigabe auf Zeit

**Aufwand:** mittel | **Jury-Schnitt:** 6.7 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Beim Freigeben einer Akte an LSPD/DoJ/LSMD lässt sich ein Enddatum setzen. Ein Sweeper zieht die Freigabe danach automatisch zurück und schreibt eine Protokollzeile.

**Warum.** Eine Freigabe für einen einzelnen gemeinsamen Fall gilt heute ewig – zurücknehmen muss jemand von Hand, und daran denkt niemand. Über Monate wächst so ein stiller Dauerzugriff Fremder auf Akten. Das Ablauf-Muster inklusive Worker gibt es für öffentliche Ausschreibungen schon.

**Setzt an bei.** NOOSE-Website/Data/Entities/Common/PartnerShare.cs (kein Gültig-bis-Feld vorhanden), NOOSE-Website/Services/PartnerShareService.cs + IPartnerShareService.cs, NOOSE-Website/Services/PartnerVisibility.cs (eine Stelle für das Lesegatter), NOOSE-Website/Infrastructure/Public/PublicWantedExpiryWorker.cs (Ablauf-Worker als Bauplan, inkl. der Regel "Lesepfad filtert selbst nach Datum, der Worker räumt nur auf"), NOOSE-Website/Components/Common/Shared/PartnerShareDialog.razor (UI-Anker), NOOSE-Website/Services/ManualAudit.cs

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (3/10):* Richtige Hygiene gegen stillen Dauerzugriff Fremder, aber vollstaendig unsichtbar: niemand wird jemals bemerken, dass dieses Feature existiert.
- *Architektur-Passung (9/10):* Eine Gültig-bis-Spalte an PartnerShare, ein Sweeper genau wie PublicWantedExpiryWorker und eine ManualAudit-Zeile; gelesen wird die Freigabe ohnehin zentral über PartnerVisibility, also genügt ein zusätzliches Prädikat.
- *Aufwand und Dauerlast (8/10):* Ablaufdatum plus Sweeper nach einem im Haus schon erprobten Muster; senkt die Dauerlast sogar, weil niemand mehr händisch zurücknehmen muss.

</details>

### Dienstausweis und Markenregister

**Aufwand:** mittel | **Jury-Schnitt:** 6.7 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Die Dienstnummer wird nach Schema vergeben statt frei getippt, daraus entsteht ein druckbarer Dienstausweis mit Wappen, Lichtbild, Dienstgrad und Gueltigkeit. Eine oeffentliche Pruefseite /ausweis/{Nummer} sagt Buergern nur: gibt es, Codename, Dienstgrad, gueltig ja/nein.

**Warum.** Agent.BadgeNumber ist heute ein Freitextfeld ohne jede Funktion - es kann doppelt sein, leer sein, erfunden sein. Im Rollenspiel kann niemand pruefen, ob jemand wirklich NOOSE ist, was die Behoerde beliebig macht. Der Ausweis ist das sichtbarste Behoerden-Ritual ueberhaupt.

**Setzt an bei.** Feld und Lichtbild stecken schon in NOOSE-Website/Data/Entities/Agent.cs (BadgeNumber, AvatarFileName, PendingBadgeNumber). Druckmuster inkl. Klarnamen-Gate: NOOSE-Website/Components/Pages/Personnel/PersonnelFilePrint.razor. Bildauslieferung ueber NOOSE-Website/Infrastructure/Storage/AgentAvatarStorageService.cs. Die Pruefseite braucht einen Modul-Schalter in NOOSE-Website/Services/Public/PublicModules.cs und einen Eintrag in PublicRoutes.cs. Namensanzeige strikt ueber NOOSE-Website/Services/AgentNameDisplay.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* 'Ausweis bitte' ist ein taeglicher RP-Moment, und die oeffentliche Pruefseite macht Behoerdenidentitaet erstmals faelschungssicher — die Nummernvergabe nach Schema raeumt nebenbei ein kaputtes Freitextfeld auf.
- *Architektur-Passung (7/10):* BadgeNumber inklusive Pending-Änderungsworkflow und freigegebenes Profilbild liegen bereit, Schema-Vergabe passt zu CaseNumberCounter, und die öffentliche Prüfseite kann exakt das Muster der anonymen 404-Endpoints und Modul-Schalter übernehmen.
- *Aufwand und Dauerlast (6/10):* Vergabeschema und Druckblatt laufen danach von allein, aber die Umstellung der bestehenden Freitext-Dienstnummern und der anonyme Prüf-Endpoint (Rate-Limit, Existenz-Orakel) kosten Sorgfalt.

</details>

### Kalender-Abo für Handy und Discord

**Aufwand:** mittel | **Jury-Schnitt:** 6.7 | **Blickwinkel:** Vorhandene Infrastruktur als Hebel

**Was es tut.** Ein persönlicher, per Geheim-Link geschützter ICS-Feed, den jeder Agent in Google-/Apple-Kalender abonnieren kann – mit genau den Terminen, Besprechungen und Fristen, die er auch auf der Seite sehen darf.

**Warum.** Die Nutzer sind im Spiel und auf Discord, nicht auf der Website. Eine Besprechung wird verpasst, weil der Kalender nur auf /kalender existiert. Der Aggregator samt Sichtbarkeitsprüfung über alle datierten Aktentypen ist fertig – es fehlt nur ein zweites Ausgabeformat.

**Setzt an bei.** NOOSE-Website/Services/CalendarService.cs + ICalendarService.cs (GetEntriesAsync liefert bereits das sichtbarkeitsgefilterte Fenster über alle Quellen), NOOSE-Website/Components/Common/StatisticsExportEndpointRouteBuilderExtensions.cs (Muster für einen Minimal-API-Exportendpunkt mit Policy und Zugriffsprotokoll), NOOSE-Website/Infrastructure/Export/CsvHelper.cs (Schwesterformat), NOOSE-Website/Program.cs (Map*Endpoints-Block), NOOSE-Website/Data/Entities/Agent.cs (Feld für das Abo-Token), NOOSE-Website/Infrastructure/Meetings/MeetingReminderWorker.cs (zeigt, welche Termine relevant sind)

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Verpasste Besprechungen sind ein Dauerthema, weil der Termin nur auf einer Seite lebt, die niemand taeglich oeffnet — der Aggregator ist fertig, es fehlt ein zweites Ausgabeformat (inhaltsgleich mit Idee 61).
- *Architektur-Passung (8/10):* ICalendarService.GetEntriesAsync ist genau der sichtbarkeitsgeprüfte Aggregator und die Minimal-API-Endpoints samt partitioniertem Rate-Limit sind das Muster; neu sind ein handgeschriebener ICS-Serializer und ein Geheim-Token als zweiter, cookie-freier Zugangsweg.
- *Aufwand und Dauerlast (5/10):* Zweites Ausgabeformat auf einem fertigen Aggregator, aber ein Geheim-Link ist ein dauerhaft gültiger Datenabfluss, der Widerruf, Rotation und Abruf-Deckelung braucht.

</details>

### Einsatztagebuch am laufenden Einsatz

**Aufwand:** mittel | **Jury-Schnitt:** 6.3 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Solange eine Operation auf 'Laufend' steht, schreiben die Beteiligten kurze Meldungen mit Zeitstempel, Art (Lagemeldung, Anforderung, Ergebnis) und Ort hinein. Alle Beteiligten sehen die Zeilen live; nach Abschluss ist das Protokoll der Ablauf-Text der Akte.

**Warum.** Operation.Expiry ('Ablauf') und Result werden heute hinterher aus dem Gedaechtnis getippt. Waehrend des Einsatzes ist die Operationsakte nutzlos, obwohl genau dann etwas passiert. Ein mitlaufendes Protokoll macht die Seite zum Werkzeug statt zur Ablage und liefert den Ablauf-Text gratis.

**Setzt an bei.** Entitaet und Live-Push sind eine direkte Kopie von NOOSE-Website/Data/Entities/Taskforces/TaskforceMessage.cs + NOOSE-Website/Infrastructure/Chat/TaskforceChatBroadcaster.cs. Einbau als weiterer Abschnitt in NOOSE-Website/Components/Pages/Operations/OperationDetail.razor (RecordSectionRail). Eingabefeld inkl. @-Erwaehnungen und Bild-Paste: NOOSE-Website/Components/Common/Shared/MentionInput.razor. Zielfeld beim Abschluss: Ablauf in NOOSE-Website/Data/Entities/Operations/Operation.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (4/10):* Waehrend eines laufenden Einsatzes tabbt niemand raus, um Lagemeldungen zu tippen; realistisch wird es doch wieder hinterher befuellt und ist dann nur ein umstaendlicherer Ergebnis-Text.
- *Architektur-Passung (8/10):* Das ist der Taskforce-Chat (TaskforceMessage + TaskforceChatBroadcaster + Live-Panel) auf eine bestehende Entität kopiert, deren Ablauf-Feld schon existiert — bewährte Mechanik, nur eine neue Kind-Tabelle plus Zeitstrahl-Eintrag in TimelineService.
- *Aufwand und Dauerlast (7/10):* Praktisch dieselbe Mechanik wie der Taskforce-Chat samt Broadcaster-Muster, nur an die Operation gehängt — kein neuer Betriebsaufwand, keine Pflege, nur Nutzungsrisiko.

</details>

### Discord-Rollen automatisch abgleichen

**Aufwand:** mittel | **Jury-Schnitt:** 6 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Rang, Admin-, TRU- und HRB-Kennzeichen eines Agenten werden auf die passenden Discord-Rollen uebertragen - bei Freigabe, Befoerderung, Kuendigung und zusaetzlich einmal taeglich als Nachlauf.

**Warum.** Rang und Rollen werden heute doppelt gepflegt: einmal auf der Seite, einmal von Hand im Discord. Dadurch driftet beides auseinander, und gekuendigte Agenten behalten im Discord oft wochenlang ihre Rolle.

**Setzt an bei.** Die Spalte fuer genau das ist schon da, aber tot: Agent.DiscordRolesSyncAt / DiscordRollenSyncAm (NOOSE-Website/Data/Entities/Agent.cs:86) wird nirgends gelesen oder geschrieben. Rang/Flags liegen auf demselben Datensatz, die Aenderungs-Einstiegspunkte sind AgentManagementService.ReleaseAsync/Befoerderung/Kuendigung (NOOSE-Website/Services/AgentManagementService.cs). Rollen-IDs werden im Admin-Bereich bereits gepflegt (NOOSE-Website/Models/Common/DiscordWebhookModels.cs, DiscordRouting.DefaultRole; NOOSE-Website/Components/Pages/Admin/Shared/DiscordPanel.razor). Ein Tages-Nachlauf passt als Worker nach dem Muster von NOOSE-Website/Infrastructure/Gamification/TopAgentAwardWorker.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Beendet eine doppelte Handpflege, die zuverlaessig auseinanderdriftet, und alle sehen taeglich das Ergebnis in der Mitgliederliste — gekuendigte Agenten mit Rolle sind ausserdem ein echtes Sicherheitsproblem.
- *Architektur-Passung (7/10):* Die Spalte DiscordRollenSyncAm liegt seit der allerersten Migration unbenutzt auf Agent, Freigabe/Beförderung/Kündigung sind klare Einsprungpunkte und Tages-Worker sind Standard — neu ist allein der Bot-Token samt Discord-REST-Zugriff neben den bisherigen Einweg-Webhooks.
- *Aufwand und Dauerlast (4/10):* Braucht einen Bot mit Rollenrecht, eine Rollen-Zuordnung, die bei jeder Umbenennung nachgezogen wird, und einen täglichen Abgleich, der still scheitert, wenn die Hierarchie nicht stimmt.

</details>

### Wiedervorlage-Rückstand für die Führung

**Aufwand:** mittel | **Jury-Schnitt:** 6 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Eine Führungsansicht über alle offenen und überfälligen Wiedervorlagen — nach Agent, Alter und Akte sortierbar — mit Umhängen auf einen anderen Agenten und automatischer Eskalation an die Führung nach N Tagen Überfälligkeit.

**Warum.** FollowupService.GetMyDueAsync zeigt jedem nur seine eigenen Wiedervorlagen. Wer seine verrotten lässt, fällt niemandem auf. Die Statistik liefert zwar eine Pünktlichkeitsquote als Kurve, aber keine Liste der konkreten Rückstände, auf die man handeln könnte.

**Setzt an bei.** Services/FollowupService.cs (GetMyDueAsync als Vorlage, nur ohne die Eigen-Einschränkung) und Data/Entities/Common/Followup.cs mit der Spalte ZustaendigerAgentId; Infrastructure/Followups/FollowupDueWorker.cs macht den Fälligkeitslauf samt Benachrichtigung schon und bekommt nur eine Eskalationsstufe dazu; Services/Statistics/ThroughputStatisticsService.cs GetFollowupPunctualityAsync als Kennzahl darüber; Services/AgentSelection.cs (OnlySelectable) für das Umhängen; Components/Common/Shared/FollowupPanel.razor + FollowupDialog.razor.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (3/10):* Reines Nachhalte-Werkzeug fuer die Fuehrung — es macht den Alltag der meisten Agenten eher unangenehmer statt besser.
- *Architektur-Passung (8/10):* Followup trägt Zuständigen und Fälligkeit bereits, die Eskalation kann im vorhandenen FollowupDueWorker mitlaufen, und die Ansicht ist eine Führungs-Variante der schon existierenden Abfrage — nur Umhängen und Schwellwert-Konfiguration kommen neu dazu.
- *Aufwand und Dauerlast (7/10):* Eine andere Abfrage auf vorhandene Daten plus Umhängen; nur die automatische Eskalation will so gedeckelt sein, dass sie nicht zur Dauerbenachrichtigung wird.

</details>

### Einsatzalarm mit Rueckmeldung

**Aufwand:** mittel | **Jury-Schnitt:** 5.7 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Die Fuehrung (oder jeder Agent bei Gefahr im Verzug) loest einen Alarm aus: Discord-Ping an @NOOSE, Glocke an alle, dazu eine Seite mit 'Ich ruecke aus / bin unterwegs / kann nicht'. Der Zaehler laeuft live mit; beendet wird der Alarm in eine Operation ueberfuehrt.

**Warum.** Es gibt keinerlei Mittel, kurzfristig Leute zusammenzurufen. Das passiert heute komplett im Discord, hinterlaesst keine Spur, und niemand weiss hinterher, wer ausgerueckt ist. Genau dieser Moment ist der Kern von Behoerden-Rollenspiel.

**Setzt an bei.** Rollen-Ping und Kategorie-Routing stehen in NOOSE-Website/Services/DiscordWebhookService.cs; neue Zeile in NOOSE-Website/Models/Enums/NotificationType.cs (Vorbild: AppointmentScheduled, routebar). Glocke ueber NOOSE-Website/Infrastructure/Notifications/NotificationBroadcaster.cs. Empfaenger-Snapshot und Quittierung exakt wie NOOSE-Website/Data/Entities/Announcements/AnnouncementAcknowledgment.cs + NOOSE-Website/Infrastructure/Announcements/AcknowledgmentBroadcaster.cs. Uebergabe in NOOSE-Website/Data/Entities/Operations/Operation.cs (Status Running).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Der Moment ist RP-Kern, aber wer mitten im Spiel ist, reagiert auf den Discord-Ping und nicht auf eine Rueckmeldeseite — der Mehrwert gegenueber einem @NOOSE im Kanal ist vor allem das Protokoll hinterher, nicht das Zusammenrufen selbst.
- *Architektur-Passung (7/10):* Jeder Einzelbaustein ist da (NotificationType + DiscordRouting, Glocken-Fanout, Singleton-Broadcaster für den Live-Zähler, OperationService für die Überführung), aber es kommt eine eigene Entität samt neuer Nav-Fläche und Echtzeit-Ablauf hinzu.
- *Aufwand und Dauerlast (5/10):* Einmal gebaut läuft die Mechanik allein, aber ein Ping-Knopf für alle ist missbrauchsanfällig, und wenn er zweimal ohne Resonanz gedrückt wird, ist das Feature tot.

</details>

### Nachrichten-Feeds fuer Presse und Warnungen

**Aufwand:** klein | **Jury-Schnitt:** 5.7 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Presse, Warnungen, Lageberichte und neue Fahndungen bekommen je einen Feed zum Abonnieren - Discord und andere Server-Webseiten koennen sie ohne Zutun der NOOSE einbinden.

**Warum.** Heute sieht Aussenstehende nur, wer die Seite von sich aus aufruft. Andere Fraktionen und Server-Kanaele koennen NOOSE-Meldungen nicht automatisch spiegeln, also verbreitet sich eine Warnung nur so weit, wie jemand sie haendisch weitertraegt.

**Setzt an bei.** Alle vier Quellen sind fertig, oeffentlich und mit Veroeffentlichungsdatum versehen: NOOSE-Website/Services/Public/PressReleaseService.cs, PublicWarningService.cs, PublicReportService.cs, PublicWantedService.cs. Welche Routen oeffentlich sind, steht zentral in NOOSE-Website/Services/Public/PublicRoutes.cs, die Modul-Schalter in PublicModules.cs - ein neuer Feed haengt sich an denselben Schalter und an den Not-Aus. Die Indexierungs-Regel und robots.txt muessen mitgezogen werden (NOOSE-Website/Infrastructure/Public/PublicIndexingMiddleware.cs, NOOSE-Website/wwwroot/robots.txt).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (1/10):* Auf einem GTA-RP-Server abonniert niemand RSS — fuer die Verbreitung an andere Fraktionen sind die vorhandenen Discord-Webhooks bereits der bessere und gebaute Weg.
- *Architektur-Passung (8/10):* Presse-, Warn-, Lagebericht- und Fahndungsdienste liefern fertige Veröffentlichungs-Snapshots, Modul-Schalter und Not-Aus greifen im Dienst, und die Endpoint-Erweiterungsklassen sind das Muster — es fehlt nur ein handgeschriebener XML-Ausgang je Rubrik.
- *Aufwand und Dauerlast (8/10):* Ein paar XML-Endpunkte auf bereits veröffentlichten Schnappschüssen, gut cachebar; nur Modulschalter und Not-Aus müssen sie mitnehmen.

</details>

### Einarbeitungsplan mit Pate

**Aufwand:** mittel | **Jury-Schnitt:** 5.3 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Bei der Freigabe eines neuen Agenten wird automatisch ein Einarbeitungsplan aus den Pflicht-Ausbildungsmodulen angelegt, ein Pate zugewiesen und ein Probezeit-Ende gesetzt. Fortschritt als Ampel in der Personalakte, Erinnerung vor Ablauf, Abschlussvermerk am Ende.

**Warum.** Ausbildungsmodule gibt es, aber nur als Liste, die die Führung im Nachhinein abhakt — ohne Frist, ohne Verantwortlichen, ohne Erinnerung. Neue Agenten fallen durchs Raster, und an das Probezeitende erinnert sich niemand, weil es den Begriff im System gar nicht gibt.

**Setzt an bei.** Services/TrainingModuleService.cs mit Data/Entities/Personnel/TrainingModule.cs und AgentModuleCompletion.cs (Modulkatalog und Abhaken existieren komplett); Components/Pages/Personnel/Shared/ModulesPanel.razor als Anzeigeort; Services/AgentManagementService.cs ReleaseAsync und PromoteApplicantToAgentAsync als Auslöser; Services/FollowupService.cs für Fristen und Erinnerung; Services/PersonnelFileService.cs mit AgentNoteKind.Training/Information für den Abschlussvermerk; Services/DiscordWebhookService.cs für den Ping an den Paten.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Neuzugaenge halten oder verlieren entscheidet ueber die Groesse der Behoerde, und ein Pate mit Probezeit-Ende ist genau der Griff dafuer — betrifft aber immer nur die wenigen Neuen gleichzeitig.
- *Architektur-Passung (6/10):* TrainingModule und AgentModuleCompletion liegen bereit, ReleaseAsync ist der natürliche Auslöser und Erinnerungs-Worker gibt es zuhauf; neu sind Pate, Probezeitende und der Plan als eigener Zustand über den heutigen Abhak-Listen.
- *Aufwand und Dauerlast (5/10):* Automatik plus Fristen sind überschaubar, aber der Pflichtmodul-Katalog will gepflegt sein und das Ganze schläft ein, sobald länger niemand eingestellt wird.

</details>

### Fuhrpark der Behoerde

**Aufwand:** mittel | **Jury-Schnitt:** 5.3 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Der eigene Fahrzeugbestand: Kennzeichen, Typ, Funkrufname, Zustand (im Dienst, Werkstatt, zerstoert), Zuweisung an Agent oder Taskforce samt Uebergabe-Historie. Eine Kennzeichen-Abfrage erkennt ein eigenes Fahrzeug und sagt das, statt es als Verdaechtigen-Treffer zu zeigen.

**Warum.** Fahrzeuge existieren nur an Verdaechtigen (PersonVehicle). Der eigene Bestand liegt in Discord-Nachrichten, also weiss keiner, welcher Wagen frei ist oder wer den Streifenwagen zuletzt hatte. Unterscheidet sich von der Asservatenkammer: dort liegt Beschlagnahmtes im Bestand, hier laeuft Dienstgeraet mit Kennzeichen und Rufnamen herum.

**Setzt an bei.** Kennzeichen-Feld als Vorbild: NOOSE-Website/Data/Entities/People/PersonVehicle.cs. Bestands- und Buchungsmuster inkl. Besitzer-Achse (NOOSE / nameof(Agent) / nameof(Person)) steht fertig in NOOSE-Website/Services/EvidenceService.cs (OwnerTypes, Z. 22-25) und NOOSE-Website/Data/Entities/Evidence/EvidenceEntry.cs. Zuweisungsziel NOOSE-Website/Data/Entities/Taskforces/Taskforce.cs. Auffindbarkeit ueber eine Zeile in NOOSE-Website/Services/Search/SearchCatalog.cs + Provider.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Nuetzlich fuer den Materialverwalter, aber taeglich merken es nur wenige, und der Bestand veraltet genauso schnell wie der Discord-Thread, den er ersetzen soll.
- *Architektur-Passung (7/10):* Formgleich mit der Asservatenkammer (Katalog + Zuweisung + Historie + Präfix-Aktenzeichen), Kennzeichen-Abfrage ist eine SearchCatalog-Zeile plus Provider — solide, aber eine vollständige neue Domäne mit Zuweisungstabellen und Restrict-Fallstricken.
- *Aufwand und Dauerlast (4/10):* Ein weiterer vollständiger Aktentyp, dessen Wert vollständig an manueller Bestandspflege hängt — genau die Sorte Liste, die nach vier Wochen niemand mehr aktualisiert.

</details>

### Vier-Augen für Rechte und harte Aktionen

**Aufwand:** mittel | **Jury-Schnitt:** 5.3 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** TRU-, HRB- und Admin-Rechte sowie Kontolöschungen laufen als Antrag über den bestehenden Posteingang: einer beantragt mit Begründung, ein zweiter entscheidet. Das Antragsmodell kennt bereits vier Typen, hier kommt einer dazu.

**Warum.** Heute kann ein einzelner Admin sich und anderen jedes Recht geben (AdminSetAsync, TruSetAsync, HrbSetAsync) und Konten hart löschen — protokolliert, aber ohne Zweitunterschrift. Für eine Behörde mit ausgearbeiteter Rechteordnung ist das die auffälligste Lücke, und beim Löschen ist sie zusätzlich unumkehrbar.

**Setzt an bei.** Data/Entities/Requests/Request.cs ist exakt dafür gebaut: Typ-Enum plus typ-eigene Zusatzspalten (FreigabeBehoerde, PublicationWantedId, KopfgeldAnteilId) — ein fünfter Block reiht sich ein; Models/Enums/RequestType.cs; Services/RequestService.cs mit GetOpenAsync/DecideAsync/GetOpenCountAsync inklusive Nav-Badge; Components/Pages/Admin/Shares.razor führt den Posteingang mit mehreren Antragsarten bereits vor; Services/IAgentManagementService.cs (AdminSetAsync, TruSetAsync, HrbSetAsync, DeleteAccountAsync) als abzusichernde Ziele; Services/Permission.cs und Authorization/BootstrapAdmins.cs für die Ausnahmen.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (2/10):* Sauber gedacht, aber es ist ein fiktives Amt mit einer Handvoll Admins, die einander vertrauen; im Alltag merkt davon niemand etwas ausser zusaetzlicher Reibung.
- *Architektur-Passung (8/10):* RequestType ist ein vierwertiges Enum hinter einem einzigen Posteingang mit Genehmigen/Ablehnen-Workflow, also exakt der vorgesehene Erweiterungspunkt; zu bedenken bleiben die unentziehbaren Bootstrap-Admins und der Fall, dass nur ein Admin online ist.
- *Aufwand und Dauerlast (6/10):* Nur ein weiterer Typ im vorhandenen Antragsmodell, braucht aber eine durchdachte Notausstiegsregel, damit sich eine kleine Behörde nicht selbst aussperrt.

</details>

### Aktenlisten auf dem Handy

**Aufwand:** mittel | **Jury-Schnitt:** 5.3 | **Blickwinkel:** Alltag des einzelnen Agenten

**Was es tut.** Unterhalb der Handy-Breite werden die Aktenlisten statt als breite Tabelle als kompakte Karten ausgegeben — Name, Aktenzeichen, Einstufung, Gefährdung, Aktualität —, und die Filterleiste klappt in ein Auswahlfeld.

**Warum.** Die Personenliste hat neun Spalten in einem MudDataGrid; auf einem Telefon bleibt davon ein horizontal geschobener Streifen. Genau das ist aber die Nutzung, die die Leute haben: neben dem laufenden Spiel schnell etwas nachschlagen. Das app.css enthält bisher nur zwei Handy-Regeln, und keine davon betrifft die Listen.

**Setzt an bei.** NOOSE-Website/Components/Pages/People/PeopleList.razor und die gleich gebauten Listen für Fraktionen/Gruppen/Parteien/Vorgänge; die Zeilendaten liegen bereits als eigene Row-Klasse vor, aus der sich eine Kartenansicht ohne neue Abfrage rendern lässt. Stilvorlage für eine dichte, responsive Zeile ist die vorhandene .chronik-zeile samt Handy-Regel in NOOSE-Website/wwwroot/app.css (ab Zeile ~1044); Karten-Baustein u. a. NOOSE-Website/Components/Pages/Taskforces/Shared/TaskforceCard.razor. Filter bleiben über NOOSE-Website/Components/Common/Shared/QueryState.cs erhalten.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Das Handy ist die tatsaechliche Nutzungssituation neben dem laufenden Spiel, und neun Spalten in einem horizontal geschobenen Streifen sind dort schlicht unbenutzbar — direkter Multiplikator fuer jede andere Handy-Idee.
- *Architektur-Passung (4/10):* Es gibt keine geteilte Listenkomponente, an der eine Kartenansicht einmal hinge, und app.css hat genau zwei Handy-Breakpoints — die Arbeit fällt in jeder Listenseite erneut an, ohne dass die Architektur irgendeinen Haken dafür anbietet.
- *Aufwand und Dauerlast (5/10):* Die Kartenansicht muss je Liste gebaut und danach bei jeder neuen Spalte mitgepflegt werden — dauerhafte Doppelpflege von Tabelle und Karte.

</details>

### Dienstbuch: Im-Dienst-Status und Schichtprotokoll

**Aufwand:** mittel | **Jury-Schnitt:** 5.3 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Ein Agent meldet sich in der Kopfleiste mit einem Klick zum Dienst an und wieder ab. Wer gerade im Dienst ist, steht live auf dem Lagezentrum; beim Abmelden entsteht automatisch ein Schichteintrag mit Dauer und optionalem Kurzbericht.

**Warum.** Heute weiss niemand, wer gerade tatsaechlich spielbar und erreichbar ist. Abmeldungen (Data/Entities/Absences/Absence.cs) decken nur ganze Kalendertage ab, also genau das Gegenteil. Das Ergebnis: Einsaetze werden im Discord zusammengesucht, und die Seite ist waehrend des Rollenspiels tot.

**Setzt an bei.** Gegenstueck-Modell und Sichtbarkeitsgate: NOOSE-Website/Data/Entities/Absences/Absence.cs + NOOSE-Website/Services/AbsenceVisibility.cs. Live-Push 1:1 nach dem Singleton-Muster von NOOSE-Website/Infrastructure/Chat/TaskforceChatBroadcaster.cs. Schalter gehoert in die AppBar NOOSE-Website/Components/Layout/MainLayout.razor (Z. 15-72, neben NotificationBell). Kachel + Liste aus NOOSE-Website/Components/Common/Shared/StatTile.razor und NOOSE-Website/Components/Pages/Home.razor. Wer ueberhaupt gelistet wird, entscheidet NOOSE-Website/Services/AgentSelection.cs (OnlySelectable).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Beantwortet die haeufigste Frage des Tages ('wer ist gerade spielbar'), konkurriert aber direkt mit dem Discord-Voice-Channel, der dieselbe Antwort ohne Klick liefert — und steht und faellt mit der Disziplin, sich auch wieder abzumelden.
- *Architektur-Passung (5/10):* Broadcaster- und Dashboard-Kachel-Muster tragen die Live-Anzeige, aber ein Schichteintrag mit Dauer und Kurzbericht ist der Zwilling der bereits gebauten Dienst-Aktivitäten (AgentActivity mit Vorlagen und Akten-Verknüpfung), und "Anwesenheit" ist eine Achse, die es im Datenmodell bisher nirgends gibt.
- *Aufwand und Dauerlast (5/10):* Der Code ist überschaubar, aber der Nutzen hängt an der Disziplin der Spieler, vergessene Abmeldungen brauchen einen Aufräum-Worker und das Schichtprotokoll wächst dauerhaft mit.

</details>

### Bewerber per Discord auf dem Laufenden

**Aufwand:** mittel | **Jury-Schnitt:** 5.3 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Bewerber werden bei jedem Schritt - Eingang, Test freigeschaltet, Termin, Entscheidung - direkt per Discord-Direktnachricht benachrichtigt statt nur im Bewerberportal.

**Warum.** Bewerber haben noch keine Gewohnheit, das Portal zu oeffnen; sie warten im Discord. Dadurch verfallen Testfristen und Termine unbemerkt, und Bewerbungen sterben an Schweigen statt an einer Entscheidung.

**Setzt an bei.** Bewerber sind bereits Discord-angemeldete Konten mit Snowflake: AgentStatus.Applicant (NOOSE-Website/Models/Enums/AgentStatus.cs:9) und Agent.DiscordId (NOOSE-Website/Data/Entities/Agent.cs:18) - die Aufloesung existiert schon in DiscordWebhookService.ResolveDiscordIdsAsync. Alle Ausloeser sind vorhanden: NOOSE-Website/Services/BewerbungService.cs, BewerbungTestService.cs, NOOSE-Website/Infrastructure/Recruiting/ und der Ablauf-Waechter NOOSE-Website/Infrastructure/Recruiting/BewerbungTestExpiryWorker.cs. Die Kategorie Recruiting ist bereits routingfaehig, pingt heute aber nur die HRB-Rolle (NOOSE-Website/Models/Common/DiscordWebhookModels.cs). Wichtig: die Schwaerzungsregel fuer Anschreiben nicht umgehen (NOOSE-Website/Services/BewerbungTemplateRenderer.cs). Direktnachrichten brauchen den Bot-Zugang aus Vorschlag 1.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Bewerbungen sterben heute an Schweigen statt an einer Entscheidung, weil Bewerber im Discord warten und nicht im Portal — betrifft zwar wenige Leute gleichzeitig, aber genau die, die die Behoerde wachsen lassen.
- *Architektur-Passung (5/10):* Alle Auslöser liegen in BewerbungService und die DiscordId steht am Bewerber-Konto, aber Direktnachrichten verlangen denselben neuen Bot-Token samt DM-Kanal-Öffnung wie die anderen Bot-Ideen — die vorhandene Webhook-Infrastruktur kann das prinzipiell nicht.
- *Aufwand und Dauerlast (5/10):* Direktnachrichten brauchen einen Bot, eine gemeinsame Gilde und offene DMs — scheitert leise beim Empfänger und muss diese Fehlschläge dauerhaft sichtbar machen.

</details>

### Amtshilfeersuchen an Partnerbehoerden

**Aufwand:** mittel | **Jury-Schnitt:** 5 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Ein foermliches Ersuchen an LSPD, DoJ oder LSMD: Betreff, Rechtsgrundlage, Frist, angehaengte Aktenfreigabe. Die Partnerbehoerde sieht es in ihrem Eingang und antwortet im Faden; Ersuchen und Antwort haengen anschliessend an der Akte.

**Warum.** Partner koennen heute ausschliesslich lesen, was ihnen freigegeben wurde. Es gibt keinen Weg, sie foermlich um etwas zu bitten - das laeuft im Discord und die Antwort landet nie in der Akte. Damit fehlt der gesamte behoerdenuebergreifende Schriftverkehr, der Behoerden-Rollenspiel eigentlich ausmacht.

**Setzt an bei.** Antrags-/Eingangs-Workflow mit Behoerden-Achse existiert: NOOSE-Website/Data/Entities/Requests/Request.cs (FreigabeAgency, FreigabePartnerAgentId) + NOOSE-Website/Models/Enums/RequestType.cs. Freigabe-Mechanik und Live-Push: NOOSE-Website/Data/Entities/Common/PartnerShare.cs + NOOSE-Website/Infrastructure/Shares/SharesBroadcaster.cs. Nachrichtenfaden als Vorbild: NOOSE-Website/Services/Public/TicketService.cs mit NOOSE-Website/Infrastructure/Chat/TicketBroadcaster.cs. Schreibrecht fuer Partner klaert MayContribute() in NOOSE-Website/Authorization/AgentPrincipalExtensions.cs; Behoerdenliste NOOSE-Website/Models/Enums/PartnerAgency.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (5/10):* Behoerdenuebergreifender Schriftverkehr ist inhaltlich stark, haengt aber komplett daran, dass LSPD/DoJ-Spieler diese fremde Seite regelmaessig oeffnen — und das tun sie erfahrungsgemaess nicht.
- *Architektur-Passung (6/10):* Request-Workflow, PartnerShare und polymorphe Verknüpfungen passen, aber Partner-Antworten brauchen eine ausdrückliche Ausnahme im ReadOnlyBarrierInterceptor (wie sie heute nur für Document/Source/TaskforceMessage besteht) und eine neue Fläche in der bewusst kargen Partner-Navigation.
- *Aufwand und Dauerlast (4/10):* Erfordert einen Schreibpfad für strikt lesende Partnerkonten — also eine Ausnahme im Rechtemodell — und steht und fällt damit, ob LSPD/DoJ überhaupt mitspielen.

</details>

### Aktenführer je Akte

**Aufwand:** mittel | **Jury-Schnitt:** 5 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Jede Personen-, Fraktions- und Vorgangsakte bekommt einen zuständigen Agenten plus Vertretung. Veraltungs-Meldungen, Mängel und Wiedervorlagen adressieren dann eine konkrete Person, und jeder Agent bekommt eine Liste 'meine Akten'.

**Warum.** Die Aktualitäts-Ampel produziert eine Liste veralteter Akten, für die sich niemand zuständig fühlt — sieht jeder, macht keiner. Ohne Zuständigkeit ist weder Aktenpflege noch Dienstaufsicht darüber steuerbar, und beim Ausscheiden eines Agenten weiss niemand, was eigentlich sein Bestand war.

**Setzt an bei.** Data/Entities/Common/Followup.cs (Spalte ZustaendigerAgentId) als exaktes Feldmuster; Services/DashboardService.cs GetUpdateNeedAsync liefert die Stale-Liste, die dann nach Zuständigem filterbar wird; Services/AgentSelection.cs OnlySelectable für den Picker (inklusive der FindAsync-Rückfall-Regel aus FollowupDialog/ObservationDialog); Services/NotificationService.cs für den Anstoss; Services/Search/SearchCatalog.cs für die Filterachse in den Listen; Components/Pages/People/PersonEditor.razor und Components/Pages/Factions/FactionEditor.razor als Einbauorte.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (4/10):* Zustaendigkeit ist die Voraussetzung dafuer, dass Aktenpflege ueberhaupt steuerbar wird, verteilt aber vor allem Verpflichtung — und 'meine Akten' ist erst dann motivierend, wenn sich jemand freiwillig meldet.
- *Architektur-Passung (6/10):* Am Vorgang gibt es Fallbearbeiter und Leitung schon (CaseAgent), aber für Person und Fraktion bedeutet das neue Spalten samt Migration, Picker mit der dokumentierten FindAsync-Rückfallregel und Anpassungen an Aktualitäts-Meldungen, Mängeln und Wiedervorlagen.
- *Aufwand und Dauerlast (5/10):* Zwei Felder an drei Aktentypen sind schnell gebaut, aber Zuständigkeiten müssen fortlaufend gepflegt werden und verwaisen bei jedem Abgang — ohne Dienstübergabe hängt das in der Luft.

</details>

### Discord-Bot mit Slash-Befehlen

**Aufwand:** gross | **Jury-Schnitt:** 5 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Ein Interaktions-Endpunkt (POST /discord/interaktionen) nimmt Discord-Slash-Befehle entgegen: /akte, /fahndung, /suche, /abmeldung, /noosei. Der Bot erkennt den Agenten an seiner Discord-ID und antwortet nur mit dem, was dieser Agent auch auf der Seite sehen duerfte.

**Warum.** Heute ist Discord eine Einbahnstrasse: die Seite schickt Nachrichten raus, aber niemand kann aus Discord heraus etwas nachschlagen. Wer im Spiel ist und schnell wissen will, ob eine Person eine Akte hat, muss die Seite oeffnen - und tut es deshalb nicht.

**Setzt an bei.** Agent.DiscordId als Schluessel (NOOSE-Website/Data/Entities/Agent.cs:18) und die Aufloesung dazu existiert schon in NOOSE-Website/Services/DiscordWebhookService.cs (ResolveDiscordIdsAsync). Antworten kommen aus vorhandenen Diensten: ISearchService.QuickSearchAsync (NOOSE-Website/Services/ISearchService.cs), INooseiGateway.AskAsync (NOOSE-Website/Services/Llm/NooseiGateway.cs:62), IAbsenceService.CreateAsync (NOOSE-Website/Services/IAbsenceService.cs). Rechte greifen automatisch, weil sie im Service-Layer sitzen (NOOSE-Website/Services/Permission.cs, NOOSE-Website/Services/ViewerScope.cs) - der Bot muss nur ein echtes ClaimsPrincipal bauen (NOOSE-Website/Authorization/AgentPrincipalExtensions.cs). Endpunkt-Muster und Rate-Limit-Policy sind vorhanden (NOOSE-Website/Components/Common/StatisticsExportEndpointRouteBuilderExtensions.cs, NOOSE-Website/Program.cs Zeile ~389).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (9/10):* Die Nutzer sind den ganzen Tag in Discord und nur selten auf der Seite; ein /akte direkt im Kanal bringt die Datenbank erstmals dorthin, wo tatsaechlich gespielt und geredet wird — grosser Aufwand, aber der einzige echte Strukturbruch in der Liste.
- *Architektur-Passung (4/10):* DiscordId am Agenten und das Endpoint-Muster helfen, aber Ed25519-Signaturprüfung, Bot-Registrierung, verzögerte Interaktionsantworten und vor allem das Erzeugen eines vollwertigen ClaimsPrincipal außerhalb eines Requests sind vier Dinge, für die es im Haus kein einziges Vorbild gibt.
- *Aufwand und Dauerlast (2/10):* Signaturprüfung, Befehlsregistrierung, Bot-Token und eine fremde, sich ändernde Plattform bedeuten dauerhafte Betriebslast — und Antworten landen in einem Kanal, in dem das Rechtemodell nicht mehr gilt.

</details>

### Dienstanweisungen mit Kenntnisnahme

**Aufwand:** mittel | **Jury-Schnitt:** 5 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Nummerierte, versionierte Dienstanweisungen (DA-01, DA-02 ...) mit 'gueltig ab', Aenderungsstand und Pflicht-Kenntnisnahme. Wer eine neue Fassung nicht quittiert hat, bekommt einen Hinweis; die Fuehrung sieht die Quote. Anweisungen sind aus Akten und Anordnungen verlinkbar.

**Warum.** Verhaltensregeln liegen heute als lose Dokumente in der Bibliothek herum - ohne Nummer, ohne Fassung, ohne Nachweis. Niemand kann sagen, welche Fassung gilt oder wer sie gelesen hat, und im Rollenspiel gibt es damit nichts, woran man einen Agenten messen koennte. Das Gesetzbuch ist das Aussenrecht, das hier ist das Hausrecht.

**Setzt an bei.** Struktur Buch/Paragraf/Titel/Text steht fertig in NOOSE-Website/Data/Entities/Common/Law.cs mit Seiten unter NOOSE-Website/Components/Pages/Laws/. Quittierung inkl. Empfaenger-Snapshot und Live-Quote: NOOSE-Website/Data/Entities/Announcements/AnnouncementAcknowledgment.cs + NOOSE-Website/Infrastructure/Announcements/AcknowledgmentBroadcaster.cs. Fassungsvergleich ueber NOOSE-Website/Services/HtmlDiff.cs. Sichtbarkeitsstufen (Fuehrung/TRU/HRB) nach NOOSE-Website/Services/DocumentVisibility.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (4/10):* Ueberlappt stark mit dem Schwarzen Brett samt Pflicht-Quittierung; unterm Strich noch eine Pflichtschleife mehr in einem System, das davon schon reichlich hat.
- *Architektur-Passung (5/10):* Die Pflicht-Quittierung gibt es fertig am Schwarzen Brett (AnnouncementAcknowledgment + GetOpenAcknowledgmentsCountAsync), aber Versionierung mit Fassungsstand ist eine Achse, die im ganzen Datenmodell fehlt, und das Ganze sitzt zwischen Bibliothek und Brett — zwei Bausteinen, die es schon gibt.
- *Aufwand und Dauerlast (6/10):* Das Quittierungsmuster gibt es bereits im Schwarzen Brett, aber Versionierung plus der redaktionelle Dauerauftrag, Anweisungen aktuell zu halten, ist echte laufende Arbeit.

</details>

### Besoldung: monatlicher Soldlauf

**Aufwand:** mittel | **Jury-Schnitt:** 4.7 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Sold je Dienstgrad, einmal im Monat per Knopfdruck ausgeloest: je Agent eine Auszahlung gegen die Kasse, dazu Zulagen (TRU, Fuehrung) und Abzuege bei Fehlzeiten, und eine druckbare Besoldungsmitteilung je Agent.

**Warum.** Es gibt Budget und Kostenerstattung (Finanzierungen), aber keinen wiederkehrenden Sold - also fehlt das regelmaessigste Behoerden-Ritual ueberhaupt und der einzige Grund, warum ein Dienstgrad sich materiell anfuehlt. Die Kasse ist da, die Rangskala ist da, es fehlt der Lauf dazwischen.

**Setzt an bei.** Perioden-Mathematik und Budgetreservierung: NOOSE-Website/Services/FinancingBudgetService.cs + NOOSE-Website/Data/Entities/Financing/FinancingBudgetPeriod.cs und NOOSE-Website/Services/FinancingPeriod.cs. Buchung gegen das Kassenbuch: NOOSE-Website/Data/Entities/Kasse/KassenBuchung.cs, Guard Permission.RequireKassenBookingWrite in NOOSE-Website/Services/Permission.cs, Beleg-Druck NOOSE-Website/Components/Pages/Kasse/KassenBuchungPrint.razor. Saetze je Rang analog FinancingBudgetOverride in NOOSE-Website/Data/Entities/Agent.cs und NOOSE-Website/Models/Enums/Rank.cs; Abzuege aus NOOSE-Website/Data/Entities/Absences/Absence.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (4/10):* Ein schoenes Behoerden-Ritual, aber es passiert einmal im Monat, die Kasse ist Spielgeld ohne Verbindung zur Ingame-Wirtschaft, und ausser der Fuehrung merkt es kaum jemand.
- *Architektur-Passung (7/10):* KassenService.BookAsync mit DbContext-Überladung, FinancingBudgetService mit Rang-Basis und Monatsperiode sowie SituationReportWorker als Monatslauf-Vorbild sind exakt die Teile, die es braucht — es bleibt aber eine eigene Lauf-/Positionsentität samt Rang-Satz-Konfiguration.
- *Aufwand und Dauerlast (3/10):* Geldbuchungen sind korrektheitskritisch, der Lauf braucht Sätze, Zulagen, Abzüge und Sonderfälle (Eintritt, Kündigung, Rückabwicklung) und jeder Fehler erzeugt Handarbeit in der Kasse.

</details>

### Dienst-Schnittstelle fuer den FiveM-Server

**Aufwand:** gross | **Jury-Schnitt:** 4.3 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Eine schreibgeschuetzte Schnittstelle mit eigenen Zugangsschluesseln, ueber die ein Ingame-Terminal Kennzeichen, Namen und Aktenzeichen abfragen kann - mit Kontingent und vollstaendigem Zugriffsprotokoll.

**Warum.** Die Akten sind genau dort nicht abrufbar, wo gespielt wird. Ein Streifenwagen, der ein Kennzeichen prueft, hat keinen Weg zur Datenbank, also bleibt der gesamte Bestand im Spiel ungenutzt.

**Setzt an bei.** Es gibt heute keinerlei maschinelle Schnittstelle - jeder Endpunkt haengt am Anmelde-Cookie (NOOSE-Website/Program.cs Zeilen 502-518). Abfragbar waere direkt Vorhandenes: Kennzeichen aus NOOSE-Website/Data/Entities/People/PersonVehicle.cs (Spalte Kennzeichen), Steckbriefe aus NOOSE-Website/Services/Public/PublicWantedService.cs, Namenssuche ueber ISearchService (NOOSE-Website/Services/ISearchService.cs). Die Rechte-Durchsetzung muss nicht neu gebaut werden, sie sitzt im Service-Layer (NOOSE-Website/Services/Permission.cs, NOOSE-Website/Services/ViewerScope.cs). Rate-Limit-Policies gibt es als Muster in NOOSE-Website/Program.cs (~Zeile 389), Zugriffsprotokollierung in NOOSE-Website/Services/AccessLogService.cs.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Die Akten dort abrufbar zu machen, wo tatsaechlich gespielt wird, waere der groesste denkbare Sprung — der Nutzen haengt aber vollstaendig daran, dass die Server-Entwickler das Ingame-Terminal auch bauen, und das liegt ausserhalb dieses Projekts.
- *Architektur-Passung (4/10):* Kontingent-Mathematik (LlmQuota), partitionierte Rate-Limits und das Zugriffsprotokoll sind echte Vorbilder, aber die gesamte Autorisierung ist ClaimsPrincipal-aus-Login-Cookie — ein Maschinen-Aufrufer braucht eine zweite Authentifizierungsachse und einen synthetischen Akteur, den jeder Visibility- und Permission-Aufruf akzeptieren muss.
- *Aufwand und Dauerlast (2/10):* Schlüsselverwaltung, Kontingente und Protokollierung sind das kleinere Problem — der Nutzen hängt an einem Ingame-Skript, das jemand anders bauen und pflegen muss, und ein geleakter Schlüssel ist ein Datenabfluss.

</details>

### Ortsregister Los Santos mit Lagekarte

**Aufwand:** gross | **Jury-Schnitt:** 4 | **Blickwinkel:** Rollenspiel-Immersion

**Was es tut.** Ein gepflegtes Ortsverzeichnis (Name, Bezirk, Aliasnamen, Position auf einer Los-Santos-Grafik). Die bestehenden Ort-Felder bekommen Autovervollstaendigung darauf, und eine Kartenseite zeigt Observationen, Operationen, Fraktions-Anwesen und bekannte Aufenthaltsorte als Marker.

**Warum.** Ort ist an sechs Stellen freier Text ('vor dem Vanilla', 'Vanilla Unicorn', 'VU') - dadurch ist raeumlich nichts auswertbar und es gibt kein Lagebild. Eine Behoerde ohne Karte fuehlt sich nicht wie eine Behoerde an; Brennpunkte sieht man erst, wenn man sie zeichnen kann.

**Setzt an bei.** Die sechs Freitextfelder [Column("Ort")] in NOOSE-Website/Data/Entities/Operations/Operation.cs, /People/Observation.cs, /Appointments/Appointment.cs, /Meetings/Meeting.cs, /Informants/InformantMeeting.cs, /Abductions/AgentAbduction.cs. Dazu NOOSE-Website/Data/Entities/People/PersonLocation.cs (Text) und das Feld Anwesen in NOOSE-Website/Data/Entities/Factions/Faction.cs. Neue Suchkategorie ueber NOOSE-Website/Services/Search/SearchCatalog.cs + ein Provider. Marker-Rendering und Lazy-Interop nach dem Muster von NOOSE-Website/wwwroot/js/graph.js (inkl. ?v=-Regel).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Eine Karte ist das sichtbarste 'wir sind eine Behoerde'-Element ueberhaupt und macht Brennpunkte erstmals sichtbar, kostet aber viel und liefert nur soweit ein Lagebild, wie alle sechs Ort-Felder ab sofort diszipliniert ueber das Register laufen.
- *Architektur-Passung (4/10):* Der Katalogteil sitzt sauber auf SuggestionType.Location und den Wertelisten auf, aber Koordinaten, Kartengrafik und Marker sind eine komplett neue UI-Technik ohne Bibliothek im Haus (vis-network ist ein Graph, keine Karte) und verlangen ein Backfill von sechs Freitextfeldern.
- *Aufwand und Dauerlast (2/10):* Das teuerste Stück der Liste: Kartengrafik, Koordinatenpflege, Aliaspflege und die Nachbearbeitung von sechs Freitextfeldern sind nie fertig und verrotten schnell.

</details>

### Einbettbares Fahndungs-Fenster

**Aufwand:** mittel | **Jury-Schnitt:** 4 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Ein kleiner Einbettungs-Baustein, den andere Fraktions- oder Server-Webseiten einbinden koennen: zeigt die aktuellen Fahndungen oder die Gefahrenlage-Ampel im NOOSE-Look.

**Warum.** Reichweite endet heute an der eigenen Domain. Wer eine NOOSE-Fahndung auf seiner Seite zeigen will, muss sie abtippen - und was abgetippt ist, wird nie wieder aktualisiert.

**Setzt an bei.** Die Inhalte und ihre Freigabelogik existieren komplett: NOOSE-Website/Services/Public/PublicWantedService.cs, NOOSE-Website/Services/Public/PublicSituationService.cs (Gefahrenlage) und die Modul-Schalter in NOOSE-Website/Services/Public/PublicModules.cs, die ein Abschalten sofort durchgreifen lassen (RequireEnabledAsync). Das Poster-Layout in NOOSE-Website/Components/Pages/Public/WantedPoster.razor und PrintLayout zeigen, dass eine schlanke, rahmenlose Ansicht ohne Interaktivitaet in dieser Codebase schon vorgesehen ist ([ExcludeFromInteractiveRouting]). Die oeffentliche Foto-Auslieferung ist bereits anonym moeglich (NOOSE-Website/Components/Public/PublicWantedFileEndpointRouteBuilderExtensions.cs).

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (3/10):* Setzt voraus, dass andere Fraktionen ueberhaupt eigene Webseiten betreiben und dort NOOSE-Inhalte zeigen wollen — das ist auf den meisten Servern ein Empfaenger, vielleicht zwei.
- *Architektur-Passung (5/10):* Das Board rendert bereits anonym und statisch, sodass der Inhalt trivial ist — dafür müssen Framing-Schutz und Forwarded-Header-/HSTS-Aufbau gezielt für eine Route aufgeweicht werden, und der Not-Aus muss die Einbettung mit abdecken.
- *Aufwand und Dauerlast (4/10):* Ein öffentlicher Einbettungs-Baustein muss dauerhaft mit fremden Seiten kompatibel bleiben, und ob ihn überhaupt jemand einbindet, entscheidet nicht die NOOSE.

</details>

### Taskforce-Chat mit Discord verbinden

**Aufwand:** gross | **Jury-Schnitt:** 3.7 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Jede Taskforce bekommt optional einen Discord-Thread: was dort geschrieben wird, landet im Taskforce-Chat der Seite und umgekehrt. Der Thread ist die Aussenstelle, die Akte bleibt das Archiv.

**Warum.** Der Taskforce-Chat ist die Stelle, an der die Seite am staerksten mit Discord konkurriert - und verliert. Einsatzabsprachen laufen im Discord, tauchen in der Akte nie auf, und beim Nachlesen fehlt spaeter die Haelfte.

**Setzt an bei.** Der Chat samt Live-Verteilung existiert vollstaendig: NOOSE-Website/Services/TaskforceChatService.cs, NOOSE-Website/Services/ITaskforceChatService.cs, NOOSE-Website/Infrastructure/Chat/TaskforceChatBroadcaster.cs. Die Sichtbarkeitsregel, wer eine Taskforce ueberhaupt sieht, liegt zentral in NOOSE-Website/Services/TaskforceVisibility.cs. Ausgehend kann der vorhandene PushCustomAsync genutzt werden (NOOSE-Website/Services/IDiscordWebhookService.cs); eingehend braucht es denselben Interaktions-/Bot-Zugang wie der Slash-Befehl-Vorschlag.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (7/10):* Geht den Konflikt mit Discord richtig herum an (nicht gewinnen wollen, sondern anschliessen), ist aber technisch das fragilste Stueck der ganzen Liste und faellt bei jedem Discord-Ausfall auf.
- *Architektur-Passung (3/10):* Die Taskforce-Sichtbarkeit hängt an TaskforceAgent und wird überall hart durchgesetzt, während die Mitgliedschaft eines Discord-Threads davon nichts weiß — Spiegelung in beide Richtungen setzt genau das Prinzip außer Kraft, das der Codebase zugrunde liegt, und verlangt zusätzlich Bot, Identitätsmapping und Schleifenschutz.
- *Aufwand und Dauerlast (1/10):* Zwei-Wege-Synchronisation ist dauerhafter Betrieb (Echo-Schleifen, Bearbeitungen, Anhänge, Ausfälle) und trägt Inhalte aus einem rechtegeprüften Chat in einen Kanal, der nichts davon durchsetzt.

</details>

### Installierbare Handy-App mit Push

**Aufwand:** gross | **Jury-Schnitt:** 3.3 | **Blickwinkel:** Reichweite und Aussenwelt

**Was es tut.** Die Seite wird als App auf dem Handy installierbar (eigenes Symbol, Vollbild) und schickt echte Push-Nachrichten - auch wenn der Browser geschlossen ist.

**Warum.** Wer im Spiel oder unterwegs ist, hat die Seite nicht offen. Die Glocke oben rechts erreicht nur den, der ohnehin schon da ist - Erwaehnungen, faellige Wiedervorlagen und Besprechungserinnerungen kommen faktisch zu spaet an.

**Setzt an bei.** wwwroot enthaelt bis heute weder manifest.json noch einen Service Worker (NOOSE-Website/wwwroot/ - nur app.js, app.css, robots.txt, NooseIcon.png). Icon und Farbwelt sind fertig (NOOSE-Website/NooseIcon.png, NOOSE-Website/Theme/NooseTheme.cs). Ausloeser sind vorhanden: NOOSE-Website/Services/NotificationService.cs und NOOSE-Website/Infrastructure/Notifications/NotificationBroadcaster.cs, dazu die Erinnerungs-Worker in NOOSE-Website/Infrastructure/Meetings/MeetingReminderWorker.cs und NOOSE-Website/Infrastructure/Followups/FollowupDueWorker.cs. Das Push-Abo je Geraet ist eine neue kleine Tabelle im AppDbContext.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (6/10):* Echtes Push erreicht erstmals Leute, die die Seite gar nicht offen haben, aber Discord ist fuer diese Zielgruppe bereits der Push-Kanal — und iOS macht bei PWA-Push erfahrungsgemaess Aerger.
- *Architektur-Passung (2/10):* Weder Manifest noch Service Worker noch irgendeine Browser-Persistenz existieren, Web Push bringt VAPID-Schlüssel, Abo-Speicher und verschlüsselte Nutzlasten mit, und Blazor Server am SignalR-Kreis ist für die Offline-Hälfte einer PWA konzeptionell der ungünstigste Unterbau.
- *Aufwand und Dauerlast (2/10):* Service Worker, VAPID-Schlüssel, Abo-Lebenszyklus und iOS-Eigenheiten sind Dauerbaustelle — und ein Cache-Worker vor einer Blazor-Server-App ist eine eigene Fehlerquelle.

</details>

### Archiv statt Dauerbestand

**Aufwand:** gross | **Jury-Schnitt:** 3.3 | **Blickwinkel:** Fuehrung und Aktenqualitaet

**Was es tut.** Ein dritter Zustand neben aktiv und gelöscht: abgeschlossene Vorgänge, dauerhaft tote Personen und aufgelöste Fraktionen wandern nach konfigurierbarer Frist ins Archiv — aus Listen, Pickern und Standardsuche raus, vollständig erhalten, mit einem Klick zurückholbar.

**Warum.** Es gibt nur aktiv oder Papierkorb. Alles bleibt für immer in jeder Liste, jeder Suche und jeder Auswahl stehen, verwässert die Aktualitäts-Ampel (weil Uralt-Akten dauerhaft rot bleiben) und die Statistik. Eine Behörde ohne Aussonderungsregel ist ausserdem RP-untypisch — Aufbewahrungsfristen sind ein eigenes Spielelement.

**Setzt an bei.** Models/Abstractions/ISoftDelete mit dem per Reflection automatisch angewandten Query-Filter in Data/AppDbContext.cs ist die direkte Vorlage für einen zweiten Filter; Services/RecencyService.cs samt Data/Entities/Common/RecencyThreshold.cs für die Fristen-Konfiguration pro Aktentyp; Services/TrashService.cs und Services/TrashProjection.cs zeigen, wie ein neuer Zustand mit einer Zeile je Typ registriert wird; Infrastructure/Threat/ThreatScoreSweepWorker.cs als Vorbild für den täglichen Lauf; Services/Search/SearchCatalog.cs für den Filter-Trait.

<details><summary>Was die Jury sagte</summary>

- *Alltagsnutzen (4/10):* Langfristig richtig — ohne Aussonderung verwaessern Listen, Ampel und Statistik unaufhaltsam —, aber es ist grosse, unsichtbare Infrastrukturarbeit, deren Nutzen erst nach Monaten spuerbar wird.
- *Architektur-Passung (4/10):* Ein zweiter globaler Query-Filter neben ISoftDelete klingt mechanisch ähnlich, schlägt aber auf 67 Suchanbieter, jede Auswahlliste, Aktualität, Statistik, Papierkorb und Partner-Sichtbarkeit durch — und die dokumentierte IgnoreQueryFilters-Falle zeigt, wie scharfkantig diese Filter bereits sind.
- *Aufwand und Dauerlast (2/10):* Ein dritter Zustand muss in jeder Liste, jedem Picker, 67 Suchanbietern, Statistik und Ampel berücksichtigt werden — und ab dann in jeder künftigen Abfrage erneut.

</details>

---

## 3. Abgelehnt (3)

Bewusst verworfen. Der Grund steht dabei, damit die Frage nicht in sechs Monaten erneut aufkommt.

### Streifen-Abfrage fuers Handy

*Jury-Schnitt 8.7, Aufwand klein.* Eine bewusst karge Ein-Feld-Seite: Kennzeichen oder Name rein, heraus kommt eine einzige Antwortkarte mit genau dem, was man im Funk braucht - gesucht ja/nein, Kopfgeld, Warnhinweise, Einstufung, Lebensstatus, letzte Observation. Kein Rail, keine Tabs, grosse Schrift.

**Warum abgelehnt.** Abgelehnt, obwohl die Jury sie auf Platz 2 setzte. Der Bedarf - die Datenbank waehrend des Spiels benutzbar machen - bleibt damit offen; der andere Weg dorthin ist "Aktenlisten auf dem Handy", das vorgemerkt ist.

### Aktenreife-Ampel mit Mängelliste

*Jury-Schnitt 4, Aufwand gross.* Die Führung legt pro Aktentyp Prüfregeln fest (Beschreibung gefüllt, mindestens eine Quelle, Einstufung gesetzt, mindestens eine Verknüpfung, Foto vorhanden). Jede Akte bekommt daraus einen Reifegrad mit Ampel und eine konkrete Liste dessen, was fehlt; dazu eine Führungsansicht aller Akten mit Mängeln.

**Warum abgelehnt.** Eine Maengelliste ueber fremde Akten fuehlt sich im Rollenspiel wie eine Hausaufgabe an. Freiwillige Kontrollschleifen verwaisen auf einem Freizeitserver zuverlaessig, und eine dauerhaft rote Ampel wird ignoriert wie eine dauerhaft rote Glocke.

### Aktenrevision mit Vier-Augen-Abzeichnung

*Jury-Schnitt 3.7, Aufwand mittel.* Die Führung zieht Akten zur Prüfung (gezielt oder als Zufallsstichprobe), weist sie einem zweiten Agenten zu, der eine Checkliste abarbeitet und mit Datum abzeichnet. Das Ergebnis — geprüft oder Nacharbeit nötig — hängt an der Akte und im Nachweis.

**Warum abgelehnt.** Schlechtestbewertete Idee der gesamten Sammlung. Erzeugt Pflichtarbeit fuer zwei Leute pro Akte.

---

## 4. Zusammengefuehrt (4)

Zwei Blickwinkel kamen unabhaengig auf dieselbe Sache. Hier steht, wo sie aufgegangen ist.

- **Mehrfachauswahl in Listen** - Aufgegangen in **#11 Mehrfachauswahl in der Suche** - dieselbe Aktionsleiste, an beiden Orten eingehaengt. Sie wird einmal gebaut.
- **Kalender-Abo fuers Handy** - Aufgegangen in **Kalender-Abo fuer Handy und Discord** (vorgemerkt) - identischer Vorschlag.
- **Seit deinem letzten Besuch** - Aufgegangen in **#15 Meine Schicht** als eigener Abschnitt. Getrennt waeren es zwei Einstiege fuer dieselbe Frage gewesen.
- **Dublettenwächter für Personenakten** - Aufgegangen in **#24 Dubletten-Radar fuer Personenakten** als dessen zweite Haelfte (Bestandsfeed).

---

## Wie diese Liste entstand

Fuenf Blickwinkel haben die Codebase unabhaengig voneinander erkundet und je 8 bis 14 Vorschlaege
gemacht:

- **Rollenspiel-Immersion** - 13 Vorschlaege
- **Alltag des einzelnen Agenten** - 14 Vorschlaege
- **Fuehrung und Aktenqualitaet** - 13 Vorschlaege
- **Vorhandene Infrastruktur als Hebel** - 12 Vorschlaege
- **Reichweite und Aussenwelt** - 14 Vorschlaege

Danach hat jeder von drei Juroren **jeden** Vorschlag nach genau einer Frage bewertet:
Alltagsnutzen, Architektur-Passung, Verhaeltnis aus Aufwand und Dauerlast.

Die Diagnose, auf die alle fuenf Blickwinkel unabhaengig kamen: **die Seite ist inhaltlich fast
fertig, wird aber nach dem Spiel befuellt statt waehrenddessen benutzt.** Deshalb sind fast alle
starken Vorschlaege keine neuen Aktentypen, sondern neue Formen fuer Daten, die es laengst gibt -
und neue Wege dorthin, wo die Leute tatsaechlich sind: im Spiel und im Discord.

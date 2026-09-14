# CODE_REVIEW_TODO

Bekannte Tech-Debt- und Review-Findings, die bewusst nicht sofort behoben wurden.
In `CLAUDE.md` unter „Weiterführende Docs" verlinkt.

## Agenten-Auswahllisten (2026-08-07)

Kontext: `Services/AgentSelection.cs` ist seit dieser Änderung die einzige Stelle, die entscheidet, wer in
einer Agenten-Auswahlliste erscheint (`OnlySelectable` / `OnlyListable`). Zwei Punkte blieben offen.

### 1. Taskforce-scoped Partner-@Mentions (Feature, nicht Bug)

`MentionService.CandidatesAsync` ist die **bewusste Ausnahme** von `OnlySelectable`: Partner-Konten bleiben
erwähnbar. Das ist heute aber wirkungslos — die Mention läuft ins Leere:

- `MentionService.PartnerReleasableMentionTypes` enthält **kein** `nameof(Agent)`, also entfernt
  `ApplyPartnerScopeAsync` jede Agent-Mention und `Segment` rendert für Partner-Betrachter
  „(nicht verfügbar)".
- `NotificationService.FanOutMentionsAsync` prüft die Sichtbarkeit über den **bool-Shim** von
  `Visibility.IsRecordVisibleAsync` (`PartnerAgency: null`). Für einen Partner-Empfänger schlägt das Gate
  fehl, die Benachrichtigung wird verworfen.

Wenn Partner-Mentions wirklich funktionieren sollen, braucht es drei Teile in **einem** Commit:
1. `CandidatesAsync` bekommt einen optionalen `taskforceId`; Partner-Arm über einen neuen
   `PartnerVisibility.OnlyReleasedTo(db, nameof(Taskforce), id)` (Inverse von `HasShareAsync`), plus
   `!IsClassified`-Prüfung auf der Taskforce. Durchschleifen nach `MentionInput` und von dort in
   `TaskforceChatPanel`, `CommentPanel`, `CustomFieldsPanel`, `SourceDialog`, `FollowupDialog`,
   `TaskforceForm` (bei `/taskforces/neu` `null` übergeben).
2. `ApplyPartnerScopeAsync` muss Agent-Mentions der **eigenen** Behörde durchlassen (interne Codenamen
   bleiben verborgen).
3. `FanOutMentionsAsync` muss für Partner-Empfänger einen echten `ViewerScope` mit `PartnerAgency` bauen.

Achtung bei 3.: der Gate-Wechsel betrifft **alle** Mention-Benachrichtigungen. Der interne Zweig muss
bitgleich zum heutigen Shim bleiben (`MayClassifiedRead == MayAllTaskforces == MayAgenda == isLeadership`).

### 2. Verwaiste `DocumentAccessExclusion`-Zeilen auf Teamleitungen

`DocumentAccessService.GetAccessListAsync` listet Teamleitungen nicht mehr, `DocumentService` befreit aber
nur `isAdmin` von Ausschlüssen. Eine **bestehende** Ausschluss-Zeile auf eine Teamleitung wirkt daher
weiter, ist in der UI aber nicht mehr sichtbar und nicht mehr aufhebbar. Neue Zeilen können nicht mehr
entstehen — die UI bietet den Eintrag nicht an, und `RevokeAsync` wirft seit dieser Änderung auch
serverseitig für Teamleitungen. Es geht also nur um Altbestand:

```sql
SELECT x.* FROM DokumentEinsichtEntzug x JOIN AspNetUsers u ON u.Id = x.AgentId
  WHERE u.IstTeamLeitung = 1;
```

Bei Treffern die Zeilen einmalig löschen. Die saubere Alternative wäre ein `IsOnlyReader` auf
`DocumentViewerScope` (dann sind Ausschlüsse für die Aufsicht generell wirkungslos, passend zu „OnlyReader
liest alles") — `DocumentViewerScope` ist aber ein positionales `record struct` mit ~30 Konstruktionsstellen
in den Tests, deshalb bewusst zurückgestellt.

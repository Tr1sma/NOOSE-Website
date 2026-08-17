namespace NOOSE_Website.Services.Public;

/// <summary>The single truth about what may leave the house — the public counterpart to <see cref="Visibility"/>.</summary>
/// <remarks>
/// Every entity is either listed in <see cref="Publishable"/> with what exactly leaves, or in
/// <see cref="NeverPublic"/> with why it never does. <c>PublicVisibilityCoverageTests</c> reflects over all
/// <c>DbSet</c>s and fails the build on anything undecided, so a new table forces the author to answer the question
/// instead of leaving it to a reviewer's attention.
/// <para>
/// Being listed as publishable is a statement about the entity's own publication path, not a permission: the record
/// still only goes out through its publish snapshot, and a classified file never goes out at all.
/// </para>
/// </remarks>
public static class PublicVisibility
{
    private const string InternalRecord = "Interner Aktenbestand; nach außen geht allenfalls eine eigene Publikations-Tabelle.";
    private const string Assignment = "Zuordnungszeile ohne eigene Aussage; nach außen nie.";
    private const string Personnel = "Personaldaten der Behörde; würde Agenten enttarnen.";
    private const string Operational = "Operative Interna; nach außen nie.";
    private const string Configuration = "Konfiguration ohne Inhalt für außen.";
    private const string Protocol = "Protokollzeile; nach außen nie.";
    private const string Finance = "Interne Finanzdaten; öffentlich erscheint höchstens eine Summe aus einer eigenen Tabelle.";
    private const string Recruiting = "Bewerbungsverfahren; vertraulich zwischen Bewerber und Behörde.";
    private const string Assistant = "NOOSEI-Interna; nach außen nie.";
    private const string SearchInternal = "Suchindex des internen Bestands; nach außen nie.";

    /// <summary>Entity name → what exactly is published from it.</summary>
    public static readonly IReadOnlyDictionary<string, string> Publishable =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OeffentlichesModul"] = "Nur Beschriftung, Icon und Reihenfolge eines Nav-Eintrags; kein Aktenbezug.",
            ["OeffentlicheSeite"] = "Titel, Menütitel, Icon und der veröffentlichte Inhalt einer redaktionellen "
                + "Seite. Der Entwurf bleibt drinnen, und die Seite trägt keinen Aktenbezug.",
            ["OeffentlicheFahndung"] = "Der Publikations-Snapshot einer Ausschreibung: öffentliches Aktenzeichen, "
                + "Art, Anzeigename, die vom Autor gewählten Aliase, Vorwurfstext, letzte Gegend, Fahrzeugtext, die "
                + "beim Publizieren festgehaltene Gefahrenstufe, eine Kopie des Fotos und — im Archiv — das "
                + "Gefasst-Datum, dazu die Kopfgeld-Obergrenze als \"bis X\". Der Aktenbezug "
                + "(PersonId/FraktionId), der rohe Bedrohungs-Score, der Aufrufzähler und der Rückzugsgrund "
                + "bleiben drinnen.",
            ["FahndungKopfgeldAnteil"] = "Ausschließlich die Summe aller zugesagten und gesicherten Anteile "
                + "einer laufenden Ausschreibung, als eine Zahl. Herkunft, Stifter, der Betrag eines einzelnen "
                + "Anteils, ihre Anzahl, das Konto, die Kassenbuchung und der Status bleiben drinnen — eine "
                + "Aufschlüsselung verriete, welcher Agent wie viel eigenes Geld auf einen Kopf gesetzt hat.",
            ["Warnhinweis"] = "Bezeichnung und Farbe eines zugeordneten Warnhinweises, als Chip auf Board, "
                + "Steckbrief und Poster. Reihenfolge, Aktiv-Kennzeichen und die Zeilen-Id bleiben drinnen.",
        };

    /// <summary>Entity name → why it can never appear outside.</summary>
    public static readonly IReadOnlyDictionary<string, string> NeverPublic =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // --- the agency itself ---
            ["Agent"] = "Agenten bleiben nach außen anonym; weder Codename noch Klarname noch Dienstgrad verlassen das Haus.",

            // --- person files ---
            ["Person"] = "Die Akte bleibt drinnen; nach außen geht ausschließlich der Publikations-Snapshot einer Ausschreibung.",
            ["PersonDoc"] = "Maßnahmenprotokoll; nach außen nie.",
            ["PersonPhoto"] = "Bilddatei der Akte; ein Foto geht nur über den geprüften Publikations-Pfad hinaus.",
            ["ClassificationHistory"] = "Einstufungs-Verlauf; würde die Existenz eingestufter Akten verraten.",
            ["PersonAlias"] = "Aliasfeld der Akte; nach außen nur als Teil eines Snapshots.",
            ["PersonPhone"] = InternalRecord,
            ["PersonVehicle"] = "Fahrzeugdaten der Akte; nach außen nur als Teil eines Snapshots.",
            ["PersonLocation"] = InternalRecord,
            ["PersonWeapon"] = "Waffendaten der Akte; nach außen nur als Teil eines Snapshots.",
            ["ProfileSuggestion"] = "Unbestätigter Steckbrief-Vorschlag; nach außen nie.",
            ["PersonRelation"] = "Beziehungsgeflecht; nach außen nie.",

            // --- groupings ---
            ["Faction"] = "Die Akte bleibt drinnen; nach außen geht ausschließlich das Publikations-Profil.",
            ["FactionRank"] = InternalRecord,
            ["FactionWeaponStock"] = Operational,
            ["FactionInventory"] = Operational,
            ["FactionDrugRoute"] = Operational,
            ["FactionMember"] = "Mitgliederliste; nach außen nie.",
            ["FactionAgent"] = Assignment,
            ["FactionPhoto"] = "Bilddatei der Akte; nach außen nur über den geprüften Publikations-Pfad.",
            ["PersonGroup"] = InternalRecord,
            ["PersonGroupMember"] = "Mitgliederliste; nach außen nie.",
            ["PersonGroupAgent"] = Assignment,
            ["Party"] = InternalRecord,
            ["PartyMember"] = "Mitgliederliste; nach außen nie.",
            ["PartyAgent"] = Assignment,

            // --- casework ---
            ["Case"] = InternalRecord,
            ["CaseAgent"] = Assignment,
            ["Operation"] = Operational,
            ["OperationAgent"] = Assignment,
            ["Taskforce"] = Operational,
            ["TaskforceAgent"] = Assignment,
            ["TaskforceMessage"] = "Interner Einheiten-Chat; nach außen nie.",
            ["Observation"] = Operational,
            ["AgentAbduction"] = Operational,
            ["AbductionCompromise"] = Operational,
            ["EvidenceItem"] = Operational,
            ["EvidenceEntry"] = Operational,
            ["EvidenceEntryLine"] = Operational,
            ["Informant"] = "Quellenschutz; die Existenz einer Quelle verlässt das Haus nie.",
            ["InformantMeeting"] = "Quellenschutz; nach außen nie.",
            ["CounterIntelRule"] = "Gegenaufklärungs-Regel; ihre Kenntnis wäre eine Anleitung zur Umgehung.",
            ["WatchlistEntry"] = "Persönliche Merkliste eines Agenten; nach außen nie.",
            ["LeadDismissal"] = Operational,
            ["Followup"] = "Wiedervorlage an einer Akte; nach außen nie.",

            // --- attachments to records ---
            ["Source"] = "Quellenangabe an einer Akte; nach außen nie.",
            ["Comment"] = "Interner Kommentar an einer Akte; nach außen nie.",
            ["Link"] = "Verknüpfung zwischen Akten; nach außen nie.",
            ["Tag"] = Configuration,
            ["TagMapping"] = Assignment,
            ["CustomFieldDefinition"] = Configuration,
            ["CustomFieldValue"] = "Zusatzfeld an einer Akte; nach außen nie.",

            // --- library and documents ---
            ["Document"] = "Bibliotheks-Dokument mit eigener VS-Stufe; nach außen nie.",
            ["DocumentTemplate"] = Configuration,
            ["DocTemplate"] = Configuration,
            ["LibraryFile"] = "Anhang der Bibliothek; nach außen nie.",
            ["DocumentAccessExclusion"] = Assignment,
            ["PartnerShare"] = "Freigabe an eine Partnerbehörde; das ist ein eigener Kanal, nicht die Öffentlichkeit.",
            ["Law"] = "Gesetzestext, standardmäßig intern; nach außen geht nur ein ausdrücklich freigegebener Auszug.",

            // --- personnel ---
            ["AgentRankHistory"] = Personnel,
            ["AgentNote"] = Personnel,
            ["AgentPromotionRequest"] = Personnel,
            ["TrainingModule"] = Personnel,
            ["AgentModuleCompletion"] = Personnel,
            ["AgentBadge"] = Personnel,
            ["Absence"] = Personnel,
            ["AgentActivity"] = Personnel,
            ["AgentActivityLink"] = Assignment,
            ["ActivityTemplate"] = Configuration,
            ["PersonnelTemplate"] = Configuration,
            ["AgentInvite"] = "Einladungslink in die Behörde; nach außen nie.",

            // --- internal operations ---
            ["Request"] = "Antrag im Haus; nach außen nie.",
            ["Notification"] = "Benachrichtigung an einen Agenten; nach außen nie.",
            ["Job"] = "Aufgabe im Haus; nach außen nie.",
            ["JobAssignment"] = Assignment,
            ["Appointment"] = "Termin im Haus; nach außen nie.",
            ["AppointmentAssignment"] = Assignment,
            ["Meeting"] = "Besprechung im Haus; nach außen nie.",
            ["MeetingAgendaItem"] = "Tagesordnungspunkt; nach außen nie.",
            ["MeetingSignOff"] = Assignment,
            ["MeetingAttendance"] = Assignment,
            ["Announcement"] = "Mitteilung an die Belegschaft; nach außen geht stattdessen eine Pressemitteilung.",
            ["AnnouncementAcknowledgment"] = Assignment,
            ["Feedback"] = "Rückmeldung eines Agenten zur Anwendung; nach außen nie.",
            ["SituationReport"] = "Monats-Lagebericht; nach außen geht nur ein ausdrücklich freigegebener Auszug.",
            ["DossierSummary"] = "Maschineller Kurzbrief zu einer Akte; nach außen nie.",

            // --- money ---
            ["KassenBuchung"] = Finance,
            ["KassenBuchungVorlage"] = Configuration,
            ["FinancingItem"] = Finance,
            ["FinancingRequest"] = Finance,
            ["FinancingRequestLine"] = Finance,
            ["FinancingBudgetPeriod"] = Finance,

            // --- assistant ---
            ["LlmRequestLog"] = Assistant,
            ["LlmQuotaPeriod"] = Assistant,
            ["LlmQuotaAdjustment"] = Assistant,
            ["NooseiConversation"] = Assistant,
            ["NooseiMessage"] = Assistant,

            // --- scoring and config ---
            ["ThreatScoreConfig"] = "Gewichtung des Bedrohungs-Scores; ihre Kenntnis wäre eine Anleitung zur Umgehung.",
            ["ThreatScoreHistory"] = "Score-Verlauf je Akte; öffentlich erscheint nur ein aggregierter Trend ohne Aktenbezug.",
            ["RecencyThreshold"] = Configuration,
            ["SystemSetting"] = Configuration,
            ["EnumLabelOverride"] = Configuration,
            ["CaseNumberCounter"] = "Zählerstand der Aktenzeichen; verrät die Zahl der Akten.",
            ["SavedSearch"] = "Gespeicherte Suche eines Agenten; nach außen nie.",
            ["GraphCanvasLayout"] = "Persönliche Graph-Ansicht; nach außen nie.",
            ["SearchPhoneticKey"] = SearchInternal,
            ["SearchStemToken"] = SearchInternal,

            // --- logs ---
            ["AuditLog"] = Protocol,
            ["AccessLog"] = Protocol,

            // --- recruiting ---
            ["Bewerbung"] = Recruiting,
            ["BewerbungMessage"] = Recruiting,
            ["BewerbungTest"] = Recruiting,
            ["BewerbungTestQuestion"] = "Testfragen; öffentlich wären sie eine Lösungshilfe.",
            ["BewerbungTestOption"] = "Antwortoptionen; öffentlich wären sie eine Lösungshilfe.",
            ["BewerbungTestAssignment"] = Recruiting,
            ["BewerbungTestAnswer"] = Recruiting,
            ["Bewerbungssperre"] = "Bewerbungssperre einer Person; nach außen nie.",

            // --- public area's own tables ---
            ["BuergerProfil"] = "Konto eines Bürgers; sein Name gehört ihm, nicht der Website. Nach außen sieht ihn nur er selbst.",
            ["FahndungWarnhinweis"] = Assignment,
        };

    /// <summary>True when someone has decided the entity's public fate either way.</summary>
    public static bool IsDecided(string entityName)
        => Publishable.ContainsKey(entityName) || NeverPublic.ContainsKey(entityName);

    /// <summary>True when the entity has a publication path at all.</summary>
    public static bool MayBePublished(string entityName) => Publishable.ContainsKey(entityName);
}

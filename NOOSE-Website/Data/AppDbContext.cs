using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Requests;
using NOOSE_Website.Data.Entities.Announcements;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Watchlist;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data;

/// <summary>
/// Zentraler EF-Core-Kontext der NOOSE-Website.
/// Ab Phase 1 ein <see cref="IdentityDbContext{TUser}"/> für ASP.NET Core Identity
/// (Nutzer = <see cref="Agent"/>) plus die Querschnitts-Tabellen Audit-/Zugriffs-Log.
/// Domänen-Akten (Person, Personen-Dok, ...) kommen ab Phase 2 hinzu.
/// </summary>
public class AppDbContext : IdentityDbContext<Agent>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    // ---- Phase 2: Personen-Akten ----
    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonDoc> PersonDocs => Set<PersonDoc>();
    public DbSet<PersonPhoto> PersonPhotos => Set<PersonPhoto>();
    public DbSet<ClassificationHistory> ClassificationHistory => Set<ClassificationHistory>();
    public DbSet<PersonAlias> PersonAliases => Set<PersonAlias>();
    public DbSet<PersonPhone> PersonPhones => Set<PersonPhone>();
    public DbSet<PersonVehicle> PersonVehicles => Set<PersonVehicle>();
    public DbSet<PersonLocation> PersonLocations => Set<PersonLocation>();
    public DbSet<PersonWeapon> PersonWeapons => Set<PersonWeapon>();
    public DbSet<ProfileSuggestion> ProfileSuggestions => Set<ProfileSuggestion>();
    public DbSet<CaseNumberCounter> CaseNumberCounter => Set<CaseNumberCounter>();

    // ---- Phase 3a: Querschnitt (Tags, Kommentare, Quellen) – generisch über EntitaetTyp/EntitaetId ----
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TagMapping> TagMappings => Set<TagMapping>();
    public DbSet<Comment> Comments => Set<Comment>();

    // ---- Phase 3b: Verknüpfungs-Engine (generisch) + Person-Beziehungen (typisiert) ----
    public DbSet<Link> Links => Set<Link>();
    public DbSet<PersonRelation> PersonRelations => Set<PersonRelation>();

    // ---- Phase 3c: gespeicherte Suchen ----
    public DbSet<SavedSearch> SavedSearch => Set<SavedSearch>();

    // ---- Phase 4a: Fraktionen ----
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<FactionRank> FactionRanks => Set<FactionRank>();
    public DbSet<FactionWeaponStock> FactionWeaponStocks => Set<FactionWeaponStock>();
    public DbSet<FactionInventory> FactionInventories => Set<FactionInventory>();
    public DbSet<FactionDrugRoute> FactionDrugRoutes => Set<FactionDrugRoute>();
    public DbSet<FactionMember> FactionMembers => Set<FactionMember>();
    public DbSet<FactionAgent> FactionAgents => Set<FactionAgent>();
    public DbSet<FactionPhoto> FactionPhotos => Set<FactionPhoto>();
    public DbSet<FactionActivity> FactionActivities => Set<FactionActivity>();

    // ---- Phase 4b: Personengruppen ----
    public DbSet<PersonGroup> PersonGroups => Set<PersonGroup>();
    public DbSet<PersonGroupMember> PersonGroupMembers => Set<PersonGroupMember>();
    public DbSet<PersonGroupAgent> PersonGroupAgents => Set<PersonGroupAgent>();

    // ---- Phase 5a: Parteien ----
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyMember> PartyMembers => Set<PartyMember>();
    public DbSet<PartyAgent> PartyAgents => Set<PartyAgent>();

    // ---- Phase 5b: Operationen ----
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<OperationAgent> OperationAgents => Set<OperationAgent>();

    // ---- Phase 5: Vorgangs-/Fallakten ----
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseAgent> CaseAgents => Set<CaseAgent>();

    // ---- Phase 5c: Taskforces ----
    public DbSet<Taskforce> Taskforces => Set<Taskforce>();
    public DbSet<TaskforceAgent> TaskforceAgents => Set<TaskforceAgent>();

    // ---- Phase 5d: Taskforce-Chat ----
    public DbSet<TaskforceMessage> TaskforceMessages => Set<TaskforceMessage>();

    // ---- Phase 5: Observationen (Überwachungseinträge an Personen) ----
    public DbSet<Observation> Observations => Set<Observation>();

    // ---- Phase 5e: Personalakte je Agent ----
    public DbSet<AgentRankHistory> AgentRankHistories => Set<AgentRankHistory>();
    public DbSet<AgentNote> AgentNotes => Set<AgentNote>();
    public DbSet<AgentPromotionRequest> AgentPromotionRequests => Set<AgentPromotionRequest>();

    // ---- Phase 5: Antrags-/Posteingang-Workflow (Hochstufung) ----
    public DbSet<Request> Requests => Set<Request>();

    // ---- Phase 6: In-App-Benachrichtigungen (Glocke) ----
    public DbSet<Notification> Notifications => Set<Notification>();

    // ---- Phase 6: Watchlist (gefolgte Akten) ----
    public DbSet<WatchlistEntry> Watchlists => Set<WatchlistEntry>();

    // ---- Phase 6: Aufgaben/To-Dos & Zuweisungen ----
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();

    // ---- Phase 8 (Block C): Termine/Kalender & Teilnehmer ----
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentAssignment> AppointmentAssignments => Set<AppointmentAssignment>();

    // ---- Phase 6: News/Schwarzes Brett + Behörden-Broadcast ----
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementAcknowledgment> AnnouncementAcknowledgments => Set<AnnouncementAcknowledgment>();

    // ---- Phase 7: Dok-Vorlagen (admin-definierte Erfassungsmasken) ----
    public DbSet<DocTemplate> DocTemplates => Set<DocTemplate>();

    // ---- Phase 7: konfigurierbare Custom-Felder je Aktentyp ----
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();

    // ---- Phase 7: Dokumenten-Bibliothek (WYSIWYG-Dokumente) + Dokument-Vorlagen ----
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();

    // ---- Phase 7: Aktualitäts-Ampel (Schwellwerte je Aktentyp) + Wiedervorlagen ----
    public DbSet<RecencyThreshold> RecencyThresholds => Set<RecencyThreshold>();
    public DbSet<ThreatScoreConfig> ThreatScoreConfigs => Set<ThreatScoreConfig>();
    public DbSet<Followup> Followups => Set<Followup>();

    // ---- Phase 8 (Block D, Schritt 2): archivierte Monats-Lageberichte ----
    public DbSet<SituationReport> SituationReports => Set<SituationReport>();

    // ---- Phase 7 (Abschluss): Systemeinstellungen, Gesetzbuch, Datei-Bibliothek ----
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Law> Laws => Set<Law>();
    public DbSet<LibraryFile> LibraryFiles => Set<LibraryFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Agent>(b =>
        {
            b.Property(a => a.Codename).HasMaxLength(128);
            b.Property(a => a.RealName).HasMaxLength(128);
            b.Property(a => a.BadgeNumber).HasMaxLength(32);
            b.Property(a => a.DiscordId).HasMaxLength(32);
            b.Property(a => a.DiscordUsername).HasMaxLength(64);
            b.Property(a => a.AvatarUrl).HasMaxLength(512);
            b.Property(a => a.BlockedReason).HasMaxLength(512);
            b.HasIndex(a => a.DiscordId).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(a => a.EntityType).HasMaxLength(128);
            b.Property(a => a.EntityId).HasMaxLength(64);
            b.Property(a => a.AgentId).HasMaxLength(64);
            b.Property(a => a.AgentName).HasMaxLength(128);
            b.HasIndex(a => new { a.EntityType, a.EntityId });
            b.HasIndex(a => a.Timestamp);
        });

        modelBuilder.Entity<AccessLog>(b =>
        {
            b.Property(a => a.EntityType).HasMaxLength(128);
            b.Property(a => a.EntityId).HasMaxLength(64);
            b.Property(a => a.AgentId).HasMaxLength(64);
            b.Property(a => a.AgentName).HasMaxLength(128);
            b.HasIndex(a => new { a.EntityType, a.EntityId });
        });

        // ---- Phase 2: Personen-Akten ----
        modelBuilder.Entity<Person>(b =>
        {
            b.Property(p => p.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            // Bedrohungs-Score-Aufschlüsselung (Phase 8/Block D): JSON beliebiger Länge → longtext (wie bei Fraktion).
            b.Property(p => p.ThreatDetailJson).HasColumnType("longtext");
            b.HasIndex(p => p.CaseNumber).IsUnique();
            b.HasIndex(p => p.Name);
            b.HasIndex(p => p.IsClassified);

            b.HasMany(p => p.Aliases).WithOne(a => a.Person!)
                .HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.PhoneNumbers).WithOne(t => t.Person!)
                .HasForeignKey(t => t.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Vehicles).WithOne(f => f.Person!)
                .HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Locations).WithOne(o => o.Person!)
                .HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Weapons).WithOne(w => w.Person!)
                .HasForeignKey(w => w.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Photos).WithOne(f => f.Person!)
                .HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Docs).WithOne(d => d.Person!)
                .HasForeignKey(d => d.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Observations).WithOne(o => o.Person!)
                .HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonAlias>(b => b.Property(a => a.AliasName).HasMaxLength(200));
        modelBuilder.Entity<PersonPhone>(b =>
        {
            b.Property(t => t.Number).HasMaxLength(40);
            b.Property(t => t.Designation).HasMaxLength(100);
            b.HasIndex(t => t.Number);
        });
        modelBuilder.Entity<PersonVehicle>(b =>
        {
            b.Property(f => f.Designation).HasMaxLength(200);
            b.Property(f => f.LicensePlate).HasMaxLength(40);
        });
        modelBuilder.Entity<PersonLocation>(b =>
        {
            b.Property(o => o.Text).HasMaxLength(300);
            b.Property(o => o.Note).HasMaxLength(500);
        });
        modelBuilder.Entity<PersonWeapon>(b => b.Property(w => w.Text).HasMaxLength(200));

        modelBuilder.Entity<ProfileSuggestion>(b =>
        {
            b.Property(v => v.Value).HasMaxLength(300).IsRequired();
            // Eindeutig je Typ+Wert (case-insensitiv über die DB-Collation) → keine doppelten Vorschläge.
            b.HasIndex(v => new { v.Type, v.Value }).IsUnique();
        });

        modelBuilder.Entity<PersonPhoto>(b =>
        {
            b.Property(f => f.FileNameSaved).HasMaxLength(128);
            b.Property(f => f.OriginalName).HasMaxLength(260);
            b.Property(f => f.ContentType).HasMaxLength(100);
            b.Property(f => f.CreatedById).HasMaxLength(64);
        });

        modelBuilder.Entity<ClassificationHistory>(b =>
        {
            b.Property(e => e.EntityType).HasMaxLength(128);
            b.Property(e => e.EntityId).HasMaxLength(64);
            b.Property(e => e.Justification).HasMaxLength(1000);
            b.Property(e => e.AgentId).HasMaxLength(64);
            b.Property(e => e.AgentName).HasMaxLength(128);
            b.Property(e => e.RequestId).HasMaxLength(64);
            b.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        modelBuilder.Entity<PersonDoc>(b =>
        {
            b.Property(d => d.Faction).HasMaxLength(200);
            // Lose Verknüpfung zu Fraktion/Personengruppe (kein FK – analog EntitaetTyp/EntitaetId).
            b.Property(d => d.OrgType).HasMaxLength(128);
            b.Property(d => d.OrgId).HasMaxLength(64);
            b.HasIndex(d => d.PersonId);
            b.HasIndex(d => new { d.OrgType, d.OrgId });
        });

        // Observation (Überwachungseintrag an einer Person) – Kind der Person wie PersonDok.
        modelBuilder.Entity<Observation>(b =>
        {
            b.Property(o => o.Location).HasMaxLength(300);
            b.Property(o => o.Sighting).HasMaxLength(4000);
            b.Property(o => o.Result).HasMaxLength(4000);
            // Lose Verknüpfung zu Fraktion/Personengruppe (kein FK – analog EntitaetTyp/EntitaetId).
            b.Property(o => o.OrgType).HasMaxLength(128);
            b.Property(o => o.OrgId).HasMaxLength(64);
            b.HasIndex(o => o.PersonId);
            b.HasIndex(o => o.Start);
            b.HasIndex(o => new { o.OrgType, o.OrgId });
            // FK auf den beobachtenden Identity-Agent: SetNull, damit ein gelöschter Agent die Observation
            // nicht mitlöscht (die Beziehung zur Person ist im Person-Block mit Cascade konfiguriert).
            b.HasOne(o => o.ObservingAgent).WithMany()
                .HasForeignKey(o => o.ObservingAgentId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(o => o.ObservingAgentId);
        });

        modelBuilder.Entity<CaseNumberCounter>(b =>
        {
            // Zusammengesetzter Schlüssel (Praefix, Jahr) → eine eigene Sequenz je Aktentyp und Jahr.
            b.HasKey(z => new { z.Prefix, z.Year });
            b.Property(z => z.Prefix).HasMaxLength(8);
            // Jahr ist eine echte Jahreszahl (kein Auto-Increment) – wird beim Insert explizit gesetzt.
            b.Property(z => z.Year).ValueGeneratedNever();
        });

        // ---- Phase 3a: Querschnitt ----
        // Generische Assoziationen (Quelle/Tag-Zuordnung/Kommentar) verweisen polymorph per
        // EntitaetTyp+EntitaetId auf ihre Eltern-Akte (wie Audit-/Zugriffs-Log) – kein FK, daher
        // ist der kombinierte Index der schnelle Lade-Pfad „alle X einer Akte".
        modelBuilder.Entity<Source>(b =>
        {
            b.Property(q => q.EntityType).HasMaxLength(128);
            b.Property(q => q.EntityId).HasMaxLength(64);
            b.Property(q => q.Title).HasMaxLength(300).IsRequired();
            b.Property(q => q.Url).HasMaxLength(2048);
            b.Property(q => q.TargetType).HasMaxLength(128);
            b.Property(q => q.TargetId).HasMaxLength(64);
            b.Property(q => q.FileNameSaved).HasMaxLength(128);
            b.Property(q => q.OriginalName).HasMaxLength(260);
            b.Property(q => q.ContentType).HasMaxLength(100);
            b.HasIndex(q => new { q.EntityType, q.EntityId });
        });

        modelBuilder.Entity<Tag>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(60).IsRequired();
            b.Property(t => t.Colour).HasMaxLength(32);
            b.HasIndex(t => t.Name).IsUnique();
        });

        // ---- Phase 7: Dok-Vorlagen (admin-definierte Erfassungsmasken) ----
        modelBuilder.Entity<DocTemplate>(b =>
        {
            b.Property(v => v.Name).HasMaxLength(120).IsRequired();
            b.Property(v => v.Description).HasMaxLength(500);
            b.Property(v => v.DefaultReason).HasMaxLength(2000);
            b.Property(v => v.DefaultFaction).HasMaxLength(200);
            b.Property(v => v.DefaultReceivedInformation).HasMaxLength(4000);
            // Kein Unique-Index: Soft-Delete würde sonst die Wieder-Anlage eines gelöschten Namens
            // blockieren (gelöschte Zeile belegt den Namen). Eindeutigkeit wird in DokVorlageService
            // geprüft (respektiert den Soft-Delete-Filter).
            b.HasIndex(v => v.Name);
            b.HasIndex(v => v.IsActive);
        });

        // ---- Phase 7: konfigurierbare Custom-Felder je Aktentyp ----
        modelBuilder.Entity<CustomFieldDefinition>(b =>
        {
            b.Property(d => d.EntityType).HasMaxLength(128).IsRequired();
            b.Property(d => d.Name).HasMaxLength(120).IsRequired();
            b.Property(d => d.Options).HasMaxLength(2000);
            // Kein Unique-Index (Soft-Delete würde Wieder-Anlage blockieren); Eindeutigkeit je
            // Aktentyp prüft CustomFeldDefinitionService (respektiert den Soft-Delete-Filter).
            b.HasIndex(d => new { d.EntityType, d.IsActive });
        });

        modelBuilder.Entity<CustomFieldValue>(b =>
        {
            b.Property(w => w.CustomFieldDefinitionId).HasMaxLength(64).IsRequired();
            b.Property(w => w.EntityType).HasMaxLength(128).IsRequired();
            b.Property(w => w.EntityId).HasMaxLength(64).IsRequired();
            b.Property(w => w.Value).HasColumnType("longtext");
            b.HasIndex(w => new { w.EntityType, w.EntityId });
            // Ein Wert je Feld je Akte.
            b.HasIndex(w => new { w.CustomFieldDefinitionId, w.EntityType, w.EntityId }).IsUnique();
        });

        // ---- Phase 7: Dokumenten-Bibliothek + Dokument-Vorlagen ----
        modelBuilder.Entity<Document>(b =>
        {
            b.Property(d => d.Title).HasMaxLength(300).IsRequired();
            b.Property(d => d.Category).HasMaxLength(120);
            // Formatierter HTML-Body kann beliebig lang werden → longtext (wie CustomFeldWert.Wert).
            b.Property(d => d.ContentHtml).HasColumnType("longtext");
            b.HasIndex(d => d.Title);
            b.HasIndex(d => d.Category);
            b.HasIndex(d => d.IsClassified);
        });

        modelBuilder.Entity<DocumentTemplate>(b =>
        {
            b.Property(v => v.Name).HasMaxLength(120).IsRequired();
            b.Property(v => v.Description).HasMaxLength(500);
            b.Property(v => v.Category).HasMaxLength(120);
            b.Property(v => v.ContentHtml).HasColumnType("longtext");
            // Kein Unique-Index (Soft-Delete würde die Wieder-Anlage eines gelöschten Namens blockieren);
            // Eindeutigkeit prüft DokumentVorlageService (respektiert den Soft-Delete-Filter).
            b.HasIndex(v => v.Name);
            b.HasIndex(v => v.IsActive);
        });

        // ---- Phase 7: Aktualitäts-Ampel + Wiedervorlagen ----
        modelBuilder.Entity<RecencyThreshold>(b =>
        {
            // Eine Konfig-Zeile je Aktentyp (z. B. nameof(Person)) → Aktentyp ist der Primärschlüssel.
            b.HasKey(s => s.RecordsType);
            b.Property(s => s.RecordsType).HasMaxLength(64);
        });

        // ---- Phase 8/Block D: admin-einstellbare Bedrohungs-Score-Konfiguration (eine Singleton-Zeile, JSON) ----
        modelBuilder.Entity<ThreatScoreConfig>(b =>
        {
            b.HasKey(k => k.Id);
            b.Property(k => k.Id).HasMaxLength(32);
            b.Property(k => k.Json).HasColumnType("longtext");
        });

        // ---- Phase 8/Block D, Schritt 2: archivierte Monats-Lageberichte (Snapshot als JSON) ----
        modelBuilder.Entity<SituationReport>(b =>
        {
            b.Property(l => l.Title).HasMaxLength(200).IsRequired();
            b.Property(l => l.SnapshotJson).HasColumnType("longtext");
            // Archiv-Liste sortiert nach Berichtsmonat (neueste zuerst) – Index ist der schnelle Pfad.
            // KEIN Unique-Index auf (Jahr, Monat): Neu-Erzeugung ersetzt per Soft-Delete (alter Bericht bleibt
            // als gelöschte Zeile bestehen); die Aktiv-Eindeutigkeit prüft der LageberichtService.
            b.HasIndex(l => new { l.Year, l.Month });
        });

        modelBuilder.Entity<Followup>(b =>
        {
            b.Property(w => w.EntityType).HasMaxLength(128);
            b.Property(w => w.EntityId).HasMaxLength(64);
            b.Property(w => w.Note).HasMaxLength(500);
            b.Property(w => w.ResponsibleAgentId).HasMaxLength(64);
            b.Property(w => w.DoneById).HasMaxLength(64);
            // Schneller Lade-Pfad „alle Wiedervorlagen einer Akte".
            b.HasIndex(w => new { w.EntityType, w.EntityId });
            // Job-Query: offene, fällige, noch nicht gemeldete Wiedervorlagen.
            b.HasIndex(w => new { w.DueAt, w.Done, w.NotifiedAt });
            // FK auf den zuständigen Identity-Agent: SetNull, damit ein gelöschter Agent die Wiedervorlage
            // nicht mitlöscht (Feld ist nullable).
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(w => w.ResponsibleAgentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TagMapping>(b =>
        {
            b.Property(z => z.EntityType).HasMaxLength(128);
            b.Property(z => z.EntityId).HasMaxLength(64);
            b.HasOne(z => z.Tag).WithMany().HasForeignKey(z => z.TagId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(z => new { z.EntityType, z.EntityId });
            // Verhindert Doppel-Tagging derselben Akte mit demselben Tag.
            b.HasIndex(z => new { z.TagId, z.EntityType, z.EntityId }).IsUnique();
        });

        modelBuilder.Entity<Comment>(b =>
        {
            b.Property(k => k.EntityType).HasMaxLength(128);
            b.Property(k => k.EntityId).HasMaxLength(64);
            b.Property(k => k.AuthorName).HasMaxLength(128);
            b.HasIndex(k => new { k.EntityType, k.EntityId });
        });

        // ---- Phase 3b: Verknüpfungen + Person-Beziehungen ----
        modelBuilder.Entity<Link>(b =>
        {
            b.Property(v => v.SourceType).HasMaxLength(128);
            b.Property(v => v.SourceId).HasMaxLength(64);
            b.Property(v => v.TargetType).HasMaxLength(128);
            b.Property(v => v.TargetId).HasMaxLength(64);
            b.Property(v => v.Label).HasMaxLength(200);
            // Beide Richtungen schnell auffindbar.
            b.HasIndex(v => new { v.SourceType, v.SourceId });
            b.HasIndex(v => new { v.TargetType, v.TargetId });
        });

        modelBuilder.Entity<PersonRelation>(b =>
        {
            b.Property(x => x.Note).HasMaxLength(500);
            // Zwei FKs auf dieselbe Tabelle (Personen) → Restrict statt Cascade (sonst „multiple cascade paths").
            b.HasOne(x => x.PersonA).WithMany()
                .HasForeignKey(x => x.PersonAId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PersonB).WithMany()
                .HasForeignKey(x => x.PersonBId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.PersonAId);
            b.HasIndex(x => x.PersonBId);
        });

        modelBuilder.Entity<SavedSearch>(b =>
        {
            b.Property(g => g.AgentId).HasMaxLength(64);
            b.Property(g => g.Name).HasMaxLength(120).IsRequired();
            b.HasOne<Agent>().WithMany().HasForeignKey(g => g.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(g => g.AgentId);
        });

        // ---- Phase 4a: Fraktionen ----
        modelBuilder.Entity<Faction>(b =>
        {
            b.Property(f => f.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(f => f.Name).HasMaxLength(200).IsRequired();
            b.Property(f => f.Kind).HasMaxLength(100);
            b.Property(f => f.Radio).HasMaxLength(100);
            b.Property(f => f.Darkchat).HasMaxLength(100);
            b.Property(f => f.IssuingTimes).HasMaxLength(300);
            b.Property(f => f.Estate).HasMaxLength(500);
            b.Property(f => f.RecognitionColor).HasMaxLength(32);
            b.Property(f => f.Targets).HasMaxLength(2000);
            b.Property(f => f.Description).HasMaxLength(2000);
            // Bedrohungs-Score-Aufschlüsselung (Phase 8/Block D): JSON beliebiger Länge → longtext (wie CustomFeldWert.Wert).
            b.Property(f => f.ThreatDetailJson).HasColumnType("longtext");
            b.HasIndex(f => f.CaseNumber).IsUnique();
            b.HasIndex(f => f.Name);
            b.HasIndex(f => f.IsClassified);

            b.HasMany(f => f.Ranks).WithOne(r => r.Faction!)
                .HasForeignKey(r => r.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.WeaponStock).WithOne(w => w.Faction!)
                .HasForeignKey(w => w.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Inventory).WithOne(l => l.Faction!)
                .HasForeignKey(l => l.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.DrugRoutes).WithOne(d => d.Faction!)
                .HasForeignKey(d => d.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Members).WithOne(m => m.Faction!)
                .HasForeignKey(m => m.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Agents).WithOne(a => a.Faction!)
                .HasForeignKey(a => a.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Photos).WithOne(x => x.Faction!)
                .HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Activities).WithOne(a => a.Faction!)
                .HasForeignKey(a => a.FactionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FactionActivity>(b =>
        {
            b.Property(a => a.Title).HasMaxLength(200).IsRequired();
            b.Property(a => a.Kind).HasMaxLength(100);
            b.Property(a => a.Location).HasMaxLength(200);
            b.Property(a => a.Description).HasMaxLength(4000);
            b.HasIndex(a => a.FactionId);
            // Index auf Art für die Vorschlags-Abfrage (Distinct über vorhandene Arten).
            b.HasIndex(a => a.Kind);
        });

        modelBuilder.Entity<FactionPhoto>(b =>
        {
            // Spiegelt die PersonFoto-Konfiguration (Metadaten; Datei liegt außerhalb wwwroot).
            b.Property(f => f.FileNameSaved).HasMaxLength(128);
            b.Property(f => f.OriginalName).HasMaxLength(260);
            b.Property(f => f.ContentType).HasMaxLength(100);
            b.Property(f => f.CreatedById).HasMaxLength(64);
            b.HasIndex(f => f.FactionId);
        });

        modelBuilder.Entity<FactionAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.FactionId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        modelBuilder.Entity<FactionRank>(b =>
        {
            b.Property(r => r.Designation).HasMaxLength(100).IsRequired();
        });
        modelBuilder.Entity<FactionWeaponStock>(b =>
        {
            b.Property(w => w.Designation).HasMaxLength(200).IsRequired();
            b.Property(w => w.Quantity).HasMaxLength(50);
        });
        modelBuilder.Entity<FactionInventory>(b =>
        {
            b.Property(l => l.Designation).HasMaxLength(200).IsRequired();
            b.Property(l => l.Quantity).HasMaxLength(50);
        });
        modelBuilder.Entity<FactionDrugRoute>(b =>
        {
            b.Property(d => d.Designation).HasMaxLength(200).IsRequired();
            b.Property(d => d.Note).HasMaxLength(200);
        });

        modelBuilder.Entity<FactionMember>(b =>
        {
            b.Property(m => m.Rank).HasMaxLength(100);
            // FK auf Person mit Restrict (Fraktion cascadet bereits auf diese Tabelle → sonst „multiple cascade paths").
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // Kein Unique-Index: Mitgliedschaften sind soft-deletebar (Verlauf), und ein Wiedereintritt legt
            // eine neue aktive Zeile neben der beendeten an. Aktiv-Eindeutigkeit prüft FraktionService.
            b.HasIndex(m => new { m.FactionId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        // ---- Phase 4b: Personengruppen ----
        modelBuilder.Entity<PersonGroup>(b =>
        {
            b.Property(g => g.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(g => g.Name).HasMaxLength(200).IsRequired();
            b.Property(g => g.Description).HasMaxLength(2000);
            b.Property(g => g.Targets).HasMaxLength(2000);
            b.HasIndex(g => g.CaseNumber).IsUnique();
            b.HasIndex(g => g.Name);
            b.HasIndex(g => g.IsClassified);

            b.HasMany(g => g.Members).WithOne(m => m.PersonGroup!)
                .HasForeignKey(m => m.PersonGroupId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(g => g.Agents).WithOne(a => a.PersonGroup!)
                .HasForeignKey(a => a.PersonGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonGroupMember>(b =>
        {
            b.Property(m => m.Role).HasMaxLength(100);
            // FK auf Person mit Restrict (Gruppe cascadet bereits auf diese Tabelle → sonst „multiple cascade paths").
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // Kein Unique-Index: soft-deletebar (Verlauf) + Wiedereintritt; Aktiv-Eindeutigkeit prüft PersonengruppeService.
            b.HasIndex(m => new { m.PersonGroupId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        modelBuilder.Entity<PersonGroupAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.PersonGroupId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        // ---- Phase 5a: Parteien ----
        modelBuilder.Entity<Party>(b =>
        {
            b.Property(p => p.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Targets).HasMaxLength(2000);
            b.Property(p => p.Remarks).HasMaxLength(2000);
            b.HasIndex(p => p.CaseNumber).IsUnique();
            b.HasIndex(p => p.Name);
            b.HasIndex(p => p.IsClassified);

            b.HasMany(p => p.Members).WithOne(m => m.Party!)
                .HasForeignKey(m => m.PartyId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Agents).WithOne(a => a.Party!)
                .HasForeignKey(a => a.PartyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PartyMember>(b =>
        {
            b.Property(m => m.Role).HasMaxLength(100);
            // FK auf Person mit Restrict (Partei cascadet bereits auf diese Tabelle → sonst „multiple cascade paths").
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // Kein Unique-Index: soft-deletebar (Verlauf) + Wiedereintritt; Aktiv-Eindeutigkeit prüft ParteiService.
            b.HasIndex(m => new { m.PartyId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        modelBuilder.Entity<PartyAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.PartyId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        // ---- Phase 5b: Operationen ----
        modelBuilder.Entity<Operation>(b =>
        {
            b.Property(o => o.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(o => o.Title).HasMaxLength(200).IsRequired();
            b.Property(o => o.Type).HasMaxLength(100);
            b.Property(o => o.Location).HasMaxLength(200);
            b.Property(o => o.Expiry).HasMaxLength(4000);
            b.Property(o => o.Result).HasMaxLength(4000);
            b.Property(o => o.Remarks).HasMaxLength(2000);
            b.HasIndex(o => o.CaseNumber).IsUnique();
            b.HasIndex(o => o.Title);
            b.HasIndex(o => o.Status);
            b.HasIndex(o => o.IsClassified);

            b.HasMany(o => o.Agents).WithOne(a => a.Operation!)
                .HasForeignKey(a => a.OperationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OperationAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.OperationId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        // ---- Phase 5: Vorgangs-/Fallakten ----
        modelBuilder.Entity<Case>(b =>
        {
            b.Property(v => v.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(v => v.Title).HasMaxLength(200).IsRequired();
            b.Property(v => v.Type).HasMaxLength(100);
            b.Property(v => v.Description).HasMaxLength(4000);
            b.Property(v => v.Summary).HasMaxLength(4000);
            b.Property(v => v.ClosingNote).HasMaxLength(4000);
            b.HasIndex(v => v.CaseNumber).IsUnique();
            b.HasIndex(v => v.Title);
            b.HasIndex(v => v.Status);
            b.HasIndex(v => v.IsClassified);

            b.HasMany(v => v.Agents).WithOne(a => a.Case!)
                .HasForeignKey(a => a.CaseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CaseAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.CaseId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        // ---- Phase 6: Aufgaben/To-Dos & Zuweisungen ----
        modelBuilder.Entity<Job>(b =>
        {
            b.Property(a => a.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(a => a.Title).HasMaxLength(200).IsRequired();
            b.Property(a => a.Description).HasMaxLength(4000);
            b.HasIndex(a => a.CaseNumber).IsUnique();
            b.HasIndex(a => a.Title);
            b.HasIndex(a => a.Status);
            b.HasIndex(a => a.DueDate);
            b.HasIndex(a => a.IsRestricted);

            b.HasMany(a => a.Assignments).WithOne(z => z.Job!)
                .HasForeignKey(z => z.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobAssignment>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(z => z.Agent).WithMany()
                .HasForeignKey(z => z.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(z => new { z.JobId, z.AgentId }).IsUnique();
            b.HasIndex(z => z.AgentId);
        });

        // ---- Phase 8 (Block C): Termine/Kalender & Teilnehmer (Vorlage: Aufgabe) ----
        modelBuilder.Entity<Appointment>(b =>
        {
            b.Property(t => t.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(t => t.Title).HasMaxLength(200).IsRequired();
            b.Property(t => t.Location).HasMaxLength(200);
            b.Property(t => t.Description).HasMaxLength(4000);
            b.HasIndex(t => t.CaseNumber).IsUnique();
            b.HasIndex(t => t.Title);
            b.HasIndex(t => t.Category);
            b.HasIndex(t => t.Status);
            // Kalender lädt je sichtbarem Fenster über den Beginn → Index ist der schnelle Pfad.
            b.HasIndex(t => t.Start);
            b.HasIndex(t => t.Visibility);

            b.HasMany(t => t.Participant).WithOne(z => z.Appointment!)
                .HasForeignKey(z => z.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppointmentAssignment>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(z => z.Agent).WithMany()
                .HasForeignKey(z => z.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(z => new { z.AppointmentId, z.AgentId }).IsUnique();
            b.HasIndex(z => z.AgentId);
        });

        // ---- Phase 6: News/Schwarzes Brett + Behörden-Broadcast ----
        modelBuilder.Entity<Announcement>(b =>
        {
            b.Property(a => a.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(a => a.Title).HasMaxLength(200).IsRequired();
            b.Property(a => a.TargetId).HasMaxLength(64);
            // Inhalt bewusst ohne HasMaxLength (longtext) – trägt @{Typ:Id}-Erwähnungstokens.
            b.HasIndex(a => a.CaseNumber).IsUnique();
            // Brett-Sortierung (Wichtig zuerst, dann neueste) und Zielgruppen-Filter.
            b.HasIndex(a => new { a.Important, a.CreatedAt });
            b.HasIndex(a => a.Audience);

            b.HasMany(a => a.Acknowledgments).WithOne(q => q.Announcement!)
                .HasForeignKey(q => q.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnnouncementAcknowledgment>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(q => q.Agent).WithMany()
                .HasForeignKey(q => q.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(q => new { q.AnnouncementId, q.AgentId }).IsUnique();
            b.HasIndex(q => new { q.AgentId, q.AcknowledgedAt });
        });

        // ---- Phase 5: Antrags-/Posteingang-Workflow (Hochstufung) ----
        modelBuilder.Entity<Request>(b =>
        {
            b.Property(a => a.TargetType).HasMaxLength(128);
            b.Property(a => a.TargetId).HasMaxLength(64);
            b.Property(a => a.TargetDesignation).HasMaxLength(256);
            b.Property(a => a.Justification).HasMaxLength(2000);
            b.Property(a => a.DecisionNote).HasMaxLength(2000);
            b.Property(a => a.RequesterName).HasMaxLength(100);
            b.Property(a => a.DeciderName).HasMaxLength(100);
            b.Property(a => a.CreatedById).HasMaxLength(64);
            b.HasIndex(a => new { a.Type, a.Status });
            b.HasIndex(a => new { a.TargetType, a.TargetId });
            b.HasIndex(a => a.CreatedById);
        });

        // ---- Phase 6: In-App-Benachrichtigungen (Glocke) ----
        modelBuilder.Entity<Notification>(b =>
        {
            b.Property(n => n.RecipientId).HasMaxLength(64);
            b.Property(n => n.Title).HasMaxLength(300);
            b.Property(n => n.Href).HasMaxLength(512);
            b.Property(n => n.CreatedById).HasMaxLength(64);
            // Schneller Lade-Pfad „(ungelesene) Benachrichtigungen eines Empfängers".
            b.HasIndex(n => new { n.RecipientId, n.ReadAt });
        });

        // ---- Phase 6: Watchlist (gefolgte Akten) ----
        modelBuilder.Entity<WatchlistEntry>(b =>
        {
            b.Property(w => w.AgentId).HasMaxLength(64);
            b.Property(w => w.EntityType).HasMaxLength(128);
            b.Property(w => w.EntityId).HasMaxLength(64);
            b.Property(w => w.CreatedById).HasMaxLength(64);
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne<Agent>().WithMany().HasForeignKey(w => w.AgentId).OnDelete(DeleteBehavior.Restrict);
            // Schneller Lade-Pfad „Folger einer Akte" (Fan-out) und „meine beobachteten Akten".
            b.HasIndex(w => new { w.EntityType, w.EntityId });
            b.HasIndex(w => new { w.AgentId, w.IsDeleted });
            // Bewusst KEIN Unique-Index: Entfolgen = Soft-Delete, erneutes Folgen reaktiviert die Zeile
            // (Aktiv-Eindeutigkeit prüft der WatchlistService per Aktiv-Abfrage – analog FraktionMitglied).
        });

        // ---- Phase 5c: Taskforces ----
        modelBuilder.Entity<Taskforce>(b =>
        {
            b.Property(t => t.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(t => t.Name).HasMaxLength(200).IsRequired();
            b.Property(t => t.Purpose).HasMaxLength(4000);
            b.Property(t => t.Remarks).HasMaxLength(2000);
            b.HasIndex(t => t.CaseNumber).IsUnique();
            b.HasIndex(t => t.Name);
            b.HasIndex(t => t.Status);
            b.HasIndex(t => t.IsClassified);

            b.HasMany(t => t.Agents).WithOne(a => a.Taskforce!)
                .HasForeignKey(a => a.TaskforceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskforceAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.TaskforceId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        // ---- Phase 5d: Taskforce-Chat ----
        modelBuilder.Entity<TaskforceMessage>(b =>
        {
            b.Property(n => n.Text).HasMaxLength(4000).IsRequired();
            b.Property(n => n.AuthorName).HasMaxLength(128);
            // Chronologisches Laden je Taskforce (jüngste zuerst).
            b.HasIndex(n => new { n.TaskforceId, n.CreatedAt });
            b.HasOne(n => n.Taskforce).WithMany()
                .HasForeignKey(n => n.TaskforceId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Phase 5e: Personalakte je Agent (Kind-Daten an AgentId; FK auf Identity-Agent = Restrict) ----
        modelBuilder.Entity<AgentRankHistory>(b =>
        {
            b.Property(v => v.ActorName).HasMaxLength(128);
            b.Property(v => v.Reason).HasMaxLength(500);
            b.HasIndex(v => new { v.AgentId, v.Timestamp });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(v => v.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentNote>(b =>
        {
            b.Property(v => v.Text).HasMaxLength(4000).IsRequired();
            b.Property(v => v.AuthorName).HasMaxLength(128);
            b.HasIndex(v => new { v.AgentId, v.Kind });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(v => v.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentPromotionRequest>(b =>
        {
            b.Property(a => a.Justification).HasMaxLength(2000);
            b.Property(a => a.RequesterName).HasMaxLength(128);
            b.Property(a => a.DeciderName).HasMaxLength(128);
            b.Property(a => a.DecisionNote).HasMaxLength(2000);
            b.HasIndex(a => new { a.AgentId, a.Status });
            b.HasIndex(a => a.Status);
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Phase 7 (Abschluss): Systemeinstellungen, Gesetzbuch, Datei-Bibliothek ----
        modelBuilder.Entity<SystemSetting>(b =>
        {
            b.HasKey(e => e.Key);
            b.Property(e => e.Key).HasMaxLength(128);
            // Wert bewusst ohne HasMaxLength (longtext) – trägt auch längere Banner-/Wartungstexte.
        });

        modelBuilder.Entity<Law>(b =>
        {
            b.Property(g => g.LawBook).HasMaxLength(128).IsRequired();
            b.Property(g => g.Paragraph).HasMaxLength(32).IsRequired();
            b.Property(g => g.Title).HasMaxLength(256).IsRequired();
            b.Property(g => g.Sentence).HasMaxLength(512);
            // Text bewusst ohne HasMaxLength (longtext).
            b.HasIndex(g => g.LawBook);
            b.HasIndex(g => g.Title);
        });

        modelBuilder.Entity<LibraryFile>(b =>
        {
            b.Property(d => d.Title).HasMaxLength(300).IsRequired();
            b.Property(d => d.Category).HasMaxLength(120);
            b.Property(d => d.OriginalName).HasMaxLength(260).IsRequired();
            b.Property(d => d.FileNameSaved).HasMaxLength(128).IsRequired();
            b.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
            b.HasIndex(d => d.Title);
            b.HasIndex(d => d.Category);
            b.HasIndex(d => d.IsClassified);
        });

        // Globaler Soft-Delete-Filter: jede Entität, die ISoftDelete implementiert, wird
        // standardmäßig ohne die als gelöscht markierten Datensätze abgefragt. Greift ab
        // Phase 2 (sobald Akten ISoftDelete nutzen) – das Plumbing steht hier bereits.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDelete).IsAssignableFrom(t.ClrType))
                     .ToList())
        {
            SetSoftDeleteFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(null, new object[] { modelBuilder });
        }
    }

    private static readonly MethodInfo SetSoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(SetSoftDeleteFilter),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ISoftDelete
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}

using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Antraege;
using NOOSE_Website.Data.Entities.Ankuendigungen;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Benachrichtigungen;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personal;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;
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
    public DbSet<ZugriffsLog> ZugriffsLogs => Set<ZugriffsLog>();

    // ---- Phase 2: Personen-Akten ----
    public DbSet<Person> Personen => Set<Person>();
    public DbSet<PersonDok> PersonDoks => Set<PersonDok>();
    public DbSet<PersonFoto> PersonFotos => Set<PersonFoto>();
    public DbSet<EinstufungVerlauf> EinstufungVerlauf => Set<EinstufungVerlauf>();
    public DbSet<PersonAlias> PersonAliase => Set<PersonAlias>();
    public DbSet<PersonTelefon> PersonTelefone => Set<PersonTelefon>();
    public DbSet<PersonFahrzeug> PersonFahrzeuge => Set<PersonFahrzeug>();
    public DbSet<PersonOrt> PersonOrte => Set<PersonOrt>();
    public DbSet<PersonWaffe> PersonWaffen => Set<PersonWaffe>();
    public DbSet<SteckbriefVorschlag> SteckbriefVorschlaege => Set<SteckbriefVorschlag>();
    public DbSet<AktenzeichenZaehler> AktenzeichenZaehler => Set<AktenzeichenZaehler>();

    // ---- Phase 3a: Querschnitt (Tags, Kommentare, Quellen) – generisch über EntitaetTyp/EntitaetId ----
    public DbSet<Quelle> Quellen => Set<Quelle>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TagZuordnung> TagZuordnungen => Set<TagZuordnung>();
    public DbSet<Kommentar> Kommentare => Set<Kommentar>();

    // ---- Phase 3b: Verknüpfungs-Engine (generisch) + Person-Beziehungen (typisiert) ----
    public DbSet<Verknuepfung> Verknuepfungen => Set<Verknuepfung>();
    public DbSet<PersonBeziehung> PersonBeziehungen => Set<PersonBeziehung>();

    // ---- Phase 3c: gespeicherte Suchen ----
    public DbSet<GespeicherteSuche> GespeicherteSuchen => Set<GespeicherteSuche>();

    // ---- Phase 4a: Fraktionen ----
    public DbSet<Fraktion> Fraktionen => Set<Fraktion>();
    public DbSet<FraktionRang> FraktionRaenge => Set<FraktionRang>();
    public DbSet<FraktionWaffenbestand> FraktionWaffenbestaende => Set<FraktionWaffenbestand>();
    public DbSet<FraktionLagerbestand> FraktionLagerbestaende => Set<FraktionLagerbestand>();
    public DbSet<FraktionMitglied> FraktionMitglieder => Set<FraktionMitglied>();
    public DbSet<FraktionAgent> FraktionAgenten => Set<FraktionAgent>();

    // ---- Phase 4b: Personengruppen ----
    public DbSet<Personengruppe> Personengruppen => Set<Personengruppe>();
    public DbSet<PersonengruppeMitglied> PersonengruppeMitglieder => Set<PersonengruppeMitglied>();
    public DbSet<PersonengruppeAgent> PersonengruppeAgenten => Set<PersonengruppeAgent>();

    // ---- Phase 5a: Parteien ----
    public DbSet<Partei> Parteien => Set<Partei>();
    public DbSet<ParteiMitglied> ParteiMitglieder => Set<ParteiMitglied>();
    public DbSet<ParteiAgent> ParteiAgenten => Set<ParteiAgent>();

    // ---- Phase 5b: Operationen ----
    public DbSet<Operation> Operationen => Set<Operation>();
    public DbSet<OperationAgent> OperationAgenten => Set<OperationAgent>();

    // ---- Phase 5: Vorgangs-/Fallakten ----
    public DbSet<Vorgang> Vorgaenge => Set<Vorgang>();
    public DbSet<VorgangAgent> VorgangAgenten => Set<VorgangAgent>();

    // ---- Phase 5c: Taskforces ----
    public DbSet<Taskforce> Taskforces => Set<Taskforce>();
    public DbSet<TaskforceAgent> TaskforceAgenten => Set<TaskforceAgent>();

    // ---- Phase 5d: Taskforce-Chat ----
    public DbSet<TaskforceNachricht> TaskforceNachrichten => Set<TaskforceNachricht>();

    // ---- Phase 5: Observationen (Überwachungseinträge an Personen) ----
    public DbSet<Observation> Observationen => Set<Observation>();

    // ---- Phase 5e: Personalakte je Agent ----
    public DbSet<AgentDienstgradVerlauf> AgentDienstgradVerlaeufe => Set<AgentDienstgradVerlauf>();
    public DbSet<AgentVermerk> AgentVermerke => Set<AgentVermerk>();
    public DbSet<AgentBefoerderungsantrag> AgentBefoerderungsantraege => Set<AgentBefoerderungsantrag>();

    // ---- Phase 5: Antrags-/Posteingang-Workflow (Hochstufung) ----
    public DbSet<Antrag> Antraege => Set<Antrag>();

    // ---- Phase 6: In-App-Benachrichtigungen (Glocke) ----
    public DbSet<Benachrichtigung> Benachrichtigungen => Set<Benachrichtigung>();

    // ---- Phase 6: Watchlist (gefolgte Akten) ----
    public DbSet<WatchlistEintrag> Watchlisten => Set<WatchlistEintrag>();

    // ---- Phase 6: Aufgaben/To-Dos & Zuweisungen ----
    public DbSet<Aufgabe> Aufgaben => Set<Aufgabe>();
    public DbSet<AufgabeZuweisung> AufgabeZuweisungen => Set<AufgabeZuweisung>();

    // ---- Phase 6: News/Schwarzes Brett + Behörden-Broadcast ----
    public DbSet<Ankuendigung> Ankuendigungen => Set<Ankuendigung>();
    public DbSet<AnkuendigungQuittierung> AnkuendigungQuittierungen => Set<AnkuendigungQuittierung>();

    // ---- Phase 7: Dok-Vorlagen (admin-definierte Erfassungsmasken) ----
    public DbSet<DokVorlage> DokVorlagen => Set<DokVorlage>();

    // ---- Phase 7: konfigurierbare Custom-Felder je Aktentyp ----
    public DbSet<CustomFeldDefinition> CustomFeldDefinitionen => Set<CustomFeldDefinition>();
    public DbSet<CustomFeldWert> CustomFeldWerte => Set<CustomFeldWert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Agent>(b =>
        {
            b.Property(a => a.Codename).HasMaxLength(128);
            b.Property(a => a.Klarname).HasMaxLength(128);
            b.Property(a => a.Dienstnummer).HasMaxLength(32);
            b.Property(a => a.DiscordId).HasMaxLength(32);
            b.Property(a => a.DiscordUsername).HasMaxLength(64);
            b.Property(a => a.AvatarUrl).HasMaxLength(512);
            b.Property(a => a.GesperrtGrund).HasMaxLength(512);
            b.HasIndex(a => a.DiscordId).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(a => a.EntitaetTyp).HasMaxLength(128);
            b.Property(a => a.EntitaetId).HasMaxLength(64);
            b.Property(a => a.AgentId).HasMaxLength(64);
            b.Property(a => a.AgentName).HasMaxLength(128);
            b.HasIndex(a => new { a.EntitaetTyp, a.EntitaetId });
            b.HasIndex(a => a.Zeitpunkt);
        });

        modelBuilder.Entity<ZugriffsLog>(b =>
        {
            b.Property(a => a.EntitaetTyp).HasMaxLength(128);
            b.Property(a => a.EntitaetId).HasMaxLength(64);
            b.Property(a => a.AgentId).HasMaxLength(64);
            b.Property(a => a.AgentName).HasMaxLength(128);
            b.HasIndex(a => new { a.EntitaetTyp, a.EntitaetId });
        });

        // ---- Phase 2: Personen-Akten ----
        modelBuilder.Entity<Person>(b =>
        {
            b.Property(p => p.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.HasIndex(p => p.Aktenzeichen).IsUnique();
            b.HasIndex(p => p.Name);
            b.HasIndex(p => p.IstVerschlusssache);

            b.HasMany(p => p.Aliase).WithOne(a => a.Person!)
                .HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Telefonnummern).WithOne(t => t.Person!)
                .HasForeignKey(t => t.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Fahrzeuge).WithOne(f => f.Person!)
                .HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Orte).WithOne(o => o.Person!)
                .HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Waffen).WithOne(w => w.Person!)
                .HasForeignKey(w => w.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Fotos).WithOne(f => f.Person!)
                .HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Doks).WithOne(d => d.Person!)
                .HasForeignKey(d => d.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Observationen).WithOne(o => o.Person!)
                .HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonAlias>(b => b.Property(a => a.Aliasname).HasMaxLength(200));
        modelBuilder.Entity<PersonTelefon>(b =>
        {
            b.Property(t => t.Nummer).HasMaxLength(40);
            b.Property(t => t.Bezeichnung).HasMaxLength(100);
            b.HasIndex(t => t.Nummer);
        });
        modelBuilder.Entity<PersonFahrzeug>(b =>
        {
            b.Property(f => f.Bezeichnung).HasMaxLength(200);
            b.Property(f => f.Kennzeichen).HasMaxLength(40);
        });
        modelBuilder.Entity<PersonOrt>(b =>
        {
            b.Property(o => o.Text).HasMaxLength(300);
            b.Property(o => o.Notiz).HasMaxLength(500);
        });
        modelBuilder.Entity<PersonWaffe>(b => b.Property(w => w.Text).HasMaxLength(200));

        modelBuilder.Entity<SteckbriefVorschlag>(b =>
        {
            b.Property(v => v.Wert).HasMaxLength(300).IsRequired();
            // Eindeutig je Typ+Wert (case-insensitiv über die DB-Collation) → keine doppelten Vorschläge.
            b.HasIndex(v => new { v.Typ, v.Wert }).IsUnique();
        });

        modelBuilder.Entity<PersonFoto>(b =>
        {
            b.Property(f => f.DateinameGespeichert).HasMaxLength(128);
            b.Property(f => f.OriginalName).HasMaxLength(260);
            b.Property(f => f.ContentType).HasMaxLength(100);
            b.Property(f => f.ErstelltVonId).HasMaxLength(64);
        });

        modelBuilder.Entity<EinstufungVerlauf>(b =>
        {
            b.Property(e => e.EntitaetTyp).HasMaxLength(128);
            b.Property(e => e.EntitaetId).HasMaxLength(64);
            b.Property(e => e.Begruendung).HasMaxLength(1000);
            b.Property(e => e.AgentId).HasMaxLength(64);
            b.Property(e => e.AgentName).HasMaxLength(128);
            b.Property(e => e.AntragId).HasMaxLength(64);
            b.HasIndex(e => new { e.EntitaetTyp, e.EntitaetId });
        });

        modelBuilder.Entity<PersonDok>(b =>
        {
            b.Property(d => d.Fraktion).HasMaxLength(200);
            // Lose Verknüpfung zu Fraktion/Personengruppe (kein FK – analog EntitaetTyp/EntitaetId).
            b.Property(d => d.OrgTyp).HasMaxLength(128);
            b.Property(d => d.OrgId).HasMaxLength(64);
            b.HasIndex(d => d.PersonId);
            b.HasIndex(d => new { d.OrgTyp, d.OrgId });
        });

        // Observation (Überwachungseintrag an einer Person) – Kind der Person wie PersonDok.
        modelBuilder.Entity<Observation>(b =>
        {
            b.Property(o => o.Ort).HasMaxLength(300);
            b.Property(o => o.Beobachtung).HasMaxLength(4000);
            b.Property(o => o.Ergebnis).HasMaxLength(4000);
            // Lose Verknüpfung zu Fraktion/Personengruppe (kein FK – analog EntitaetTyp/EntitaetId).
            b.Property(o => o.OrgTyp).HasMaxLength(128);
            b.Property(o => o.OrgId).HasMaxLength(64);
            b.HasIndex(o => o.PersonId);
            b.HasIndex(o => o.Beginn);
            b.HasIndex(o => new { o.OrgTyp, o.OrgId });
            // FK auf den beobachtenden Identity-Agent: SetNull, damit ein gelöschter Agent die Observation
            // nicht mitlöscht (die Beziehung zur Person ist im Person-Block mit Cascade konfiguriert).
            b.HasOne(o => o.BeobachtenderAgent).WithMany()
                .HasForeignKey(o => o.BeobachtenderAgentId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(o => o.BeobachtenderAgentId);
        });

        modelBuilder.Entity<AktenzeichenZaehler>(b =>
        {
            // Zusammengesetzter Schlüssel (Praefix, Jahr) → eine eigene Sequenz je Aktentyp und Jahr.
            b.HasKey(z => new { z.Praefix, z.Jahr });
            b.Property(z => z.Praefix).HasMaxLength(8);
            // Jahr ist eine echte Jahreszahl (kein Auto-Increment) – wird beim Insert explizit gesetzt.
            b.Property(z => z.Jahr).ValueGeneratedNever();
        });

        // ---- Phase 3a: Querschnitt ----
        // Generische Assoziationen (Quelle/Tag-Zuordnung/Kommentar) verweisen polymorph per
        // EntitaetTyp+EntitaetId auf ihre Eltern-Akte (wie Audit-/Zugriffs-Log) – kein FK, daher
        // ist der kombinierte Index der schnelle Lade-Pfad „alle X einer Akte".
        modelBuilder.Entity<Quelle>(b =>
        {
            b.Property(q => q.EntitaetTyp).HasMaxLength(128);
            b.Property(q => q.EntitaetId).HasMaxLength(64);
            b.Property(q => q.Titel).HasMaxLength(300).IsRequired();
            b.Property(q => q.Url).HasMaxLength(2048);
            b.Property(q => q.ZielTyp).HasMaxLength(128);
            b.Property(q => q.ZielId).HasMaxLength(64);
            b.Property(q => q.DateinameGespeichert).HasMaxLength(128);
            b.Property(q => q.OriginalName).HasMaxLength(260);
            b.Property(q => q.ContentType).HasMaxLength(100);
            b.HasIndex(q => new { q.EntitaetTyp, q.EntitaetId });
        });

        modelBuilder.Entity<Tag>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(60).IsRequired();
            b.Property(t => t.Farbe).HasMaxLength(32);
            b.HasIndex(t => t.Name).IsUnique();
        });

        // ---- Phase 7: Dok-Vorlagen (admin-definierte Erfassungsmasken) ----
        modelBuilder.Entity<DokVorlage>(b =>
        {
            b.Property(v => v.Name).HasMaxLength(120).IsRequired();
            b.Property(v => v.Beschreibung).HasMaxLength(500);
            b.Property(v => v.StandardGrund).HasMaxLength(2000);
            b.Property(v => v.StandardFraktion).HasMaxLength(200);
            b.Property(v => v.StandardErhalteneInformationen).HasMaxLength(4000);
            // Kein Unique-Index: Soft-Delete würde sonst die Wieder-Anlage eines gelöschten Namens
            // blockieren (gelöschte Zeile belegt den Namen). Eindeutigkeit wird in DokVorlageService
            // geprüft (respektiert den Soft-Delete-Filter).
            b.HasIndex(v => v.Name);
            b.HasIndex(v => v.IstAktiv);
        });

        // ---- Phase 7: konfigurierbare Custom-Felder je Aktentyp ----
        modelBuilder.Entity<CustomFeldDefinition>(b =>
        {
            b.Property(d => d.EntitaetTyp).HasMaxLength(128).IsRequired();
            b.Property(d => d.Name).HasMaxLength(120).IsRequired();
            b.Property(d => d.Optionen).HasMaxLength(2000);
            // Kein Unique-Index (Soft-Delete würde Wieder-Anlage blockieren); Eindeutigkeit je
            // Aktentyp prüft CustomFeldDefinitionService (respektiert den Soft-Delete-Filter).
            b.HasIndex(d => new { d.EntitaetTyp, d.IstAktiv });
        });

        modelBuilder.Entity<CustomFeldWert>(b =>
        {
            b.Property(w => w.CustomFeldDefinitionId).HasMaxLength(64).IsRequired();
            b.Property(w => w.EntitaetTyp).HasMaxLength(128).IsRequired();
            b.Property(w => w.EntitaetId).HasMaxLength(64).IsRequired();
            b.Property(w => w.Wert).HasColumnType("longtext");
            b.HasIndex(w => new { w.EntitaetTyp, w.EntitaetId });
            // Ein Wert je Feld je Akte.
            b.HasIndex(w => new { w.CustomFeldDefinitionId, w.EntitaetTyp, w.EntitaetId }).IsUnique();
        });

        modelBuilder.Entity<TagZuordnung>(b =>
        {
            b.Property(z => z.EntitaetTyp).HasMaxLength(128);
            b.Property(z => z.EntitaetId).HasMaxLength(64);
            b.HasOne(z => z.Tag).WithMany().HasForeignKey(z => z.TagId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(z => new { z.EntitaetTyp, z.EntitaetId });
            // Verhindert Doppel-Tagging derselben Akte mit demselben Tag.
            b.HasIndex(z => new { z.TagId, z.EntitaetTyp, z.EntitaetId }).IsUnique();
        });

        modelBuilder.Entity<Kommentar>(b =>
        {
            b.Property(k => k.EntitaetTyp).HasMaxLength(128);
            b.Property(k => k.EntitaetId).HasMaxLength(64);
            b.Property(k => k.AutorName).HasMaxLength(128);
            b.HasIndex(k => new { k.EntitaetTyp, k.EntitaetId });
        });

        // ---- Phase 3b: Verknüpfungen + Person-Beziehungen ----
        modelBuilder.Entity<Verknuepfung>(b =>
        {
            b.Property(v => v.VonTyp).HasMaxLength(128);
            b.Property(v => v.VonId).HasMaxLength(64);
            b.Property(v => v.NachTyp).HasMaxLength(128);
            b.Property(v => v.NachId).HasMaxLength(64);
            b.Property(v => v.Label).HasMaxLength(200);
            // Beide Richtungen schnell auffindbar.
            b.HasIndex(v => new { v.VonTyp, v.VonId });
            b.HasIndex(v => new { v.NachTyp, v.NachId });
        });

        modelBuilder.Entity<PersonBeziehung>(b =>
        {
            b.Property(x => x.Notiz).HasMaxLength(500);
            // Zwei FKs auf dieselbe Tabelle (Personen) → Restrict statt Cascade (sonst „multiple cascade paths").
            b.HasOne(x => x.PersonA).WithMany()
                .HasForeignKey(x => x.PersonAId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PersonB).WithMany()
                .HasForeignKey(x => x.PersonBId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.PersonAId);
            b.HasIndex(x => x.PersonBId);
        });

        modelBuilder.Entity<GespeicherteSuche>(b =>
        {
            b.Property(g => g.AgentId).HasMaxLength(64);
            b.Property(g => g.Name).HasMaxLength(120).IsRequired();
            b.HasOne<Agent>().WithMany().HasForeignKey(g => g.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(g => g.AgentId);
        });

        // ---- Phase 4a: Fraktionen ----
        modelBuilder.Entity<Fraktion>(b =>
        {
            b.Property(f => f.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(f => f.Name).HasMaxLength(200).IsRequired();
            b.Property(f => f.Art).HasMaxLength(100);
            b.Property(f => f.Funk).HasMaxLength(100);
            b.Property(f => f.Darkchat).HasMaxLength(100);
            b.Property(f => f.Ausstellungszeiten).HasMaxLength(300);
            b.Property(f => f.Anwesen).HasMaxLength(500);
            b.Property(f => f.Erkennungsfarbe).HasMaxLength(32);
            b.Property(f => f.Ziele).HasMaxLength(2000);
            b.Property(f => f.Beschreibung).HasMaxLength(2000);
            b.HasIndex(f => f.Aktenzeichen).IsUnique();
            b.HasIndex(f => f.Name);
            b.HasIndex(f => f.IstVerschlusssache);

            b.HasMany(f => f.Raenge).WithOne(r => r.Fraktion!)
                .HasForeignKey(r => r.FraktionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Waffenbestand).WithOne(w => w.Fraktion!)
                .HasForeignKey(w => w.FraktionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Lagerbestand).WithOne(l => l.Fraktion!)
                .HasForeignKey(l => l.FraktionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Mitglieder).WithOne(m => m.Fraktion!)
                .HasForeignKey(m => m.FraktionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Agenten).WithOne(a => a.Fraktion!)
                .HasForeignKey(a => a.FraktionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FraktionAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.FraktionId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        modelBuilder.Entity<FraktionRang>(b =>
        {
            b.Property(r => r.Bezeichnung).HasMaxLength(100).IsRequired();
        });
        modelBuilder.Entity<FraktionWaffenbestand>(b =>
        {
            b.Property(w => w.Bezeichnung).HasMaxLength(200).IsRequired();
            b.Property(w => w.Menge).HasMaxLength(50);
        });
        modelBuilder.Entity<FraktionLagerbestand>(b =>
        {
            b.Property(l => l.Bezeichnung).HasMaxLength(200).IsRequired();
            b.Property(l => l.Menge).HasMaxLength(50);
        });

        modelBuilder.Entity<FraktionMitglied>(b =>
        {
            b.Property(m => m.Rang).HasMaxLength(100);
            // FK auf Person mit Restrict (Fraktion cascadet bereits auf diese Tabelle → sonst „multiple cascade paths").
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // Kein Unique-Index: Mitgliedschaften sind soft-deletebar (Verlauf), und ein Wiedereintritt legt
            // eine neue aktive Zeile neben der beendeten an. Aktiv-Eindeutigkeit prüft FraktionService.
            b.HasIndex(m => new { m.FraktionId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        // ---- Phase 4b: Personengruppen ----
        modelBuilder.Entity<Personengruppe>(b =>
        {
            b.Property(g => g.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(g => g.Name).HasMaxLength(200).IsRequired();
            b.Property(g => g.Beschreibung).HasMaxLength(2000);
            b.Property(g => g.Ziele).HasMaxLength(2000);
            b.HasIndex(g => g.Aktenzeichen).IsUnique();
            b.HasIndex(g => g.Name);
            b.HasIndex(g => g.IstVerschlusssache);

            b.HasMany(g => g.Mitglieder).WithOne(m => m.Personengruppe!)
                .HasForeignKey(m => m.PersonengruppeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(g => g.Agenten).WithOne(a => a.Personengruppe!)
                .HasForeignKey(a => a.PersonengruppeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonengruppeMitglied>(b =>
        {
            b.Property(m => m.Rolle).HasMaxLength(100);
            // FK auf Person mit Restrict (Gruppe cascadet bereits auf diese Tabelle → sonst „multiple cascade paths").
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // Kein Unique-Index: soft-deletebar (Verlauf) + Wiedereintritt; Aktiv-Eindeutigkeit prüft PersonengruppeService.
            b.HasIndex(m => new { m.PersonengruppeId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        modelBuilder.Entity<PersonengruppeAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.PersonengruppeId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        // ---- Phase 5a: Parteien ----
        modelBuilder.Entity<Partei>(b =>
        {
            b.Property(p => p.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Beschreibung).HasMaxLength(2000);
            b.Property(p => p.Ziele).HasMaxLength(2000);
            b.Property(p => p.Bemerkungen).HasMaxLength(2000);
            b.HasIndex(p => p.Aktenzeichen).IsUnique();
            b.HasIndex(p => p.Name);
            b.HasIndex(p => p.IstVerschlusssache);

            b.HasMany(p => p.Mitglieder).WithOne(m => m.Partei!)
                .HasForeignKey(m => m.ParteiId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Agenten).WithOne(a => a.Partei!)
                .HasForeignKey(a => a.ParteiId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParteiMitglied>(b =>
        {
            b.Property(m => m.Rolle).HasMaxLength(100);
            // FK auf Person mit Restrict (Partei cascadet bereits auf diese Tabelle → sonst „multiple cascade paths").
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // Kein Unique-Index: soft-deletebar (Verlauf) + Wiedereintritt; Aktiv-Eindeutigkeit prüft ParteiService.
            b.HasIndex(m => new { m.ParteiId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        modelBuilder.Entity<ParteiAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.ParteiId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        // ---- Phase 5b: Operationen ----
        modelBuilder.Entity<Operation>(b =>
        {
            b.Property(o => o.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(o => o.Titel).HasMaxLength(200).IsRequired();
            b.Property(o => o.Typ).HasMaxLength(100);
            b.Property(o => o.Ort).HasMaxLength(200);
            b.Property(o => o.Ablauf).HasMaxLength(4000);
            b.Property(o => o.Ergebnis).HasMaxLength(4000);
            b.Property(o => o.Bemerkungen).HasMaxLength(2000);
            b.HasIndex(o => o.Aktenzeichen).IsUnique();
            b.HasIndex(o => o.Titel);
            b.HasIndex(o => o.Status);
            b.HasIndex(o => o.IstVerschlusssache);

            b.HasMany(o => o.Agenten).WithOne(a => a.Operation!)
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
        modelBuilder.Entity<Vorgang>(b =>
        {
            b.Property(v => v.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(v => v.Titel).HasMaxLength(200).IsRequired();
            b.Property(v => v.Typ).HasMaxLength(100);
            b.Property(v => v.Beschreibung).HasMaxLength(4000);
            b.Property(v => v.Zusammenfassung).HasMaxLength(4000);
            b.Property(v => v.Abschlussvermerk).HasMaxLength(4000);
            b.HasIndex(v => v.Aktenzeichen).IsUnique();
            b.HasIndex(v => v.Titel);
            b.HasIndex(v => v.Status);
            b.HasIndex(v => v.IstVerschlusssache);

            b.HasMany(v => v.Agenten).WithOne(a => a.Vorgang!)
                .HasForeignKey(a => a.VorgangId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VorgangAgent>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.VorgangId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        // ---- Phase 6: Aufgaben/To-Dos & Zuweisungen ----
        modelBuilder.Entity<Aufgabe>(b =>
        {
            b.Property(a => a.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(a => a.Titel).HasMaxLength(200).IsRequired();
            b.Property(a => a.Beschreibung).HasMaxLength(4000);
            b.HasIndex(a => a.Aktenzeichen).IsUnique();
            b.HasIndex(a => a.Titel);
            b.HasIndex(a => a.Status);
            b.HasIndex(a => a.Faelligkeit);

            b.HasMany(a => a.Zuweisungen).WithOne(z => z.Aufgabe!)
                .HasForeignKey(z => z.AufgabeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AufgabeZuweisung>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(z => z.Agent).WithMany()
                .HasForeignKey(z => z.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(z => new { z.AufgabeId, z.AgentId }).IsUnique();
            b.HasIndex(z => z.AgentId);
        });

        // ---- Phase 6: News/Schwarzes Brett + Behörden-Broadcast ----
        modelBuilder.Entity<Ankuendigung>(b =>
        {
            b.Property(a => a.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(a => a.Titel).HasMaxLength(200).IsRequired();
            b.Property(a => a.ZielId).HasMaxLength(64);
            // Inhalt bewusst ohne HasMaxLength (longtext) – trägt @{Typ:Id}-Erwähnungstokens.
            b.HasIndex(a => a.Aktenzeichen).IsUnique();
            // Brett-Sortierung (Wichtig zuerst, dann neueste) und Zielgruppen-Filter.
            b.HasIndex(a => new { a.Wichtig, a.ErstelltAm });
            b.HasIndex(a => a.Zielgruppe);

            b.HasMany(a => a.Quittierungen).WithOne(q => q.Ankuendigung!)
                .HasForeignKey(q => q.AnkuendigungId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnkuendigungQuittierung>(b =>
        {
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne(q => q.Agent).WithMany()
                .HasForeignKey(q => q.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(q => new { q.AnkuendigungId, q.AgentId }).IsUnique();
            b.HasIndex(q => new { q.AgentId, q.QuittiertAm });
        });

        // ---- Phase 5: Antrags-/Posteingang-Workflow (Hochstufung) ----
        modelBuilder.Entity<Antrag>(b =>
        {
            b.Property(a => a.ZielTyp).HasMaxLength(128);
            b.Property(a => a.ZielId).HasMaxLength(64);
            b.Property(a => a.ZielBezeichnung).HasMaxLength(256);
            b.Property(a => a.Begruendung).HasMaxLength(2000);
            b.Property(a => a.Entscheidungsnotiz).HasMaxLength(2000);
            b.Property(a => a.AntragstellerName).HasMaxLength(100);
            b.Property(a => a.EntscheiderName).HasMaxLength(100);
            b.Property(a => a.ErstelltVonId).HasMaxLength(64);
            b.HasIndex(a => new { a.Typ, a.Status });
            b.HasIndex(a => new { a.ZielTyp, a.ZielId });
            b.HasIndex(a => a.ErstelltVonId);
        });

        // ---- Phase 6: In-App-Benachrichtigungen (Glocke) ----
        modelBuilder.Entity<Benachrichtigung>(b =>
        {
            b.Property(n => n.EmpfaengerId).HasMaxLength(64);
            b.Property(n => n.Titel).HasMaxLength(300);
            b.Property(n => n.Href).HasMaxLength(512);
            b.Property(n => n.ErstelltVonId).HasMaxLength(64);
            // Schneller Lade-Pfad „(ungelesene) Benachrichtigungen eines Empfängers".
            b.HasIndex(n => new { n.EmpfaengerId, n.GelesenAm });
        });

        // ---- Phase 6: Watchlist (gefolgte Akten) ----
        modelBuilder.Entity<WatchlistEintrag>(b =>
        {
            b.Property(w => w.AgentId).HasMaxLength(64);
            b.Property(w => w.EntitaetTyp).HasMaxLength(128);
            b.Property(w => w.EntitaetId).HasMaxLength(64);
            b.Property(w => w.ErstelltVonId).HasMaxLength(64);
            // FK auf den Identity-Agent mit Restrict (keine Cascade von der Nutzer-Tabelle).
            b.HasOne<Agent>().WithMany().HasForeignKey(w => w.AgentId).OnDelete(DeleteBehavior.Restrict);
            // Schneller Lade-Pfad „Folger einer Akte" (Fan-out) und „meine beobachteten Akten".
            b.HasIndex(w => new { w.EntitaetTyp, w.EntitaetId });
            b.HasIndex(w => new { w.AgentId, w.IstGeloescht });
            // Bewusst KEIN Unique-Index: Entfolgen = Soft-Delete, erneutes Folgen reaktiviert die Zeile
            // (Aktiv-Eindeutigkeit prüft der WatchlistService per Aktiv-Abfrage – analog FraktionMitglied).
        });

        // ---- Phase 5c: Taskforces ----
        modelBuilder.Entity<Taskforce>(b =>
        {
            b.Property(t => t.Aktenzeichen).HasMaxLength(32).IsRequired();
            b.Property(t => t.Name).HasMaxLength(200).IsRequired();
            b.Property(t => t.Zweck).HasMaxLength(4000);
            b.Property(t => t.Bemerkungen).HasMaxLength(2000);
            b.HasIndex(t => t.Aktenzeichen).IsUnique();
            b.HasIndex(t => t.Name);
            b.HasIndex(t => t.Status);
            b.HasIndex(t => t.IstVerschlusssache);

            b.HasMany(t => t.Agenten).WithOne(a => a.Taskforce!)
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
        modelBuilder.Entity<TaskforceNachricht>(b =>
        {
            b.Property(n => n.Text).HasMaxLength(4000).IsRequired();
            b.Property(n => n.AutorName).HasMaxLength(128);
            // Chronologisches Laden je Taskforce (jüngste zuerst).
            b.HasIndex(n => new { n.TaskforceId, n.ErstelltAm });
            b.HasOne(n => n.Taskforce).WithMany()
                .HasForeignKey(n => n.TaskforceId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Phase 5e: Personalakte je Agent (Kind-Daten an AgentId; FK auf Identity-Agent = Restrict) ----
        modelBuilder.Entity<AgentDienstgradVerlauf>(b =>
        {
            b.Property(v => v.AkteurName).HasMaxLength(128);
            b.Property(v => v.Grund).HasMaxLength(500);
            b.HasIndex(v => new { v.AgentId, v.Zeitpunkt });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(v => v.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentVermerk>(b =>
        {
            b.Property(v => v.Text).HasMaxLength(4000).IsRequired();
            b.Property(v => v.AutorName).HasMaxLength(128);
            b.HasIndex(v => new { v.AgentId, v.Art });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(v => v.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentBefoerderungsantrag>(b =>
        {
            b.Property(a => a.Begruendung).HasMaxLength(2000);
            b.Property(a => a.AntragstellerName).HasMaxLength(128);
            b.Property(a => a.EntscheiderName).HasMaxLength(128);
            b.Property(a => a.Entscheidungsnotiz).HasMaxLength(2000);
            b.HasIndex(a => new { a.AgentId, a.Status });
            b.HasIndex(a => a.Status);
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IstGeloescht);
    }
}

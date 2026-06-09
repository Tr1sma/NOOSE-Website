using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
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
    public DbSet<AktenzeichenZaehler> AktenzeichenZaehler => Set<AktenzeichenZaehler>();

    // ---- Phase 3a: Querschnitt (Tags, Kommentare, Quellen) – generisch über EntitaetTyp/EntitaetId ----
    public DbSet<Quelle> Quellen => Set<Quelle>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TagZuordnung> TagZuordnungen => Set<TagZuordnung>();
    public DbSet<Kommentar> Kommentare => Set<Kommentar>();

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
            b.HasMany(p => p.EinstufungVerlauf).WithOne(e => e.Person!)
                .HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<PersonFoto>(b =>
        {
            b.Property(f => f.DateinameGespeichert).HasMaxLength(128);
            b.Property(f => f.OriginalName).HasMaxLength(260);
            b.Property(f => f.ContentType).HasMaxLength(100);
            b.Property(f => f.ErstelltVonId).HasMaxLength(64);
        });

        modelBuilder.Entity<EinstufungVerlauf>(b =>
        {
            b.Property(e => e.Begruendung).HasMaxLength(1000);
            b.Property(e => e.AgentId).HasMaxLength(64);
            b.Property(e => e.AgentName).HasMaxLength(128);
            b.Property(e => e.AntragId).HasMaxLength(64);
            b.HasIndex(e => e.PersonId);
        });

        modelBuilder.Entity<PersonDok>(b =>
        {
            b.Property(d => d.Fraktion).HasMaxLength(200);
            b.HasIndex(d => d.PersonId);
        });

        modelBuilder.Entity<AktenzeichenZaehler>(b =>
        {
            b.HasKey(z => z.Jahr);
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

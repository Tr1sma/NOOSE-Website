using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Agent>(b =>
        {
            b.Property(a => a.Anzeigename).HasMaxLength(128);
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

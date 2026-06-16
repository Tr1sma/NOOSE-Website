using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITagService" />
public class TagService(IDbContextFactory<AppDbContext> dbFactory) : ITagService
{
    public async Task<List<Tag>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tags.OrderBy(t => t.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<TagUsage>> GetWithUsageAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var tags = await db.Tags.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var counts = await db.TagMappings
            .GroupBy(z => z.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var map = counts.ToDictionary(x => x.TagId, x => x.Count);
        return tags.Select(t => new TagUsage(t, map.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<Tag> CreateAsync(string name, string? colour, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Tag-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Tags.AnyAsync(t => t.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Ein Tag „{name}“ existiert bereits.");
        }

        var tag = new Tag { Name = name, Colour = colour.TrimToNull() };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);
        return tag;
    }

    public async Task RefreshAsync(string tagId, string name, string? colour, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Tags anlegen darf jeder aktive Agent; Umbenennen/Einfärben ist Verwaltung → Führung/Admin.
        Permission.RequireLeadership(actor);

        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Tag-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken)
            ?? throw new InvalidOperationException("Tag nicht gefunden.");
        if (await db.Tags.AnyAsync(t => t.Id != tagId && t.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Ein Tag „{name}“ existiert bereits.");
        }

        tag.Name = name;
        tag.Colour = colour.TrimToNull();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string tagId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Hart-Löschen entfernt das Tag von ALLEN Akten (destruktiv) → Führung/Admin.
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken);
        if (tag is null)
        {
            return;
        }
        // Hart löschen; die Zuordnungen werden per FK-Cascade (OnDelete) mitentfernt.
        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Tag>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tags
            .Where(t => db.TagMappings.Any(z => z.TagId == t.Id && z.EntityType == entityType && z.EntityId == entityId))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task SetAsync(string entityType, string entityId, IEnumerable<string> tagIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var target = tagIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToHashSet();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.TagMappings
            .Where(z => z.EntityType == entityType && z.EntityId == entityId)
            .ToListAsync(cancellationToken);
        var existingIds = existing.Select(z => z.TagId).ToHashSet();

        var toRemove = existing.Where(z => !target.Contains(z.TagId)).ToList();
        var toSupplement = target
            .Where(id => !existingIds.Contains(id))
            .Select(id => new TagMapping { TagId = id, EntityType = entityType, EntityId = entityId })
            .ToList();

        if (toRemove.Count == 0 && toSupplement.Count == 0)
        {
            return;
        }

        db.TagMappings.RemoveRange(toRemove);
        db.TagMappings.AddRange(toSupplement);
        await db.SaveChangesAsync(cancellationToken);
    }
}

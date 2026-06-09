using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITagService" />
public class TagService(AppDbContext db) : ITagService
{
    public Task<List<Tag>> GetAlleAsync(CancellationToken cancellationToken = default)
        => db.Tags.OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public async Task<List<TagVerwendung>> GetMitVerwendungAsync(CancellationToken cancellationToken = default)
    {
        var tags = await db.Tags.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var anzahlen = await db.TagZuordnungen
            .GroupBy(z => z.TagId)
            .Select(g => new { TagId = g.Key, Anzahl = g.Count() })
            .ToListAsync(cancellationToken);
        var map = anzahlen.ToDictionary(x => x.TagId, x => x.Anzahl);
        return tags.Select(t => new TagVerwendung(t, map.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<Tag> ErstellenAsync(string name, string? farbe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Tag-Name darf nicht leer sein.");
        }
        if (await db.Tags.AnyAsync(t => t.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Ein Tag „{name}“ existiert bereits.");
        }

        var tag = new Tag { Name = name, Farbe = Leer(farbe) };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);
        return tag;
    }

    public async Task AktualisierenAsync(string tagId, string name, string? farbe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Tag-Name darf nicht leer sein.");
        }
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken)
            ?? throw new InvalidOperationException("Tag nicht gefunden.");
        if (await db.Tags.AnyAsync(t => t.Id != tagId && t.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Ein Tag „{name}“ existiert bereits.");
        }

        tag.Name = name;
        tag.Farbe = Leer(farbe);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string tagId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken);
        if (tag is null)
        {
            return;
        }
        // Hart löschen; die Zuordnungen werden per FK-Cascade (OnDelete) mitentfernt.
        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Tag>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, CancellationToken cancellationToken = default)
        => db.Tags
            .Where(t => db.TagZuordnungen.Any(z => z.TagId == t.Id && z.EntitaetTyp == entitaetTyp && z.EntitaetId == entitaetId))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public async Task SetzenAsync(string entitaetTyp, string entitaetId, IEnumerable<string> tagIds, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var ziel = tagIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToHashSet();

        var bestehende = await db.TagZuordnungen
            .Where(z => z.EntitaetTyp == entitaetTyp && z.EntitaetId == entitaetId)
            .ToListAsync(cancellationToken);
        var bestehendeIds = bestehende.Select(z => z.TagId).ToHashSet();

        var zuEntfernen = bestehende.Where(z => !ziel.Contains(z.TagId)).ToList();
        var zuErgaenzen = ziel
            .Where(id => !bestehendeIds.Contains(id))
            .Select(id => new TagZuordnung { TagId = id, EntitaetTyp = entitaetTyp, EntitaetId = entitaetId })
            .ToList();

        if (zuEntfernen.Count == 0 && zuErgaenzen.Count == 0)
        {
            return;
        }

        db.TagZuordnungen.RemoveRange(zuEntfernen);
        db.TagZuordnungen.AddRange(zuErgaenzen);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

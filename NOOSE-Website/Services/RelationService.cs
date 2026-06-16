using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBeziehungService" />
public class RelationService(IDbContextFactory<AppDbContext> dbFactory, IThreatScoreService threat) : IRelationService
{
    public async Task<List<RelationDisplay>> GetForPersonAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var raw = await db.PersonRelations
            .Include(b => b.PersonA)
            .Include(b => b.PersonB)
            .Where(b => b.PersonAId == personId || b.PersonBId == personId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var isLeadership = scope.MayClassifiedRead;
        // partners: only relations to released persons
        HashSet<string>? releasedOthers = null;
        if (scope.PartnerAgency is { } agency)
        {
            var otherIds = raw.Select(b => b.PersonAId == personId ? b.PersonBId : b.PersonAId).Distinct().ToList();
            releasedOthers = await PartnerVisibility.ReleasedParentIdsAsync(db, nameof(Person), otherIds, agency, cancellationToken);
        }

        var result = new List<RelationDisplay>();
        foreach (var b in raw)
        {
            // Die jeweils andere Person bestimmen.
            var other = b.PersonAId == personId ? b.PersonB : b.PersonA;
            if (other is null)
            {
                continue; // Gegenseite im Papierkorb (Query-Filter) → ausblenden.
            }
            if (other.IsClassified && !isLeadership)
            {
                continue;
            }
            if (releasedOthers is not null && !releasedOthers.Contains(other.Id))
            {
                continue;
            }
            result.Add(new RelationDisplay(b.Id, b.Type, b.Note, other.Id, other.Name, other.CaseNumber));
        }
        return result;
    }

    public async Task CreateAsync(string personAId, string personBId, RelationType type, string? note, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(personBId) || personAId == personBId)
        {
            throw new InvalidOperationException("Bitte eine andere Person als Gegenüber auswählen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.People.AnyAsync(p => p.Id == personBId, cancellationToken))
        {
            throw new InvalidOperationException("Die gewählte Person wurde nicht gefunden.");
        }

        // Dieselbe Beziehung (gleicher Typ) zwischen beiden Personen nicht doppelt anlegen – in beiden
        // Richtungen geprüft. Andere Beziehungstypen zwischen denselben Personen bleiben erlaubt.
        var exists = await db.PersonRelations.AnyAsync(b => b.Type == type
            && ((b.PersonAId == personAId && b.PersonBId == personBId)
             || (b.PersonAId == personBId && b.PersonBId == personAId)),
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Eine Beziehung dieses Typs besteht bereits zwischen den beiden Personen.");
        }

        db.PersonRelations.Add(new PersonRelation
        {
            PersonAId = personAId,
            PersonBId = personBId,
            Type = type,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        });
        await db.SaveChangesAsync(cancellationToken);
        // Feind/Verbündeter/Geschäftspartner wirken auf P4 BEIDER Personen → beide neu berechnen.
        await threat.NewCalculatePersonScoreAsync(personAId, cancellationToken);
        await threat.NewCalculatePersonScoreAsync(personBId, cancellationToken);
    }

    public async Task RemoveAsync(string relationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var b = await db.PersonRelations.FirstOrDefaultAsync(x => x.Id == relationId, cancellationToken);
        if (b is null)
        {
            return;
        }
        var personAId = b.PersonAId;
        var personBId = b.PersonBId;
        db.PersonRelations.Remove(b); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
        // Entfernte Beziehung wirkt auf P4 beider Personen → beide neu berechnen.
        await threat.NewCalculatePersonScoreAsync(personAId, cancellationToken);
        await threat.NewCalculatePersonScoreAsync(personBId, cancellationToken);
    }
}

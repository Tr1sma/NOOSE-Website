using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

public class CustomFieldValueService(IDbContextFactory<AppDbContext> dbFactory) : ICustomFieldValueService
{
    public async Task<List<CustomFieldValueDisplay>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default, ViewerScope? scope = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // partners: only when the whole record is released
        if (scope is { PartnerAgency: { } agency }
            && (!await PartnerVisibility.IsRecordVisibleToPartnerAsync(db, entityType, entityId, agency, scope?.MeId, cancellationToken)
                || !await PartnerVisibility.ParentIncludesChildrenAsync(db, entityType, entityId, agency, scope?.MeId, cancellationToken)))
        {
            return new List<CustomFieldValueDisplay>();
        }

        var definitions = await db.CustomFieldDefinitions
            .Where(d => d.EntityType == entityType && d.IsActive)
            .OrderBy(d => d.Order).ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
        if (definitions.Count == 0)
        {
            return new List<CustomFieldValueDisplay>();
        }

        var values = await db.CustomFieldValues
            .Where(w => w.EntityType == entityType && w.EntityId == entityId)
            .ToListAsync(cancellationToken);
        var valuePerDef = values
            .GroupBy(w => w.CustomFieldDefinitionId)
            .ToDictionary(g => g.Key, g => g.First().Value);

        return definitions
            .Select(d => new CustomFieldValueDisplay
            {
                Definition = d,
                Value = valuePerDef.GetValueOrDefault(d.Id),
            })
            .ToList();
    }

    public async Task SetAsync(string entityType, string entityId, IReadOnlyDictionary<string, string?> valuesPerDefinition,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var definitions = await db.CustomFieldDefinitions
            .Where(d => d.EntityType == entityType && d.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var def in definitions.Where(d => d.Mandatory))
        {
            var value = valuesPerDefinition.GetValueOrDefault(def.Id);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Das Pflichtfeld „{def.Name}“ muss ausgefüllt werden.");
            }
        }

        var existing = await db.CustomFieldValues
            .Where(w => w.EntityType == entityType && w.EntityId == entityId)
            .ToListAsync(cancellationToken);
        var existingPerDef = existing.ToDictionary(w => w.CustomFieldDefinitionId);

        foreach (var def in definitions)
        {
            var @new = valuesPerDefinition.GetValueOrDefault(def.Id).TrimToNull();
            existingPerDef.TryGetValue(def.Id, out var exists);

            if (@new is null)
            {
                if (exists is not null)
                {
                    db.CustomFieldValues.Remove(exists);
                }
                continue;
            }

            if (exists is null)
            {
                db.CustomFieldValues.Add(new CustomFieldValue
                {
                    CustomFieldDefinitionId = def.Id,
                    EntityType = entityType,
                    EntityId = entityId,
                    Value = @new,
                });
            }
            else if (exists.Value != @new)
            {
                exists.Value = @new;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

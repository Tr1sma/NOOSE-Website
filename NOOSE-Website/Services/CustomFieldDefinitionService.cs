using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ICustomFeldDefinitionService" />
public class CustomFieldDefinitionService(IDbContextFactory<AppDbContext> dbFactory) : ICustomFieldDefinitionService
{
    public async Task<List<CustomFieldDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CustomFieldDefinitions
            .OrderBy(d => d.EntityType).ThenBy(d => d.Order).ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CustomFieldDefinition>> GetForTypeAsync(string entityType, bool onlyActive, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.CustomFieldDefinitions.Where(d => d.EntityType == entityType);
        if (onlyActive)
        {
            query = query.Where(d => d.IsActive);
        }
        return await query
            .OrderBy(d => d.Order).ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomFieldDefinition> CreateAsync(CustomFieldDefinitionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var (name, entityType) = Validate(input);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.CustomFieldDefinitions.AnyAsync(d => d.EntityType == entityType && d.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Für diesen Aktentyp existiert bereits ein Feld „{name}“.");
        }

        var definition = new CustomFieldDefinition { Name = name, EntityType = entityType };
        Apply(definition, input);
        db.CustomFieldDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);
        return definition;
    }

    public async Task RefreshAsync(string id, CustomFieldDefinitionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var (name, entityType) = Validate(input);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var definition = await db.CustomFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Feld-Definition nicht gefunden.");
        if (await db.CustomFieldDefinitions.AnyAsync(d => d.Id != id && d.EntityType == entityType && d.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Für diesen Aktentyp existiert bereits ein Feld „{name}“.");
        }

        definition.Name = name;
        definition.EntityType = entityType;
        Apply(definition, input);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var definition = await db.CustomFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (definition is null)
        {
            return;
        }
        // Remove wird vom AuditSaveChangesInterceptor in einen Soft-Delete (Papierkorb) umgewandelt.
        // Bereits erfasste Werte bleiben bestehen, werden aber nicht mehr angezeigt (Filter auf aktive Felder).
        db.CustomFieldDefinitions.Remove(definition);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static (string Name, string EntityType) Validate(CustomFieldDefinitionInput input)
    {
        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Feld-Name darf nicht leer sein.");
        }
        var entityType = (input.EntityType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new InvalidOperationException("Es muss ein Aktentyp gewählt werden.");
        }
        if (input.FieldType == CustomFieldType.Selection && string.IsNullOrWhiteSpace(input.Options))
        {
            throw new InvalidOperationException("Für ein Auswahl-Feld muss mindestens eine Option angegeben werden.");
        }
        return (name, entityType);
    }

    /// <summary>Überträgt die editierbaren Felder (ohne Name/EntitaetTyp – die werden vorab validiert/gesetzt).</summary>
    private static void Apply(CustomFieldDefinition definition, CustomFieldDefinitionInput input)
    {
        definition.FieldType = input.FieldType;
        definition.Options = input.FieldType == CustomFieldType.Selection ? input.Options.TrimToNull() : null;
        definition.Mandatory = input.Mandatory;
        definition.Order = input.Order;
        definition.IsActive = input.IsActive;
    }
}

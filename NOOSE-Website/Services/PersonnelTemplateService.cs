using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonnelTemplateService" />
public class PersonnelTemplateService(IDbContextFactory<AppDbContext> dbFactory) : IPersonnelTemplateService
{
    public async Task<List<PersonnelTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonnelTemplates
            .OrderBy(v => v.Kind).ThenBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonnelTemplate>> GetActiveAsync(PersonnelTemplateKind kind, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonnelTemplates
            .Where(v => v.IsActive && v.Kind == kind)
            .OrderBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PersonnelTemplate?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonnelTemplates.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<PersonnelTemplate> CreateAsync(PersonnelTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.PersonnelTemplates.AnyAsync(v => v.Kind == input.Kind && v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert in dieser Kategorie bereits.");
        }

        var template = new PersonnelTemplate { Kind = input.Kind, Name = name };
        Apply(template, input);
        db.PersonnelTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task RefreshAsync(string id, PersonnelTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.PersonnelTemplates.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        if (await db.PersonnelTemplates.AnyAsync(v => v.Id != id && v.Kind == input.Kind && v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert in dieser Kategorie bereits.");
        }

        template.Kind = input.Kind;
        template.Name = name;
        Apply(template, input);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.PersonnelTemplates.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (template is null)
        {
            return;
        }
        // Interceptor rewrites Remove to soft-delete.
        db.PersonnelTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Copies the editable fields and sanitizes the HTML body; name is validated beforehand.</summary>
    private static void Apply(PersonnelTemplate template, PersonnelTemplateInput input)
    {
        template.Description = input.Description.TrimToNull();
        template.ContentHtml = HtmlCleanup.Clean(input.ContentHtml);
        template.IsActive = input.IsActive;
        template.Sorting = input.Sorting;
    }
}

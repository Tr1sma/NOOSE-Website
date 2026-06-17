using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

public class DocumentTemplateService(IDbContextFactory<AppDbContext> dbFactory) : IDocumentTemplateService
{
    public async Task<List<DocumentTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentTemplates
            .OrderBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DocumentTemplate>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentTemplates
            .Where(v => v.IsActive)
            .OrderBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentTemplate?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentTemplates.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<DocumentTemplate> CreateAsync(DocumentTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.DocumentTemplates.AnyAsync(v => v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }

        var template = new DocumentTemplate { Name = name };
        Apply(template, input);
        db.DocumentTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task RefreshAsync(string id, DocumentTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.DocumentTemplates.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        if (await db.DocumentTemplates.AnyAsync(v => v.Id != id && v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }

        template.Name = name;
        Apply(template, input);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.DocumentTemplates.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (template is null)
        {
            return;
        }
        // Interceptor rewrites Remove to soft-delete.
        db.DocumentTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Copies the editable fields and sanitizes the HTML body; name is validated beforehand.</summary>
    private static void Apply(DocumentTemplate template, DocumentTemplateInput input)
    {
        template.Description = input.Description.TrimToNull();
        template.Category = input.Category.TrimToNull();
        template.ContentHtml = HtmlCleanup.Clean(input.ContentHtml);
        template.IsActive = input.IsActive;
        template.Sorting = input.Sorting;
    }
}

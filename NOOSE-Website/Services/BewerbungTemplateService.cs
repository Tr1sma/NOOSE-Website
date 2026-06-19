using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBewerbungTemplateService" />
public class BewerbungTemplateService(IDbContextFactory<AppDbContext> dbFactory) : IBewerbungTemplateService
{
    private const string Category = RecruitingSeeder.TemplateCategory;

    public async Task<List<DocumentTemplate>> ListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentTemplates
            .Where(v => v.Category == Category)
            .OrderBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentTemplate?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentTemplates.FirstOrDefaultAsync(v => v.Id == id && v.Category == Category, cancellationToken);
    }

    public async Task<DocumentTemplate> CreateAsync(string name, string? description, string contentHtml, bool isActive, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.DocumentTemplates.AnyAsync(v => v.Category == Category && v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }
        var max = await db.DocumentTemplates.Where(v => v.Category == Category)
            .MaxAsync(v => (int?)v.Sorting, cancellationToken) ?? 0;

        var template = new DocumentTemplate
        {
            Name = name,
            Category = Category,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ContentHtml = HtmlCleanup.Clean(contentHtml),
            IsActive = isActive,
            Sorting = max + 1,
        };
        db.DocumentTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task UpdateAsync(string id, string name, string? description, string contentHtml, bool isActive, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.DocumentTemplates.FirstOrDefaultAsync(v => v.Id == id && v.Category == Category, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        if (await db.DocumentTemplates.AnyAsync(v => v.Id != id && v.Category == Category && v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }
        template.Name = name;
        template.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        template.ContentHtml = HtmlCleanup.Clean(contentHtml);
        template.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.DocumentTemplates.FirstOrDefaultAsync(v => v.Id == id && v.Category == Category, cancellationToken);
        if (template is null)
        {
            return;
        }
        db.DocumentTemplates.Remove(template); // soft-delete via interceptor
        await db.SaveChangesAsync(cancellationToken);
    }
}

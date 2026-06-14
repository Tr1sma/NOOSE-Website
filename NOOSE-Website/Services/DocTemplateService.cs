using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IDokVorlageService" />
public class DocTemplateService(IDbContextFactory<AppDbContext> dbFactory) : IDocTemplateService
{
    public async Task<List<DocTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocTemplates
            .OrderBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DocTemplate>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocTemplates
            .Where(v => v.IsActive)
            .OrderBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocTemplate> CreateAsync(DocTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.DocTemplates.AnyAsync(v => v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }

        var template = new DocTemplate { Name = name };
        Apply(template, input);
        db.DocTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task RefreshAsync(string id, DocTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.DocTemplates.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        if (await db.DocTemplates.AnyAsync(v => v.Id != id && v.Name == name, cancellationToken))
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
        var template = await db.DocTemplates.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (template is null)
        {
            return;
        }
        // Remove wird vom AuditSaveChangesInterceptor in einen Soft-Delete (Papierkorb) umgewandelt.
        db.DocTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Überträgt die editierbaren Felder der Eingabe auf die Vorlage (ohne Name – der wird vorab validiert).</summary>
    private static void Apply(DocTemplate template, DocTemplateInput input)
    {
        template.Description = input.Description.TrimToNull();
        template.IsActive = input.IsActive;
        template.Sorting = input.Sorting;
        template.DefaultReason = input.DefaultReason.TrimToNull();
        template.DefaultFaction = input.DefaultFaction.TrimToNull();
        template.DefaultReceivedInformation = input.DefaultReceivedInformation.TrimToNull();
        template.DefaultTruthSerum = input.DefaultTruthSerum;
        template.DefaultOutcome = input.DefaultOutcome;
    }
}

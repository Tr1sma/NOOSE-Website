using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicTemplateService" />
public class PublicTemplateService(IDbContextFactory<AppDbContext> dbFactory) : IPublicTemplateService
{
    public async Task<IReadOnlyList<PublicTemplateRow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheVorlagen
            .AsNoTracking()
            .OrderBy(v => v.Kind).ThenBy(v => v.SortOrder).ThenBy(v => v.Title)
            .Select(Projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicTemplateRow>> GetActiveAsync(PublicTemplateKind kind,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheVorlagen
            .AsNoTracking()
            .Where(v => v.Kind == kind && v.IsActive)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Title)
            .Select(Projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<PublicTemplateRow?> GetAutomaticAsync(PublicTemplateKind kind,
        CancellationToken cancellationToken = default)
        => (await GetActiveAsync(kind, cancellationToken)).FirstOrDefault();

    public async Task<PublicTemplateRow?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheVorlagen
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(Projection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> SaveAsync(PublicTemplateInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicTemplateWrite(actor);

        // the panel groups by the known kinds, so a value outside them would store a row nothing renders again
        if (!PublicTemplateKindDisplay.All.Contains(input.Kind))
        {
            throw new InvalidOperationException("Unbekannte Vorlagen-Art.");
        }

        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length < PublicTemplateRules.TitleMinLength)
        {
            throw new InvalidOperationException(
                $"Der Titel braucht mindestens {PublicTemplateRules.TitleMinLength} Zeichen.");
        }
        if (title.Length > PublicTemplateRules.TitleMaxLength)
        {
            throw new InvalidOperationException(
                $"Der Titel fasst höchstens {PublicTemplateRules.TitleMaxLength} Zeichen.");
        }

        // Trim only at the ends: the body is plain text and its line breaks are the formatting
        var text = (input.Text ?? string.Empty).Trim();
        if (text.Length < PublicTemplateRules.MinLength)
        {
            throw new InvalidOperationException(
                $"Der Text braucht mindestens {PublicTemplateRules.MinLength} Zeichen.");
        }
        if (text.Length > PublicTemplateRules.MaxLength)
        {
            throw new InvalidOperationException(
                $"Der Text fasst höchstens {PublicTemplateRules.MaxLength} Zeichen.");
        }
        // refused rather than half-expanded on send: a token of another system travels to the citizen as literal text
        if (PublicTemplateRenderer.HasForeignToken(text))
        {
            throw new InvalidOperationException(
                "Der Text enthält Platzhalter eines anderen Systems ({{…}}, @{…}, BEWERBER, DIENSTGRAD). "
                + "Erlaubt sind BUERGER, AKTENZEICHEN, DATUM, UHRZEIT und NAME.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        OeffentlicheVorlage row;
        if (string.IsNullOrWhiteSpace(input.Id))
        {
            row = new OeffentlicheVorlage { Kind = input.Kind };
            db.OeffentlicheVorlagen.Add(row);
        }
        else
        {
            row = await db.OeffentlicheVorlagen.FirstOrDefaultAsync(v => v.Id == input.Id, cancellationToken)
                ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
            row.Kind = input.Kind;
        }

        row.Title = title;
        // stored with its tokens: they are the payload here, and expansion belongs to the moment of applying
        row.Text = text;
        row.IsActive = input.IsActive;
        row.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task SetActiveAsync(string id, bool active, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicTemplateWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        row.IsActive = active;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicTemplateWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        db.OeffentlicheVorlagen.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static readonly System.Linq.Expressions.Expression<Func<OeffentlicheVorlage, PublicTemplateRow>> Projection =
        v => new PublicTemplateRow(v.Id, v.Kind, v.Title, v.Text, v.IsActive, v.SortOrder);
}

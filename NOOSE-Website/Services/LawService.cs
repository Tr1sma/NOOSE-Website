using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IGesetzService" />
public class LawService(IDbContextFactory<AppDbContext> dbFactory) : ILawService
{
    public async Task<List<Law>> GetListAsync(CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = partnerAgency is { } agency ? db.Laws.OnlyPartnerVisible(db, agency, partnerAgentId) : db.Laws.AsQueryable();
        return await query
            .OrderBy(g => g.LawBook).ThenBy(g => g.Paragraph).ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<Law?> GetAsync(string id, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // partners: only released laws
        if (partnerAgency is { } agency && !await PartnerVisibility.IsRecordVisibleToPartnerAsync(db, nameof(Law), id, agency, partnerAgentId, cancellationToken))
        {
            return null;
        }
        return await db.Laws.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<List<Law>> SearchAsync(string? searchText, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Laws.AsQueryable();

        var s = searchText?.Trim();
        if (!string.IsNullOrWhiteSpace(s))
        {
            query = query.Where(g => g.Title.Contains(s) || g.Paragraph.Contains(s) || g.LawBook.Contains(s));
        }

        return await query
            .OrderBy(g => g.LawBook).ThenBy(g => g.Paragraph)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Law> CreateAsync(LawInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Validate(input);

        var law = new Law
        {
            LawBook = input.LawBook.Trim(),
            Paragraph = input.Paragraph.Trim(),
            Title = input.Title.Trim(),
            Text = input.Text.Trim(),
            Sentence = string.IsNullOrWhiteSpace(input.Sentence) ? null : input.Sentence.Trim(),
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Laws.Add(law);
        await db.SaveChangesAsync(cancellationToken);
        return law;
    }

    public async Task RefreshAsync(string id, LawInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Validate(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var law = await db.Laws.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Paragraf nicht gefunden.");

        law.LawBook = input.LawBook.Trim();
        law.Paragraph = input.Paragraph.Trim();
        law.Title = input.Title.Trim();
        law.Text = input.Text.Trim();
        law.Sentence = string.IsNullOrWhiteSpace(input.Sentence) ? null : input.Sentence.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var law = await db.Laws.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Paragraf nicht gefunden.");

        // Soft-Delete (Interceptor wandelt Remove um); bestehende Verknüpfungen zeigen den Eintrag
        // danach nicht mehr an (Soft-Delete-Filter der Verknüpfungs-Auflösung).
        db.Laws.Remove(law);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(LawInput input)
    {
        if (string.IsNullOrWhiteSpace(input.LawBook)
            || string.IsNullOrWhiteSpace(input.Paragraph)
            || string.IsNullOrWhiteSpace(input.Title)
            || string.IsNullOrWhiteSpace(input.Text))
        {
            throw new InvalidOperationException("Gesetzbuch, Paragraf, Titel und Text sind Pflichtfelder.");
        }
    }
}

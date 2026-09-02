using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IWarnhinweisService" />
public class WarnhinweisService(IDbContextFactory<AppDbContext> dbFactory) : IWarnhinweisService
{
    private const int MaxName = 60;

    public async Task<IReadOnlyList<Warnhinweis>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warnhinweise
            .AsNoTracking()
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WarnhinweisUsage>> GetWithUsageAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Warnhinweise
            .AsNoTracking()
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Name)
            .ToListAsync(cancellationToken);
        // only assignments on LIVING notices: the group-by touches no navigation, so nothing applied the
        // soft-delete filter and the delete confirmation quoted a count that included deleted ones
        var counts = await db.FahndungWarnhinweise
            .Where(z => db.OeffentlicheFahndungen.Any(f => f.Id == z.FahndungId))
            .GroupBy(z => z.WarnhinweisId)
            .Select(g => new { WarnhinweisId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var map = counts.ToDictionary(x => x.WarnhinweisId, x => x.Count, StringComparer.Ordinal);
        return rows.Select(w => new WarnhinweisUsage(w, map.GetValueOrDefault(w.Id))).ToList();
    }

    public async Task<IReadOnlyList<WarnhinweisOption>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warnhinweise
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Name)
            .Select(w => new WarnhinweisOption(w.Id, w.Name, w.Colour))
            .ToListAsync(cancellationToken);
    }

    public async Task<Warnhinweis> CreateAsync(string name, string? colour, int sortOrder, bool isActive,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // stricter than TagService, where any agent may create: this list is rendered anonymously
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        name = RequirePublishableLabel(name);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await RequireFreeNameAsync(db, name, null, cancellationToken);

        var row = new Warnhinweis
        {
            Name = name,
            Colour = WarnhinweisColours.Sanitise(colour),
            SortOrder = sortOrder,
            IsActive = isActive,
        };
        db.Warnhinweise.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    public async Task RefreshAsync(string id, string name, string? colour, int sortOrder, bool isActive,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        name = RequirePublishableLabel(name);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Warnhinweise.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Warnhinweis nicht gefunden.");
        await RequireFreeNameAsync(db, name, id, cancellationToken);

        row.Name = name;
        row.Colour = WarnhinweisColours.Sanitise(colour);
        row.SortOrder = sortOrder;
        row.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Warnhinweise.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (row is null)
        {
            return;
        }

        // cleared explicitly, not left to the FK cascade: this assignment decides what an anonymous page renders, so
        // it must not depend on the database engine's referential semantics. The cascade stays as the net.
        var assignments = await db.FahndungWarnhinweise
            .Where(z => z.WarnhinweisId == id)
            .ToListAsync(cancellationToken);
        db.FahndungWarnhinweise.RemoveRange(assignments);
        db.Warnhinweise.Remove(row);

        // one row per affected notice: the assignment table is not IAuditable, so deleting a label silently
        // changed what several notices render. Same shape SetHintsAsync uses.
        foreach (var wantedId in assignments.Select(z => z.FahndungId).Distinct(StringComparer.Ordinal))
        {
            db.AuditLogs.Add(ManualAudit.Row(nameof(OeffentlicheFahndung), wantedId, AuditAction.Modified, actor,
                ManualAudit.Change("Warnhinweis", row.Name, null)));
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Runs of whitespace folded to one space, so a typo is not mistaken for markup.</summary>
    private static string Collapse(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pending = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pending = builder.Length > 0;
                continue;
            }
            if (pending)
            {
                builder.Append(' ');
                pending = false;
            }
            builder.Append(c);
        }
        return builder.ToString();
    }

    /// <summary>
    /// The label reaches an anonymous page without ever passing the publication check, so it answers to the same
    /// three rules as an accusation: plain text, no mention, no placeholder token.
    /// </summary>
    private static string RequirePublishableLabel(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Der Warnhinweis braucht eine Bezeichnung.");
        }
        if (name.Length > MaxName)
        {
            throw new InvalidOperationException($"Die Bezeichnung ist auf {MaxName} Zeichen begrenzt.");
        }
        // compared on collapsed whitespace: PlainText also folds runs of spaces, so an accidental double space
        // was rejected as markup
        if (Collapse(HtmlCleanup.PlainText(name)) != Collapse(name))
        {
            throw new InvalidOperationException("Die Bezeichnung ist Klartext, kein Markup.");
        }
        if (MentionParser.Parse(name).Count > 0)
        {
            throw new InvalidOperationException("Die Bezeichnung enthält eine Erwähnung.");
        }
        if (name.Contains("{{", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Die Bezeichnung enthält einen Platzhalter.");
        }
        return name;
    }

    private static async Task RequireFreeNameAsync(AppDbContext db, string name, string? ownId,
        CancellationToken cancellationToken)
    {
        // spelled out rather than `w.Id != ownId`: with a null ownId that translates to SQL NULL and quietly matches
        // nothing, so a duplicate would slip through on create
        var taken = ownId is null
            ? await db.Warnhinweise.AnyAsync(w => w.Name == name, cancellationToken)
            : await db.Warnhinweise.AnyAsync(w => w.Id != ownId && w.Name == name, cancellationToken);
        if (taken)
        {
            throw new InvalidOperationException($"Ein Warnhinweis „{name}“ existiert bereits.");
        }
    }
}

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Kasse;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Kasse;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IKassenTemplateService" />
public class KassenTemplateService(IDbContextFactory<AppDbContext> dbFactory) : IKassenTemplateService
{
    public async Task<List<KassenBuchungVorlage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.KassenVorlagen
            .OrderBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KassenBuchungVorlage>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.KassenVorlagen
            .Where(v => v.IsActive)
            .OrderBy(v => v.Sorting).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<KassenBuchungVorlage?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.KassenVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<KassenBuchungVorlage> CreateAsync(KassenVorlageInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        RequireManage(actor);
        var name = Validate(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.KassenVorlagen.AnyAsync(v => v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }

        var template = new KassenBuchungVorlage { Name = name };
        Apply(template, input);
        db.KassenVorlagen.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task RefreshAsync(string id, KassenVorlageInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        RequireManage(actor);
        var name = Validate(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.KassenVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        if (await db.KassenVorlagen.AnyAsync(v => v.Id != id && v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }

        template.Name = name;
        Apply(template, input);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        RequireManage(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Hard delete on purpose: a soft-deleted ghost keeps its unique Name and would block reuse; templates are simple presets.
        var affected = await db.KassenVorlagen.Where(v => v.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (affected > 0)
        {
            // ExecuteDelete bypasses the audit interceptor
            db.AuditLogs.Add(ManualAudit.Row(nameof(KassenBuchungVorlage), id, AuditAction.Deleted, actor));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void RequireManage(ClaimsPrincipal actor)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);
    }

    /// <summary>Validates name and amount, rejecting corrections (a preset target balance makes no sense); returns the trimmed name.</summary>
    private static string Validate(KassenVorlageInput input)
    {
        var name = (input.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        if (input.Kind == KassenBuchungArt.Korrektur)
        {
            throw new InvalidOperationException("Korrekturen können nicht als Vorlage gespeichert werden.");
        }
        if (input.Amount <= 0)
        {
            throw new InvalidOperationException("Bitte einen Betrag größer 0 angeben.");
        }
        return name;
    }

    private static void Apply(KassenBuchungVorlage template, KassenVorlageInput input)
    {
        template.Account = input.Account;
        template.Kind = input.Kind;
        template.Amount = input.Amount;
        template.Reason = input.Reason.TrimToNull();
        template.IsActive = input.IsActive;
        template.Sorting = input.Sorting;
    }
}

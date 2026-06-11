using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IDokumentVorlageService" />
public class DokumentVorlageService(IDbContextFactory<AppDbContext> dbFactory) : IDokumentVorlageService
{
    public async Task<List<DokumentVorlage>> GetAlleAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DokumentVorlagen
            .OrderBy(v => v.Sortierung).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DokumentVorlage>> GetAktiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DokumentVorlagen
            .Where(v => v.IstAktiv)
            .OrderBy(v => v.Sortierung).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DokumentVorlage?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DokumentVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<DokumentVorlage> ErstellenAsync(DokumentVorlageEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        var name = (eingabe.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.DokumentVorlagen.AnyAsync(v => v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }

        var vorlage = new DokumentVorlage { Name = name };
        Uebernehmen(vorlage, eingabe);
        db.DokumentVorlagen.Add(vorlage);
        await db.SaveChangesAsync(cancellationToken);
        return vorlage;
    }

    public async Task AktualisierenAsync(string id, DokumentVorlageEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        var name = (eingabe.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorlage = await db.DokumentVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        if (await db.DokumentVorlagen.AnyAsync(v => v.Id != id && v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }

        vorlage.Name = name;
        Uebernehmen(vorlage, eingabe);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorlage = await db.DokumentVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vorlage is null)
        {
            return;
        }
        // Remove wird vom AuditSaveChangesInterceptor in einen Soft-Delete (Papierkorb) umgewandelt.
        db.DokumentVorlagen.Remove(vorlage);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Überträgt die editierbaren Felder (ohne Name – vorab validiert); bereinigt den HTML-Body.</summary>
    private static void Uebernehmen(DokumentVorlage vorlage, DokumentVorlageEingabe eingabe)
    {
        vorlage.Beschreibung = eingabe.Beschreibung.TrimToNull();
        vorlage.Kategorie = eingabe.Kategorie.TrimToNull();
        vorlage.InhaltHtml = HtmlBereinigung.Bereinige(eingabe.InhaltHtml);
        vorlage.IstAktiv = eingabe.IstAktiv;
        vorlage.Sortierung = eingabe.Sortierung;
    }
}

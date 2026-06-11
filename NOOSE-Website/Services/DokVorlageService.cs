using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IDokVorlageService" />
public class DokVorlageService(IDbContextFactory<AppDbContext> dbFactory) : IDokVorlageService
{
    public async Task<List<DokVorlage>> GetAlleAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DokVorlagen
            .OrderBy(v => v.Sortierung).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DokVorlage>> GetAktiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DokVorlagen
            .Where(v => v.IstAktiv)
            .OrderBy(v => v.Sortierung).ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DokVorlage> ErstellenAsync(DokVorlageEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        var name = (eingabe.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.DokVorlagen.AnyAsync(v => v.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Eine Vorlage „{name}“ existiert bereits.");
        }

        var vorlage = new DokVorlage { Name = name };
        Uebernehmen(vorlage, eingabe);
        db.DokVorlagen.Add(vorlage);
        await db.SaveChangesAsync(cancellationToken);
        return vorlage;
    }

    public async Task AktualisierenAsync(string id, DokVorlageEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        var name = (eingabe.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Vorlagen-Name darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorlage = await db.DokVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");
        if (await db.DokVorlagen.AnyAsync(v => v.Id != id && v.Name == name, cancellationToken))
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
        var vorlage = await db.DokVorlagen.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vorlage is null)
        {
            return;
        }
        // Remove wird vom AuditSaveChangesInterceptor in einen Soft-Delete (Papierkorb) umgewandelt.
        db.DokVorlagen.Remove(vorlage);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Überträgt die editierbaren Felder der Eingabe auf die Vorlage (ohne Name – der wird vorab validiert).</summary>
    private static void Uebernehmen(DokVorlage vorlage, DokVorlageEingabe eingabe)
    {
        vorlage.Beschreibung = eingabe.Beschreibung.TrimToNull();
        vorlage.IstAktiv = eingabe.IstAktiv;
        vorlage.Sortierung = eingabe.Sortierung;
        vorlage.StandardGrund = eingabe.StandardGrund.TrimToNull();
        vorlage.StandardFraktion = eingabe.StandardFraktion.TrimToNull();
        vorlage.StandardErhalteneInformationen = eingabe.StandardErhalteneInformationen.TrimToNull();
        vorlage.StandardWahrheitsserum = eingabe.StandardWahrheitsserum;
        vorlage.StandardAusgang = eingabe.StandardAusgang;
    }
}

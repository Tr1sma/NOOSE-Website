using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ICustomFeldDefinitionService" />
public class CustomFeldDefinitionService(IDbContextFactory<AppDbContext> dbFactory) : ICustomFeldDefinitionService
{
    public async Task<List<CustomFeldDefinition>> GetAlleAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CustomFeldDefinitionen
            .OrderBy(d => d.EntitaetTyp).ThenBy(d => d.Reihenfolge).ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CustomFeldDefinition>> GetFuerTypAsync(string entitaetTyp, bool nurAktive, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.CustomFeldDefinitionen.Where(d => d.EntitaetTyp == entitaetTyp);
        if (nurAktive)
        {
            query = query.Where(d => d.IstAktiv);
        }
        return await query
            .OrderBy(d => d.Reihenfolge).ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomFeldDefinition> ErstellenAsync(CustomFeldDefinitionEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeAdmin(handelnder);

        var (name, entitaetTyp) = Validieren(eingabe);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.CustomFeldDefinitionen.AnyAsync(d => d.EntitaetTyp == entitaetTyp && d.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Für diesen Aktentyp existiert bereits ein Feld „{name}“.");
        }

        var definition = new CustomFeldDefinition { Name = name, EntitaetTyp = entitaetTyp };
        Uebernehmen(definition, eingabe);
        db.CustomFeldDefinitionen.Add(definition);
        await db.SaveChangesAsync(cancellationToken);
        return definition;
    }

    public async Task AktualisierenAsync(string id, CustomFeldDefinitionEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeAdmin(handelnder);

        var (name, entitaetTyp) = Validieren(eingabe);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var definition = await db.CustomFeldDefinitionen.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Feld-Definition nicht gefunden.");
        if (await db.CustomFeldDefinitionen.AnyAsync(d => d.Id != id && d.EntitaetTyp == entitaetTyp && d.Name == name, cancellationToken))
        {
            throw new InvalidOperationException($"Für diesen Aktentyp existiert bereits ein Feld „{name}“.");
        }

        definition.Name = name;
        definition.EntitaetTyp = entitaetTyp;
        Uebernehmen(definition, eingabe);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeAdmin(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var definition = await db.CustomFeldDefinitionen.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (definition is null)
        {
            return;
        }
        // Remove wird vom AuditSaveChangesInterceptor in einen Soft-Delete (Papierkorb) umgewandelt.
        // Bereits erfasste Werte bleiben bestehen, werden aber nicht mehr angezeigt (Filter auf aktive Felder).
        db.CustomFeldDefinitionen.Remove(definition);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static (string Name, string EntitaetTyp) Validieren(CustomFeldDefinitionEingabe eingabe)
    {
        var name = (eingabe.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Feld-Name darf nicht leer sein.");
        }
        var entitaetTyp = (eingabe.EntitaetTyp ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(entitaetTyp))
        {
            throw new InvalidOperationException("Es muss ein Aktentyp gewählt werden.");
        }
        if (eingabe.FeldTyp == CustomFeldTyp.Auswahl && string.IsNullOrWhiteSpace(eingabe.Optionen))
        {
            throw new InvalidOperationException("Für ein Auswahl-Feld muss mindestens eine Option angegeben werden.");
        }
        return (name, entitaetTyp);
    }

    /// <summary>Überträgt die editierbaren Felder (ohne Name/EntitaetTyp – die werden vorab validiert/gesetzt).</summary>
    private static void Uebernehmen(CustomFeldDefinition definition, CustomFeldDefinitionEingabe eingabe)
    {
        definition.FeldTyp = eingabe.FeldTyp;
        definition.Optionen = eingabe.FeldTyp == CustomFeldTyp.Auswahl ? eingabe.Optionen.TrimToNull() : null;
        definition.Pflicht = eingabe.Pflicht;
        definition.Reihenfolge = eingabe.Reihenfolge;
        definition.IstAktiv = eingabe.IstAktiv;
    }
}

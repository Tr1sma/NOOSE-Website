using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IGesetzService" />
public class GesetzService(IDbContextFactory<AppDbContext> dbFactory) : IGesetzService
{
    public async Task<List<Gesetz>> GetListeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Gesetze
            .OrderBy(g => g.Gesetzbuch).ThenBy(g => g.Paragraf).ThenBy(g => g.Titel)
            .ToListAsync(cancellationToken);
    }

    public async Task<Gesetz?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Gesetze.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<List<Gesetz>> SucheAsync(string? suchtext, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Gesetze.AsQueryable();

        var s = suchtext?.Trim();
        if (!string.IsNullOrWhiteSpace(s))
        {
            query = query.Where(g => g.Titel.Contains(s) || g.Paragraf.Contains(s) || g.Gesetzbuch.Contains(s));
        }

        return await query
            .OrderBy(g => g.Gesetzbuch).ThenBy(g => g.Paragraf)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Gesetz> ErstellenAsync(GesetzEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);
        Validieren(eingabe);

        var gesetz = new Gesetz
        {
            Gesetzbuch = eingabe.Gesetzbuch.Trim(),
            Paragraf = eingabe.Paragraf.Trim(),
            Titel = eingabe.Titel.Trim(),
            Text = eingabe.Text.Trim(),
            Strafmass = string.IsNullOrWhiteSpace(eingabe.Strafmass) ? null : eingabe.Strafmass.Trim(),
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Gesetze.Add(gesetz);
        await db.SaveChangesAsync(cancellationToken);
        return gesetz;
    }

    public async Task AktualisierenAsync(string id, GesetzEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);
        Validieren(eingabe);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var gesetz = await db.Gesetze.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Paragraf nicht gefunden.");

        gesetz.Gesetzbuch = eingabe.Gesetzbuch.Trim();
        gesetz.Paragraf = eingabe.Paragraf.Trim();
        gesetz.Titel = eingabe.Titel.Trim();
        gesetz.Text = eingabe.Text.Trim();
        gesetz.Strafmass = string.IsNullOrWhiteSpace(eingabe.Strafmass) ? null : eingabe.Strafmass.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var gesetz = await db.Gesetze.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Paragraf nicht gefunden.");

        // Soft-Delete (Interceptor wandelt Remove um); bestehende Verknüpfungen zeigen den Eintrag
        // danach nicht mehr an (Soft-Delete-Filter der Verknüpfungs-Auflösung).
        db.Gesetze.Remove(gesetz);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validieren(GesetzEingabe eingabe)
    {
        if (string.IsNullOrWhiteSpace(eingabe.Gesetzbuch)
            || string.IsNullOrWhiteSpace(eingabe.Paragraf)
            || string.IsNullOrWhiteSpace(eingabe.Titel)
            || string.IsNullOrWhiteSpace(eingabe.Text))
        {
            throw new InvalidOperationException("Gesetzbuch, Paragraf, Titel und Text sind Pflichtfelder.");
        }
    }
}

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IKommentarService" />
public class KommentarService(AppDbContext db) : IKommentarService
{
    public async Task<List<Kommentar>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        if (!await AkteSichtbarAsync(entitaetTyp, entitaetId, istFuehrung, cancellationToken))
        {
            return new();
        }

        return await db.Kommentare
            .Where(k => k.EntitaetTyp == entitaetTyp && k.EntitaetId == entitaetId)
            .OrderByDescending(k => k.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<Kommentar> ErstellenAsync(string entitaetTyp, string entitaetId, string text, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        text = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Der Kommentar darf nicht leer sein.");
        }

        var kommentar = new Kommentar
        {
            EntitaetTyp = entitaetTyp,
            EntitaetId = entitaetId,
            Text = text,
            AutorName = handelnder.GetCodename(),
        };
        db.Kommentare.Add(kommentar);
        await db.SaveChangesAsync(cancellationToken);
        return kommentar;
    }

    public async Task LoeschenAsync(string kommentarId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var kommentar = await db.Kommentare.FirstOrDefaultAsync(k => k.Id == kommentarId, cancellationToken);
        if (kommentar is null)
        {
            return;
        }
        db.Kommentare.Remove(kommentar); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Vgl. <c>QuelleService</c>: Eltern-Sichtbarkeit ohne FK-Navigation prüfen (nur Person in Phase 3).</summary>
    private async Task<bool> AkteSichtbarAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken)
    {
        if (entitaetTyp != nameof(Person))
        {
            return true;
        }
        var person = await db.Personen
            .Where(p => p.Id == entitaetId)
            .Select(p => new { p.IstVerschlusssache })
            .FirstOrDefaultAsync(cancellationToken);
        return person is not null && (istFuehrung || !person.IstVerschlusssache);
    }
}

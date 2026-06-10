using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IKommentarService" />
public class KommentarService(IDbContextFactory<AppDbContext> dbFactory) : IKommentarService
{
    public async Task<List<Kommentar>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, entitaetTyp, entitaetId, istFuehrung, cancellationToken))
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

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
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
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var kommentar = await db.Kommentare.FirstOrDefaultAsync(k => k.Id == kommentarId, cancellationToken);
        if (kommentar is null)
        {
            return;
        }
        // Löschen darf der Verfasser selbst oder die Führung – serverseitig erzwingen, nicht nur in der UI.
        if (!handelnder.IstFuehrung() && kommentar.ErstelltVonId != handelnder.GetAgentId())
        {
            throw new UnauthorizedAccessException("Diesen Kommentar darf nur der Verfasser oder die Führung löschen.");
        }
        db.Kommentare.Remove(kommentar); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
    }
}

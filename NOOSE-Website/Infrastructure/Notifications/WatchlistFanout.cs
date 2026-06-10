using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Notifications;

/// <summary>
/// Versendet je betroffener Akte eine „Beobachtete Akte geändert"-Benachrichtigung an deren aktive Folger – ohne
/// den Auslöser selbst, dedupliziert (Coalescing) und aus EMPFÄNGER-Sicht Verschlusssache-/Personalakte-geprüft
/// (kein Akten-/VS-Leck). Eigener, kurzlebiger Factory-Context (läuft im Hintergrund-Scope des
/// <see cref="WatchlistDispatcher"/>). Scoped.
/// </summary>
public sealed class WatchlistFanout(IDbContextFactory<AppDbContext> dbFactory, INotificationService notifications)
{
    public async Task VerarbeiteAsync(string? akteurId, IReadOnlyCollection<(string Typ, string Id)> akten,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Anzeigename (öffentlich, NIE Klarname) + Href je Akte in einer Sammelabfrage. Akten im Papierkorb bzw.
        // unbekannte Verweise lösen sich NICHT auf → werden übersprungen (bewusst keine „gelöscht"-Meldung).
        var aufgeloest = await AktenReferenz.AufloesenAsync(db, akten, cancellationToken);

        foreach (var (typ, id) in akten.Distinct())
        {
            if (!aufgeloest.TryGetValue((typ, id), out var aufl))
            {
                continue;
            }

            var folgerIds = await db.Watchlisten
                .Where(w => w.EntitaetTyp == typ && w.EntitaetId == id)
                .Select(w => w.AgentId).Distinct().ToListAsync(cancellationToken);
            if (akteurId is not null)
            {
                folgerIds.Remove(akteurId);
            }
            if (folgerIds.Count == 0)
            {
                continue;
            }

            // Nur aktive Folger (Gesperrte/Ausstehende sehen ohnehin nichts).
            var folger = await db.Users
                .Where(u => folgerIds.Contains(u.Id) && u.Status == AgentStatus.Aktiv)
                .Select(u => new { u.Id, u.IstAdmin, u.Dienstgrad })
                .ToListAsync(cancellationToken);
            if (folger.Count == 0)
            {
                continue;
            }

            // Coalescing: wer zu dieser Akte schon eine UNGELESENE Meldung hat, bekommt keine zweite.
            var schonOffen = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(aufl.Href))
            {
                schonOffen = (await db.Benachrichtigungen
                    .Where(n => n.Typ == NotificationTyp.AkteGeaendert && n.Href == aufl.Href
                                && n.GelesenAm == null && folgerIds.Contains(n.EmpfaengerId))
                    .Select(n => n.EmpfaengerId)
                    .ToListAsync(cancellationToken)).ToHashSet();
            }

            var titel = $"„{aufl.Anzeige}“ wurde aktualisiert.";
            foreach (var f in folger)
            {
                if (schonOffen.Contains(f.Id))
                {
                    continue;
                }
                // Verschlusssache-/Personalakte-Schutz aus Sicht des EMPFÄNGERS (kein Leck an Nicht-Berechtigte).
                var istFuehrung = f.IstAdmin || f.Dienstgrad is >= Dienstgrad.SupervisorySpecialAgent;
                if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, typ, id, istFuehrung, cancellationToken))
                {
                    continue;
                }
                await notifications.BenachrichtigeAsync(f.Id, NotificationTyp.AkteGeaendert, titel, aufl.Href, cancellationToken);
            }
        }
    }
}

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
    public async Task ProcessAsync(string? actorId, IReadOnlyCollection<(string Type, string Id)> records,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Anzeigename (öffentlich, NIE Klarname) + Href je Akte in einer Sammelabfrage. Akten im Papierkorb bzw.
        // unbekannte Verweise lösen sich NICHT auf → werden übersprungen (bewusst keine „gelöscht"-Meldung).
        // Hier (Hintergrund, viele Empfänger) ALLE Taskforce-Namen auflösen (Standard); die Mitgliedschaftsprüfung
        // erfolgt pro Folger unten über IstAkteSichtbarAsync mit dessen Id.
        var resolved = await RecordsReference.ResolveAsync(db, records, cancellationToken);

        foreach (var (type, id) in records.Distinct())
        {
            if (!resolved.TryGetValue((type, id), out var aufl))
            {
                continue;
            }

            var followerIds = await db.Watchlists
                .Where(w => w.EntityType == type && w.EntityId == id)
                .Select(w => w.AgentId).Distinct().ToListAsync(cancellationToken);
            if (actorId is not null)
            {
                followerIds.Remove(actorId);
            }
            if (followerIds.Count == 0)
            {
                continue;
            }

            // Nur aktive Folger (Gesperrte/Ausstehende sehen ohnehin nichts).
            var follower = await db.Users
                .Where(u => followerIds.Contains(u.Id) && u.Status == AgentStatus.Active)
                .Select(u => new { u.Id, u.IsAdmin, u.Rank })
                .ToListAsync(cancellationToken);
            if (follower.Count == 0)
            {
                continue;
            }

            // Coalescing: wer zu dieser Akte schon eine UNGELESENE Meldung hat, bekommt keine zweite.
            var alreadyOpen = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(aufl.Href))
            {
                alreadyOpen = (await db.Notifications
                    .Where(n => n.Type == NotificationType.RecordModified && n.Href == aufl.Href
                                && n.ReadAt == null && followerIds.Contains(n.RecipientId))
                    .Select(n => n.RecipientId)
                    .ToListAsync(cancellationToken)).ToHashSet();
            }

            var title = $"„{aufl.Display}“ wurde aktualisiert.";
            foreach (var f in follower)
            {
                if (alreadyOpen.Contains(f.Id))
                {
                    continue;
                }
                // Verschlusssache-/Personalakte-/Taskforce-Schutz aus Sicht des EMPFÄNGERS (kein Leck an
                // Nicht-Berechtigte). Bei Taskforces zählt die Mitgliedschaft → die Folger-Id (f.Id) mitgeben.
                var isLeadership = f.IsAdmin || f.Rank is >= Rank.SupervisorySpecialAgent;
                if (!await Visibility.IsRecordVisibleAsync(db, type, id, isLeadership, cancellationToken, f.Id))
                {
                    continue;
                }
                await notifications.NotifyAsync(f.Id, NotificationType.RecordModified, title, aufl.Href, cancellationToken);
            }
        }
    }
}

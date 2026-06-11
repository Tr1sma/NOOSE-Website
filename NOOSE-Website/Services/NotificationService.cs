using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Benachrichtigungen;
using NOOSE_Website.Infrastructure.Notifications;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="INotificationService" />
public class NotificationService(IDbContextFactory<AppDbContext> dbFactory, NotificationBroadcaster broadcaster)
    : INotificationService
{
    public async Task BenachrichtigeAsync(string? empfaengerId, NotificationTyp typ, string titel, string? href,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(empfaengerId))
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Benachrichtigungen.Add(new Benachrichtigung
        {
            EmpfaengerId = empfaengerId,
            Typ = typ,
            Titel = titel,
            Href = href,
        });
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Melde(empfaengerId);
    }

    public async Task BenachrichtigeErwaehnteAsync(string? text, string titel, string? href, string zielTyp, string zielId,
        ClaimsPrincipal ausloeser, CancellationToken cancellationToken = default)
    {
        // Nur Agenten-Erwähnungen, nicht den Auslöser selbst, jede Id nur einmal.
        var ausloeserId = ausloeser.GetAgentId();
        var agentIds = MentionParser.Parse(text)
            .Where(t => t.Typ == nameof(Agent) && t.Id != ausloeserId)
            .Select(t => t.Id)
            .Distinct()
            .ToList();
        if (agentIds.Count == 0)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Nur aktive Empfänger laden (Gesperrte/Ausstehende sehen ohnehin nichts).
        var empfaenger = await db.Users
            .Where(u => agentIds.Contains(u.Id) && u.Status == AgentStatus.Aktiv)
            .Select(u => new { u.Id, u.IstAdmin, u.Dienstgrad })
            .ToListAsync(cancellationToken);

        var benachrichtigt = new List<string>();
        foreach (var e in empfaenger)
        {
            // Empfänger-Perspektive: Verschlusssache-/Papierkorb-Schutz aus SICHT DES EMPFÄNGERS prüfen –
            // wer die Ziel-Akte nicht sehen darf, wird auch nicht benachrichtigt (kein Akten-/VS-Leck).
            var empfaengerIstFuehrung = e.IstAdmin || e.Dienstgrad is >= Dienstgrad.SupervisorySpecialAgent;
            if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, zielTyp, zielId, empfaengerIstFuehrung, cancellationToken))
            {
                continue;
            }
            db.Benachrichtigungen.Add(new Benachrichtigung
            {
                EmpfaengerId = e.Id,
                Typ = NotificationTyp.Erwaehnung,
                Titel = titel,
                Href = href,
            });
            benachrichtigt.Add(e.Id);
        }

        if (benachrichtigt.Count == 0)
        {
            return;
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var id in benachrichtigt)
        {
            broadcaster.Melde(id);
        }
    }

    public async Task BenachrichtigeVieleAsync(IReadOnlyCollection<string> empfaengerIds, NotificationTyp typ,
        string titel, string? href, string? ausloeserId, CancellationToken cancellationToken = default)
    {
        // Auslöser ausschließen, jede Empfänger-Id nur einmal, Leeres verwerfen.
        var ziele = empfaengerIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && id != ausloeserId)
            .Distinct()
            .ToList();
        if (ziele.Count == 0)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        foreach (var id in ziele)
        {
            db.Benachrichtigungen.Add(new Benachrichtigung
            {
                EmpfaengerId = id,
                Typ = typ,
                Titel = titel,
                Href = href,
            });
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var id in ziele)
        {
            broadcaster.Melde(id);
        }
    }

    public async Task<List<Benachrichtigung>> GetEigeneAsync(ClaimsPrincipal handelnder, int max = 20, CancellationToken cancellationToken = default)
    {
        // Empfänger ist IMMER der Aufrufer selbst – die Id aus dem Principal ableiten, nie als Parameter
        // entgegennehmen (serverseitig erzwingen, nicht UI-abhängig; analog AlsGelesenMarkierenAsync).
        var agentId = handelnder.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return new();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Benachrichtigungen
            .Where(n => n.EmpfaengerId == agentId)
            .OrderByDescending(n => n.ErstelltAm)
            .Take(Math.Clamp(max, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUngeleseneAnzahlAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var agentId = handelnder.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return 0;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Benachrichtigungen
            .CountAsync(n => n.EmpfaengerId == agentId && n.GelesenAm == null, cancellationToken);
    }

    public async Task AlsGelesenMarkierenAsync(string benachrichtigungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var agentId = handelnder.GetAgentId();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var n = await db.Benachrichtigungen.FirstOrDefaultAsync(x => x.Id == benachrichtigungId, cancellationToken);
        // Nur die eigene Benachrichtigung darf als gelesen markiert werden.
        if (n is null || n.EmpfaengerId != agentId || n.GelesenAm is not null)
        {
            return;
        }
        n.GelesenAm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Melde(n.EmpfaengerId);
    }

    public async Task AlleAlsGelesenAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var agentId = handelnder.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var offen = await db.Benachrichtigungen
            .Where(n => n.EmpfaengerId == agentId && n.GelesenAm == null)
            .ToListAsync(cancellationToken);
        if (offen.Count == 0)
        {
            return;
        }
        var jetzt = DateTime.UtcNow;
        foreach (var n in offen)
        {
            n.GelesenAm = jetzt;
        }
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Melde(agentId);
    }
}

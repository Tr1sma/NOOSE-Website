using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Infrastructure.Chat;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITaskforceChatService" />
public class TaskforceChatService(IDbContextFactory<AppDbContext> dbFactory, TaskforceChatBroadcaster broadcaster) : ITaskforceChatService
{
    public async Task<List<TaskforceNachricht>> GetNachrichtenAsync(string taskforceId, bool istFuehrung, int limit = 100, DateTime? aelterAls = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Taskforce), taskforceId, istFuehrung, cancellationToken))
        {
            return new();
        }

        var query = db.TaskforceNachrichten.Where(n => n.TaskforceId == taskforceId);
        if (aelterAls is not null)
        {
            query = query.Where(n => n.ErstelltAm < aelterAls.Value);
        }
        // Jüngste `limit` laden, dann für die Anzeige chronologisch aufsteigend kehren.
        var juengste = await query
            .OrderByDescending(n => n.ErstelltAm)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        juengste.Reverse();
        return juengste;
    }

    public async Task<TaskforceNachricht> SendenAsync(string taskforceId, string text, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        var inhalt = text?.Trim();
        if (string.IsNullOrEmpty(inhalt))
        {
            throw new InvalidOperationException("Die Nachricht darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Schreiben darf nur, wer die Taskforce sehen darf (Verschlusssache → nur Führung).
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Taskforce), taskforceId, handelnder.IstFuehrung(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Taskforce ist für dich nicht zugänglich.");
        }

        var nachricht = new TaskforceNachricht
        {
            TaskforceId = taskforceId,
            Text = inhalt,
            AutorName = handelnder.GetCodename(),
        };
        db.TaskforceNachrichten.Add(nachricht);
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Melde(taskforceId);
        return nachricht;
    }

    public async Task LoeschenAsync(string nachrichtId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var nachricht = await db.TaskforceNachrichten.FirstOrDefaultAsync(n => n.Id == nachrichtId, cancellationToken);
        if (nachricht is null)
        {
            return;
        }
        // Nur der Autor oder die Führung darf zurückziehen.
        if (!handelnder.IstFuehrung() && nachricht.ErstelltVonId != handelnder.GetAgentId())
        {
            throw new UnauthorizedAccessException("Nur der Autor oder die Führung kann eine Nachricht zurückziehen.");
        }
        db.TaskforceNachrichten.Remove(nachricht); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Melde(nachricht.TaskforceId);
    }
}

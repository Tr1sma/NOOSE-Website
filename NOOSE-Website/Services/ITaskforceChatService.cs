using System.Security.Claims;
using NOOSE_Website.Data.Entities.Taskforces;

namespace NOOSE_Website.Services;

/// <summary>
/// Team-Chat einer Taskforce (Phase 5d). Sichtbarkeit/Schreibrecht erben von der Eltern-Taskforce: wer die
/// Taskforce sehen darf (Verschlusssache → nur Führung), darf mitlesen und schreiben. Löschen nur durch den Autor
/// oder die Führung. Nach Senden/Löschen wird ein Live-Signal über den Broadcaster ausgelöst.
/// </summary>
public interface ITaskforceChatService
{
    /// <summary>Lädt die jüngsten Nachrichten (chronologisch aufsteigend für die Anzeige). <paramref name="aelterAls"/>
    /// blättert ältere nach. Leere Liste, wenn die Taskforce für den Aufrufer nicht sichtbar ist.</summary>
    Task<List<TaskforceMessage>> GetMessagesAsync(string taskforceId, bool isLeadership, int limit = 100, DateTime? olderAs = null, CancellationToken cancellationToken = default);

    /// <summary>Sendet eine Nachricht (Autor = Handelnder). Wirft, wenn die Taskforce nicht sichtbar oder der Text leer ist.</summary>
    Task<TaskforceMessage> SendAsync(string taskforceId, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Zieht eine Nachricht zurück (Soft-Delete). Erlaubt für den Autor oder die Führung.</summary>
    Task DeleteAsync(string messageId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

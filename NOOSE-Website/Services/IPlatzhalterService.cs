using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>
/// Ersetzt Platzhalter-Tokens (z. B. <c>{{Name}}</c>) in einem Vorlagen-HTML durch die konkreten Werte aus
/// dem Akten-/Nutzer-Kontext. Wird beim Übernehmen einer Dokument-Vorlage in den Editor angewandt; das
/// Ergebnis bleibt frei editierbar und wird beim Speichern erneut bereinigt.
/// </summary>
public interface IPlatzhalterService
{
    /// <summary>
    /// Ersetzt bekannte Platzhalter im HTML. <paramref name="entitaetTyp"/>/<paramref name="entitaetId"/>
    /// liefern den Akten-Kontext (optional – ohne Kontext bleiben akten-bezogene Platzhalter leer).
    /// Unbekannte Tokens bleiben unverändert.
    /// </summary>
    Task<string> AnwendenAsync(string html, string? entitaetTyp, string? entitaetId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Liste der unterstützten Platzhalter (Token + Erläuterung) für die Hilfe im Vorlagen-Editor.</summary>
    IReadOnlyList<(string Token, string Beschreibung)> VerfuegbarePlatzhalter { get; }
}

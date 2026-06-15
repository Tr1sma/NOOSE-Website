using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Serverseitige Durchsetzung der Rechte-Matrix (Plan.md §6) in der Service-Schicht. <c>AuthorizeView</c>
/// in Razor versteckt nur die UI – die schreibenden Dienste müssen Rang/Eigentum selbst prüfen. Diese
/// Guards werfen <see cref="UnauthorizedAccessException"/> mit einer für den Nutzer verständlichen
/// Meldung (die UI fängt Service-Ausnahmen ab und zeigt sie als Snackbar).
/// </summary>
public static class Permission
{
    /// <summary>Wirft, wenn der Handelnde nicht der Führung (Supervisory Special Agent+) oder Admin angehört.</summary>
    public static void RequireLeadership(ClaimsPrincipal actor)
    {
        if (!actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Diese Aktion ist der Führung (ab Supervisory Special Agent) oder Admins vorbehalten.");
        }
    }

    /// <summary>
    /// Wirft, wenn der Handelnde keine Schreibrechte hat (Nur-Lese-Aufsicht = TeamLeitung ohne Admin).
    /// Ergänzt die zentrale EF-Schreibsperre (<c>ReadOnlyBarrierInterceptor</c>) dort, wo Schreibpfade den
    /// SaveChanges-Interceptor umgehen (Roh-SQL/Bulk wie <c>ExecuteUpdate</c>) oder wo ein hochrangiger
    /// Nur-Leser sonst einen rang-basierten Guard (<see cref="VerlangeFuehrung"/>) bestehen würde.
    /// </summary>
    public static void RequireWriteAccess(ClaimsPrincipal actor)
    {
        if (actor.IsOnlyReader())
        {
            throw new UnauthorizedAccessException(
                "Nur-Lese-Modus: Änderungen sind in der Aufsichtsrolle nicht möglich.");
        }
    }

    /// <summary>Wirft, wenn der Handelnde kein Admin ist (technische Systemverwaltung: Custom-Felder,
    /// Theming, Wartungsmodus – Plan.md §6).</summary>
    public static void RequireAdmin(ClaimsPrincipal actor)
    {
        if (!actor.IsAdmin())
        {
            throw new UnauthorizedAccessException(
                "Diese Aktion ist Admins vorbehalten.");
        }
    }

    /// <summary>Wirft, wenn der Handelnde die gewünschte Dokument-Verschluss-Stufe nicht vergeben darf.
    /// Führung darf jede Stufe setzen; TRU-/HRB-Angehörige nur die eigene; „Keine" ist für jeden
    /// Schreibberechtigten erlaubt (die Schreibsperre greift separat über <see cref="RequireWriteAccess"/>).</summary>
    public static void RequireMayAssignClassification(ClaimsPrincipal actor, DocumentClassification classification)
    {
        if (classification == DocumentClassification.None)
        {
            return;
        }
        if (!DocumentViewerScope.From(actor).CanSee(classification))
        {
            throw new UnauthorizedAccessException(
                $"Du darfst die Stufe „{DocumentClassificationDisplay.Label(classification)}“ nicht vergeben.");
        }
    }

    /// <summary>Wirft, wenn der Handelnde Beförderungen nicht entscheiden darf (Deputy Director+ oder Admin).</summary>
    public static void RequirePromotionDecide(ClaimsPrincipal actor)
    {
        if (!actor.MayPromotionDecide())
        {
            throw new UnauthorizedAccessException(
                "Über Beförderungen entscheidet nur Deputy Director aufwärts oder ein Admin.");
        }
    }

    /// <summary>Wirft, wenn der Handelnde die höchste Einstufung nicht setzen/entscheiden darf
    /// (Senior Special Agent+ oder Admin) – u. a. die Entscheidung über Hochstufungs-Anträge.</summary>
    public static void RequireHighestClassification(ClaimsPrincipal actor)
    {
        if (!actor.MayHighestClassification())
        {
            throw new UnauthorizedAccessException(
                "Über Hochstufungen auf „Gesichert staatsgefährdend“ entscheidet nur Senior Special Agent aufwärts oder ein Admin.");
        }
    }
}

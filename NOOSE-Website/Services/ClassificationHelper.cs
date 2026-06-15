using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Gemeinsame Einstufungs-Logik für alle Akten (Person/Fraktion/Personengruppe): das Rang-Gate für
/// „Gesichert staatsgefährdend" und die Erzeugung eines polymorphen Verlauf-Eintrags.
/// </summary>
public static class ClassificationHelper
{
    /// <summary>Wirft, wenn „Gesichert staatsgefährdend" ohne Senior Special Agent/Admin gesetzt würde.</summary>
    public static void CheckRankGate(Classification @new, ClaimsPrincipal actor)
    {
        if (@new == Classification.SecuredStateThreatening && !actor.MayHighestClassification())
        {
            throw new InvalidOperationException(
                "'Gesichert staatsgefährdend' darf erst ab Senior Special Agent direkt gesetzt werden – sonst per Antrag (Phase 5).");
        }
    }

    /// <summary>Baut einen (append-only) Verlauf-Eintrag für die Akte <paramref name="entitaetTyp"/>/<paramref name="entitaetId"/>.</summary>
    public static ClassificationHistory Entry(string entityType, string entityId, Classification value, string? justification, ClaimsPrincipal actor)
        => new()
        {
            EntityType = entityType,
            EntityId = entityId,
            Value = value,
            Justification = string.IsNullOrWhiteSpace(justification) ? null : justification.Trim(),
            Timestamp = DateTime.UtcNow,
            AgentId = actor.GetAgentId(),
            AgentName = actor.GetCodename(),
        };
}

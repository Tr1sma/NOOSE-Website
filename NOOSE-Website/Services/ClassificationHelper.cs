using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Shared classification logic.</summary>
public static class ClassificationHelper
{
    /// <summary>Enforce rank gate.</summary>
    public static void CheckRankGate(Classification @new, ClaimsPrincipal actor)
    {
        if (@new == Classification.SecuredStateThreatening && !actor.MayHighestClassification())
        {
            throw new InvalidOperationException(
                "'Gesichert staatsgefährdend' darf erst ab Senior Special Agent direkt gesetzt werden – sonst per Antrag (Phase 5).");
        }
    }

    /// <summary>Build history entry.</summary>
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

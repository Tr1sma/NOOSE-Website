using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Server-side permission guards.</summary>
public static class Permission
{
    /// <summary>Require leadership or admin.</summary>
    public static void RequireLeadership(ClaimsPrincipal actor)
    {
        if (!actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Diese Aktion ist der Führung (ab Supervisory Special Agent) oder Admins vorbehalten.");
        }
    }

    /// <summary>Require write access; denies read-only supervisors and partners.</summary>
    public static void RequireWriteAccess(ClaimsPrincipal actor)
    {
        if (actor.IsOnlyReader() || actor.IsPartner())
        {
            throw new UnauthorizedAccessException(
                "Nur-Lese-Modus: Änderungen sind in dieser Rolle nicht möglich.");
        }
    }

    /// <summary>Require admin.</summary>
    public static void RequireAdmin(ClaimsPrincipal actor)
    {
        if (!actor.IsAdmin())
        {
            throw new UnauthorizedAccessException(
                "Diese Aktion ist Admins vorbehalten.");
        }
    }

    /// <summary>Require classification permission.</summary>
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

    /// <summary>Require promotion authority.</summary>
    public static void RequirePromotionDecide(ClaimsPrincipal actor)
    {
        if (!actor.MayPromotionDecide())
        {
            throw new UnauthorizedAccessException(
                "Über Beförderungen entscheidet nur Deputy Director aufwärts oder ein Admin.");
        }
    }

    /// <summary>Require highest classification right.</summary>
    public static void RequireHighestClassification(ClaimsPrincipal actor)
    {
        if (!actor.MayHighestClassification())
        {
            throw new UnauthorizedAccessException(
                "Über Hochstufungen auf „Gesichert staatsgefährdend“ entscheidet nur Senior Special Agent aufwärts oder ein Admin.");
        }
    }
}

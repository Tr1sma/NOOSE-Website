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

    /// <summary>Require leadership or admin, but NOT read-only supervisors (OnlyReader).</summary>
    public static void RequireLeadershipNoReader(ClaimsPrincipal actor)
    {
        if (!actor.IsLeadership() || actor.IsOnlyReader())
        {
            throw new UnauthorizedAccessException(
                "Diese Auswertung ist der Führung vorbehalten und für die Nur-Lese-Aufsicht gesperrt.");
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

    /// <summary>Require leadership or the assigned informant handler; denies read-only supervisors and partners.</summary>
    public static void RequireInformantWrite(ClaimsPrincipal actor, string? handlerId)
    {
        if (actor.IsOnlyReader() || actor.IsPartner())
        {
            throw new UnauthorizedAccessException("Nur-Lese-Modus: Änderungen sind in dieser Rolle nicht möglich.");
        }
        var me = actor.GetAgentId();
        if (actor.IsLeadership() || (handlerId is not null && me is not null && me == handlerId))
        {
            return;
        }
        throw new UnauthorizedAccessException("Nur die Führung oder der zuständige Führungsagent darf diesen Informanten bearbeiten.");
    }

    /// <summary>Require the actor may use the external AI assistant (never partners or demo sessions).</summary>
    public static void RequireLlmUse(ClaimsPrincipal actor)
    {
        if (actor.IsPartner() || actor.IsDemo())
        {
            throw new UnauthorizedAccessException("Der KI-Assistent steht in dieser Rolle nicht zur Verfügung.");
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

    /// <summary>Require a configured bootstrap admin (demo mode / demo data).</summary>
    public static void RequireBootstrapAdmin(ClaimsPrincipal actor)
    {
        if (!actor.IsBootstrapAdmin())
        {
            throw new UnauthorizedAccessException(
                "Diese Aktion ist Bootstrap-Admins vorbehalten.");
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

    /// <summary>Require the actor's audience covers the record's secrecy level (leadership, or the record's own TRU/HRB).</summary>
    public static void RequireMaySeeClassified(ClaimsPrincipal actor, DocumentClassification level)
    {
        if (!DocumentViewerScope.From(actor).CanSee(level))
        {
            throw new UnauthorizedAccessException(
                "Diese Akte ist als Verschlusssache nur für die zuständige Stelle (Führung, TRU oder HRB) zugänglich.");
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

    /// <summary>Require read access to leadership-level content; read-only supervision is admitted.</summary>
    public static void RequireClassifiedRead(ClaimsPrincipal actor)
    {
        if (!actor.MayClassifiedRead())
        {
            throw new UnauthorizedAccessException(
                "Diese Auswertung ist der Führung und der Aufsicht vorbehalten.");
        }
    }

    /// <summary>Require the right to author meetings and agenda items.</summary>
    public static void RequireMeetingWrite(ClaimsPrincipal actor)
    {
        if (!actor.MayHighestClassification() || !actor.MayWrite())
        {
            throw new UnauthorizedAccessException(
                "Besprechungen und Tagesordnungspunkte darf nur Senior Special Agent aufwärts oder ein Admin bearbeiten.");
        }
    }

    /// <summary>Require recruiting management access (HRB or leadership).</summary>
    public static void RequireHrbOrLeadership(ClaimsPrincipal actor)
    {
        if (!actor.IsHrbOrLeadership())
        {
            throw new UnauthorizedAccessException(
                "Diese Aktion ist dem HRB und der Führung vorbehalten.");
        }
    }

    /// <summary>Require applicant status (portal owner actions).</summary>
    public static void RequireApplicant(ClaimsPrincipal actor)
    {
        if (!actor.IsApplicant())
        {
            throw new UnauthorizedAccessException(
                "Diese Aktion ist nur für Bewerber verfügbar.");
        }
    }
}

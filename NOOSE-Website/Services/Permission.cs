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
        if (!actor.MayCounterIntel())
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

    /// <summary>Require the actor may use NOOSEI (never partners, demo sessions or read-only supervision).</summary>
    public static void RequireLlmUse(ClaimsPrincipal actor)
    {
        if (actor.IsPartner() || actor.IsDemo() || actor.IsOnlyReader())
        {
            throw new UnauthorizedAccessException("NOOSEI steht in dieser Rolle nicht zur Verfügung.");
        }
    }

    /// <summary>Require the actor owns this NOOSEI conversation. Chats are working notes, not an agency record —
    /// the only other reader is the AI owner, for a concrete misuse suspicion.</summary>
    public static void RequireOwnConversation(ClaimsPrincipal actor, string ownerId)
    {
        if (!string.Equals(actor.GetAgentId(), ownerId, StringComparison.Ordinal) && !actor.IsAiOwner())
        {
            throw new UnauthorizedAccessException("Diese Unterhaltung gehört einem anderen Agenten.");
        }
    }

    /// <summary>Require the actor may read NOOSEI quotas: their own always, anyone else's (or the whole roster,
    /// with no id) only with the classified-read scope. Read-only supervision keeps the bare numbers here; the
    /// behaviour analysis on top of them stays behind <see cref="RequireLeadershipNoReader"/>.</summary>
    public static void RequireQuotaRead(ClaimsPrincipal actor, string? agentId = null)
    {
        if (agentId is not null && string.Equals(actor.GetAgentId(), agentId, StringComparison.Ordinal))
        {
            return;
        }
        if (!actor.MayClassifiedRead())
        {
            throw new UnauthorizedAccessException("Kein Zugriff auf dieses NOOSEI-Kontingent.");
        }
    }

    /// <summary>Require the configured AI owner; everyone else may read the quotas but never change them.</summary>
    public static void RequireAiOwner(ClaimsPrincipal actor)
    {
        if (!actor.IsAiOwner())
        {
            throw new UnauthorizedAccessException(
                "Die KI-Kontingente kann nur der KI-Eigner ändern.");
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

    /// <summary>Require an internal agent; twin of <c>Policies.InternalAgent</c>, denies partner agencies.</summary>
    public static void RequireInternalAgent(ClaimsPrincipal actor)
    {
        if (actor.IsPartner())
        {
            throw new UnauthorizedAccessException(
                "Dieser Bereich ist internen Agenten vorbehalten.");
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

    /// <summary>Require the right to file an evidence-room entry: depositing is open to every writing agent, taking something out stays with leadership.</summary>
    public static void RequireEvidenceEntryWrite(ClaimsPrincipal actor, EvidenceEntryType type)
    {
        if (!actor.MayWrite())
        {
            throw new UnauthorizedAccessException(
                "Nur-Lese-Modus: Änderungen sind in dieser Rolle nicht möglich.");
        }
        // fail closed: anything that is not a deposit needs leadership
        if (type != EvidenceEntryType.Deposit && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Herausnahmen aus der Asservatenkammer bucht nur die Führung; einlagern darf jeder Agent.");
        }
    }

    /// <summary>Require the right to picture an evidence item: a first picture is additive, replacing an existing one stays with leadership.</summary>
    public static void RequireEvidenceImageWrite(ClaimsPrincipal actor, bool itemHasImage)
    {
        if (!actor.MayWrite())
        {
            throw new UnauthorizedAccessException(
                "Nur-Lese-Modus: Änderungen sind in dieser Rolle nicht möglich.");
        }
        if (itemHasImage && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Ein vorhandenes Bild ersetzt nur die Führung.");
        }
    }

    /// <summary>Require the right to file a treasury booking: paying in is open to every writing agent, paying out and setting the balance stay with leadership.</summary>
    public static void RequireKassenBookingWrite(ClaimsPrincipal actor, KassenBuchungArt kind)
    {
        if (!actor.MayWrite())
        {
            throw new UnauthorizedAccessException(
                "Nur-Lese-Modus: Änderungen sind in dieser Rolle nicht möglich.");
        }
        // fail closed: anything that is not a deposit needs leadership
        if (kind != KassenBuchungArt.Einzahlung && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Auszahlungen und Korrekturen bucht nur die Führung; einzahlen darf jeder Agent.");
        }
    }

    /// <summary>Require the right to edit and publish editorial pages of the public area.</summary>
    /// <remarks>
    /// Reading them stays with <see cref="RequireClassifiedRead"/>: the read-only supervision must be able to see what
    /// the agency says publicly, it just may not be the one saying it.
    /// </remarks>
    public static void RequirePublicPageWrite(ClaimsPrincipal actor)
    {
        if (!actor.IsLeadership() || !actor.MayWrite())
        {
            throw new UnauthorizedAccessException(
                "Öffentliche Seiten bearbeitet und veröffentlicht nur die Führung.");
        }
    }

    /// <summary>Require the right to write and publish a press release.</summary>
    /// <remarks>
    /// Internal agent first: a signed-in citizen carries no rank claim at all, so a rank check alone would answer the
    /// wrong question. Write access before the rank check, so the read-only supervision and the demo principal are out
    /// before a case number is minted. Reading stays with <see cref="RequireClassifiedRead"/>: the supervision must be
    /// able to see what the agency says publicly, it just may not be the one saying it.
    /// </remarks>
    public static void RequirePressWrite(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite() || !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Pressemitteilungen schreibt und veröffentlicht nur die Führung.");
        }
    }

    /// <summary>Require the right to write and publish a public warning.</summary>
    /// <remarks>
    /// Same shape and same order as <see cref="RequirePressWrite"/>, and its own guard rather than a shared one: the
    /// message names the area, and two areas that happen to admit the same people today may not tomorrow.
    /// </remarks>
    public static void RequireWarningWrite(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite() || !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Öffentliche Warnungen schreibt und veröffentlicht nur die Führung.");
        }
    }

    /// <summary>Require the right to write and publish a released monthly situation report.</summary>
    /// <remarks>
    /// Same shape and same order as <see cref="RequirePressWrite"/>, and its own guard for the same reason: the
    /// message names the area. The internal report is generated by a worker, the released text is a statement.
    /// </remarks>
    public static void RequirePublicReportWrite(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite() || !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Öffentliche Lageberichte schreibt und veröffentlicht nur die Führung.");
        }
    }

    /// <summary>Require the right to release a paragraph to the public law page.</summary>
    /// <remarks>
    /// Releasing is a publication, not an edit of the statute: <see cref="ILawService"/> curates the text with
    /// <see cref="RequireLeadership"/>, this decides whether it leaves the house.
    /// </remarks>
    public static void RequireLawReleaseWrite(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite() || !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException(
                "Gesetzesauszüge gibt nur die Führung nach außen frei.");
        }
    }

    /// <summary>Require the right to author a public wanted notice.</summary>
    /// <remarks>
    /// Not <see cref="RequireWriteAccess"/>: that one only blocks the read-only supervision and partners, so a
    /// signed-in citizen — who carries no rank claim — would fall into the "rank 1-2 files a request" branch and could
    /// file a publication request against an internal file. Publishing itself still needs rank ≥ 3; below that the
    /// service turns the attempt into a request.
    /// </remarks>
    public static void RequirePublicWantedWrite(ClaimsPrincipal actor)
    {
        if (!actor.MayWrite() || actor.IsCitizen() || string.IsNullOrEmpty(actor.GetAgentId()))
        {
            throw new UnauthorizedAccessException(
                "Öffentliche Ausschreibungen bearbeitet nur ein schreibberechtigter Agent.");
        }
    }

    /// <summary>Require the right to see the list of all public wanted notices across records.</summary>
    /// <remarks>
    /// Deliberately wider than <see cref="RequireClassifiedRead"/> (rank ≥ 4): a Senior Special Agent publishes
    /// directly and must be able to work the list. The read-only supervision is admitted as everywhere else.
    /// Which rows they then see is decided per record — see <see cref="RequirePublicWantedRecordRead"/>.
    /// </remarks>
    public static void RequirePublicWantedRead(ClaimsPrincipal actor)
    {
        if (!actor.MayHighestClassification() && !actor.IsOnlyReader())
        {
            throw new UnauthorizedAccessException(
                "Öffentliche Ausschreibungen sieht Senior Special Agent aufwärts und die Aufsicht.");
        }
    }

    /// <summary>Require an internal agent for the notice of one record.</summary>
    /// <remarks>
    /// Wider than <see cref="RequirePublicWantedRead"/> on purpose: a rank 1-2 agent may prepare a notice and file a
    /// publication request, so he must be able to open the one he is working on. It is not a permission to read the
    /// underlying file — the service answers "not found" for a notice whose record the caller may not see, exactly as
    /// <see cref="Visibility.IsRecordVisibleAsync"/> decides everywhere else.
    /// </remarks>
    public static void RequirePublicWantedRecordRead(ClaimsPrincipal actor)
    {
        if (actor.IsPartner() || actor.IsCitizen() || string.IsNullOrEmpty(actor.GetAgentId()))
        {
            throw new UnauthorizedAccessException(
                "Öffentliche Ausschreibungen sieht nur ein interner Agent.");
        }
    }

    /// <summary>Require the right to author a public organisation profile.</summary>
    /// <remarks>
    /// Not <see cref="RequireWriteAccess"/>: that one only blocks the read-only supervision and partners, so a
    /// signed-in citizen — who carries no rank claim — would pass. Publishing itself additionally needs rank ≥ 3,
    /// checked after this one so the supervision and the demo principal cannot mint a publication date first.
    /// </remarks>
    public static void RequirePublicFactionProfileWrite(ClaimsPrincipal actor)
    {
        if (!actor.MayWrite() || actor.IsCitizen() || actor.IsPartner() || string.IsNullOrEmpty(actor.GetAgentId()))
        {
            throw new UnauthorizedAccessException(
                "Öffentliche Organisationsprofile bearbeitet nur ein schreibberechtigter Agent.");
        }
    }

    /// <summary>Require the right to read organisation profiles at all.</summary>
    /// <remarks>
    /// Same threshold as publishing, plus the read-only supervision as everywhere else. There is no second, wider
    /// read guard as with the wanted notice: a rank 1-2 agent cannot own a draft here, so it would admit nobody.
    /// Which rows the actor then sees is still decided per faction file.
    /// </remarks>
    public static void RequirePublicFactionProfileRead(ClaimsPrincipal actor)
    {
        if (!actor.MayHighestClassification() && !actor.IsOnlyReader())
        {
            throw new UnauthorizedAccessException(
                "Öffentliche Organisationsprofile sieht Senior Special Agent aufwärts und die Aufsicht.");
        }
    }

    /// <summary>Require the right to put money on a head, whether the agency's or the actor's own.</summary>
    /// <remarks>
    /// RequireWriteAccess alone is not this guard: it blocks only the read-only supervision and partners, so a signed-in
    /// citizen would pass. A bounty is filed by an internal agent or by nobody.
    /// </remarks>
    public static void RequireBountyWrite(ClaimsPrincipal actor)
    {
        if (!actor.MayWrite() || actor.IsCitizen() || actor.IsPartner() || string.IsNullOrEmpty(actor.GetAgentId()))
        {
            throw new UnauthorizedAccessException(
                "Kopfgeld setzt nur ein schreibberechtigter Agent.");
        }
    }

    /// <summary>Require the right to read the citizen tip inbox.</summary>
    /// <remarks>
    /// Every internal agent, the read-only supervision included — it reads everything by design, and locking it out
    /// of the inbox would be the one place it cannot look. A partner, a citizen and an applicant stay outside.
    /// </remarks>
    public static void RequireTipRead(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent())
        {
            throw new UnauthorizedAccessException("Bürgerhinweise sieht nur ein interner Agent.");
        }
    }

    /// <summary>Require the right to work a citizen tip.</summary>
    /// <remarks>
    /// Triage is everyday work, so this is every internal agent who may write — not a rank gate. RequireWriteAccess
    /// alone would not do it: it blocks the read-only supervision and partners, but a signed-in citizen carries no
    /// rank claim and would walk into the inbox that holds other citizens' submissions.
    /// </remarks>
    public static void RequireTipHandling(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite())
        {
            throw new UnauthorizedAccessException(
                "Bürgerhinweise bearbeitet nur ein schreibberechtigter Agent.");
        }
    }

    /// <summary>Require the right to pay a reward out to a tipster.</summary>
    /// <remarks>
    /// Its own axis, and not RequireKassenBookingWrite: that one only fires on the booking branch, so a reward paid
    /// entirely from a donor's own pocket would move money with no leadership check at all. The write guard comes
    /// first: RequireLeadership alone admits the read-only supervision and the demo principal, which would mint
    /// receipt numbers before the ReadOnlyBarrierInterceptor refuses the save.
    /// </remarks>
    public static void RequireRewardPayout(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite() || !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Belohnungen zahlt nur die Führung aus.");
        }
    }

    /// <summary>Require the right to read the citizen ticket desk.</summary>
    /// <remarks>
    /// MayClassifiedRead is exactly leadership or the read-only supervision, which makes this the service side of
    /// Policies.LeadershipPage — the supervision reads the desk, it just cannot answer. IsInternalAgent comes first
    /// because a signed-in citizen carries no rank claim at all.
    /// </remarks>
    public static void RequireTicketRead(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayClassifiedRead())
        {
            throw new UnauthorizedAccessException("Bürger-Tickets sieht nur die Führung.");
        }
    }

    /// <summary>Require the right to answer or decide a citizen ticket.</summary>
    /// <remarks>
    /// The write check stands before the rank one: RequireLeadership alone admits the read-only supervision and the
    /// demo principal, which would then mint a case number before the ReadOnlyBarrierInterceptor refuses the save.
    /// </remarks>
    public static void RequireTicketHandling(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite() || !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Bürger-Tickets bearbeitet nur die Führung.");
        }
    }

    /// <summary>Require the right to read the objections against public wanted notices.</summary>
    /// <remarks>
    /// The same set that works the notice list, not the ticket desk's: an objection is fahndungs-work, and the agent
    /// who published the notice has to be able to see it disputed. Deciding is narrower — see
    /// <see cref="RequireObjectionHandling"/>.
    /// </remarks>
    public static void RequireObjectionRead(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || (!actor.MayHighestClassification() && !actor.IsOnlyReader()))
        {
            throw new UnauthorizedAccessException(
                "Einsprüche sieht Senior Special Agent aufwärts und die Aufsicht.");
        }
    }

    /// <summary>Require the right to decide an objection.</summary>
    /// <remarks>
    /// The write check stands before the rank one, as with the ticket and payout guards: RequireLeadership alone lets
    /// the read-only supervision and the demo principal through, and they would then mint a case number before the
    /// ReadOnlyBarrierInterceptor refuses the save. IsInternalAgent comes first because a signed-in citizen carries
    /// no rank claim at all.
    /// </remarks>
    public static void RequireObjectionHandling(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite() || !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Einsprüche entscheidet nur die Führung.");
        }
    }

    /// <summary>Require the right to write a template for citizen-facing messages.</summary>
    /// <remarks>
    /// The write check stands before the rank one, same as the ticket and payout guards: RequireLeadership alone lets
    /// the read-only supervision and the demo principal through, and they would then edit the text every citizen gets
    /// until the ReadOnlyBarrierInterceptor refuses the save. Reading a template needs no guard at all — it is agency
    /// boilerplate, and the automatic confirmation is read while a citizen, not an agent, is acting.
    /// </remarks>
    public static void RequirePublicTemplateWrite(ClaimsPrincipal actor)
    {
        if (!actor.IsInternalAgent() || !actor.MayWrite() || !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Öffentliche Vorlagen pflegt nur die Führung.");
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

    /// <summary>Require an account that may use the citizen area; every signed-in account qualifies, not only Civilian.</summary>
    public static void RequireCitizenPortal(ClaimsPrincipal actor)
    {
        if (!actor.MayUseCitizenPortal())
        {
            throw new UnauthorizedAccessException(
                "Der Bürgerbereich steht nur angemeldeten Konten zur Verfügung.");
        }
    }
}

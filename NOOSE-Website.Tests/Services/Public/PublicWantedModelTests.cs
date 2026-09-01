using System.Collections;
using System.Reflection;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>Anonymity is structural: what a public projection cannot carry, no page can render by accident.</summary>
/// <remarks>
/// Every type in <c>NOOSE_Website.Models.Public</c> is decided either way — outward or inward, each with a reason.
/// The list used to be outward-only and hand-kept, which meant a brand-new outward DTO was simply unlisted and no
/// test went red. Reflection over the namespace turns that into a decision, the same mechanism as
/// <c>PublicVisibility</c>.
/// </remarks>
public class PublicWantedModelTests
{
    /// <summary>Every type an anonymous visitor's page is allowed to render.</summary>
    private static readonly Type[] Outward =
    [
        typeof(PublicWantedCard),
        typeof(PublicWantedDetail),
        typeof(PublicWantedArchiveCard),
        typeof(PublicWantedHint),
        typeof(PublicWantedBoard),
        typeof(PublicWantedPhoto),
        typeof(PublicBounty),
        typeof(PublicBountyAnnouncement),
        typeof(PublicPageLink),
        typeof(PublicPageView),
        typeof(PublicPageSnapshot),
        // the public shell renders these two as well: the nav tabs and the career page's requirement list
        typeof(PublicModuleState),
        typeof(NOOSE_Website.Models.Recruiting.CareerRequirement),
        // a citizen reading their own tip is not an agent; the same rules apply
        typeof(CitizenTipRow),
        typeof(CitizenTipDetail),
        typeof(CitizenTipMessage),
        typeof(CitizenRewardRow),
        typeof(CitizenRewardReceipt),
        typeof(CitizenTicketRow),
        typeof(CitizenTicketDetail),
        typeof(CitizenTicketMessage),
        typeof(CitizenObjectionRow),
        // the organisation hub and both hazard rankings render these
        typeof(PublicFactionCard),
        typeof(PublicFactionBoard),
        // the press hub and one release
        typeof(PublicPressCard),
        typeof(PublicPressView),
        typeof(PublicPressSnapshot),
        // the warning hub renders the whole body on the card; there is no page of its own
        typeof(PublicWarningCard),
        typeof(PublicWarningSnapshot),
        // the report hub and one released month
        typeof(PublicReportCard),
        typeof(PublicReportView),
        typeof(PublicReportSnapshot),
        // the situation level: what the agency says, and the level that stood before it
        typeof(PublicSituationState),
        // the law page: the statute itself, grouped by book
        typeof(PublicLawEntry),
        typeof(PublicLawBook),
        typeof(PublicLawSnapshot),
    ];

    /// <summary>Types that never reach an anonymous page, each with the reason.</summary>
    private static readonly Dictionary<Type, string> Inward = new()
    {
        [typeof(PublicWantedEdit)] = "Zeile der internen Verwaltungsliste; trägt Codename und Aufrufzähler.",
        [typeof(PublicWantedDraft)] = "Die eine Ausschreibung im Editor, inklusive Entwurfs-HTML.",
        [typeof(PublicWantedOptions)] = "Foto- und Gegend-Auswahl aus der Akte; live gelesen, nie Snapshot.",
        [typeof(PublicWantedPhotoOption)] = "Ein Aktenfoto zur Auswahl im Editor.",
        [typeof(PublicWantedItemSource)] = "Fahrzeug- oder Waffenzeile des Steckbriefs zur Auswahl im Panel; "
            + "trägt die Zeilen-Id, die nach dem nächsten Speichern der Akte ohnehin eine andere ist.",
        [typeof(PublicWantedBanner)] = "Warnbanner auf der Personenakte; rein intern.",
        [typeof(PublicWantedRequestRow)] = "Offener Veröffentlichungsantrag im Posteingang.",
        [typeof(ObjectionRow)] = "Zeile des Einspruchs-Abschnitts; nennt den Bürger und den Entscheider.",
        [typeof(ObjectionDetail)] = "Ein Einspruch in der Bearbeitung; trägt Zeilen-Ids und den Sperrstatus des Kontos.",
        [typeof(ObjectionCounts)] = "Zähler der beiden Abschnitte; eine Aussage über die Arbeitslast der Behörde.",
        [typeof(ObjectionInput)] = "Formulareingabe des Bürgers; wandert nach innen, nicht nach außen.",
        [typeof(PressEdit)] = "Zeile des Presse-Panels; nennt den veröffentlichenden Agenten und den Discord-Stempel.",
        [typeof(PressDraft)] = "Der unveröffentlichte Entwurf einer Mitteilung; anonym gibt es ihn nicht.",
        [typeof(PressInput)] = "Editor-Eingabe des Panels; wandert nach innen, nicht nach außen.",
        [typeof(WarningEdit)] = "Zeile des Warnungs-Panels; nennt den veröffentlichenden Agenten und den "
            + "Abgelaufen-Zustand einer Zeile, die draußen gar nicht mehr steht.",
        [typeof(WarningDraft)] = "Der unveröffentlichte Entwurf einer Warnung; anonym gibt es ihn nicht.",
        [typeof(WarningInput)] = "Editor-Eingabe des Panels; wandert nach innen, nicht nach außen.",
        [typeof(LawReleaseRow)] = "Zeile des Freigabe-Panels; sie nennt auch die Paragrafen, die drinnen bleiben.",
        [typeof(PublicReportEdit)] = "Zeile des Lageberichts-Panels; nennt den veröffentlichenden Agenten und den "
            + "internen Monatsbericht als Anker.",
        [typeof(PublicReportDraft)] = "Der unveröffentlichte Entwurf eines Monatstexts; anonym gibt es ihn nicht.",
        [typeof(PublicReportAnchor)] = "Ein archivierter Monatsbericht zur Auswahl im Panel; er trägt dessen interne Id.",
        [typeof(PublicReportInput)] = "Editor-Eingabe des Panels; wandert nach innen, nicht nach außen.",
        [typeof(PublicSituationInput)] = "Panel-Eingabe der Gefahrenlage; Datum und Vorgängerstufe stehen bewusst "
            + "nicht darauf, sie werden abgeleitet statt vom Client geliefert.",
        [typeof(PublicWantedInput)] = "Formulareingabe des internen Editors.",
        [typeof(PublicFactionProfileEdit)] = "Zeile der internen Verwaltungsliste; trägt Codename und Aktenzeichen.",
        [typeof(PublicFactionProfileDraft)] = "Das eine Profil im Editor, inklusive Entwurfs-HTML und FraktionId.",
        [typeof(PublicFactionProfileBanner)] = "Warnbanner auf der Fraktionsakte; rein intern.",
        [typeof(PublicFactionProfileInput)] = "Formulareingabe des internen Editors.",
        [typeof(PublicPageEdit)] = "Zeile der Redaktionsliste mit internem Bearbeiter.",
        [typeof(PublicPageInput)] = "Formulareingabe des Seiteneditors.",
        [typeof(PublicModuleDefinition)] = "Katalogzeile aus dem Code; Konfiguration, kein Inhalt.",
        [typeof(PublicModuleSnapshot)] = "Schalterstand samt Not-Aus; die Seiten lesen daraus nur IsEnabled.",
        [typeof(PublicModuleInput)] = "Formulareingabe des Modul-Panels.",
        [typeof(CitizenRow)] = "Bürgerkonto in der Verwaltungsliste; enthält den Klarnamen.",
        [typeof(WarnhinweisUsage)] = "Werteliste-Zeile mit Verwendungszähler; nach außen geht nur das Label.",
        [typeof(WarnhinweisOption)] = "Auswahl im Editor-Picker; trägt die Zeilen-Id.",
        [typeof(WarnhinweisInput)] = "Formulareingabe des Warnhinweis-Dialogs.",
        [typeof(BountyShareRow)] = "Ein Kopfgeld-Anteil im internen Panel; nennt Herkunft, Stifter und Konto.",
        [typeof(BountySummary)] = "Aufschlüsselung der Summe; nach außen geht ausschließlich Advertised.",
        [typeof(BountyCoverage)] = "Deckung eines Kassenkontos; ein Kontostand verlässt das Haus nie.",
        [typeof(BountyRequestRow)] = "Offener Kopfgeld-Antrag im Posteingang.",
        [typeof(TipRow)] = "Zeile des Bearbeiter-Eingangs; nennt Hinweisgeber und Bearbeiter.",
        [typeof(TipDetail)] = "Ein Hinweis in der Bearbeitung, inklusive Vertrauensstufe des Hinweisgebers.",
        [typeof(TipMessageRow)] = "Nachricht mit Autor; die interne Zielgruppe verlässt das Haus nie.",
        [typeof(TipInboxCounts)] = "Zähler der Eingangs-Abschnitte; eine Aussage über die Arbeitslast der Behörde.",
        [typeof(TipDuplicateRow)] = "Geschwister einer Dublettengruppe im Eingang; trägt Aktenzeichen und Auszug.",
        [typeof(TipHistoryRow)] = "Hinweisgeber-Historie an der Personenakte; anonyme Hinweise fehlen darin ganz.",
        [typeof(TipAttachmentAccess)] = "Dateiname und Typ für den autorisierten Ausliefer-Endpoint.",
        [typeof(TipInput)] = "Formulareingabe des Hinweis-Formulars.",
        [typeof(RewardRow)] = "Ausgezahlte Belohnung im internen Panel; nennt Herkunft, Konto und Kassenbuchung.",
        [typeof(RewardDraft)] = "Vorbereitung der Auszahlung; nennt die Aufschlüsselung des Kopfgelds.",
        [typeof(RewardDraftTip)] = "Auszahlbarer Hinweis im Dialog, inklusive Klarname des Hinweisgebers.",
        [typeof(RewardDraftBlocked)] = "Nicht auszahlbarer Hinweis mit dem internen Grund dafür.",
        [typeof(RewardPayoutInput)] = "Formulareingabe des Auszahlungs-Dialogs; trägt Zeilen-Ids.",
        [typeof(RewardTipAmount)] = "Eine Zeile der Verteilung; trägt die Zeilen-Id des Hinweises.",
        [typeof(TipRewardTarget)] = "Übergabewert zwischen Auszahlung und Nachlauf; trägt Zeilen-Ids.",
        [typeof(TicketRow)] = "Zeile des Führungs-Schalters; nennt Bürger und Bearbeiter.",
        [typeof(TicketDetail)] = "Ein Ticket in der Bearbeitung, inklusive Sperrstatus des Bürgerkontos.",
        [typeof(TicketMessageRow)] = "Nachricht mit Autor; die interne Zielgruppe verlässt das Haus nie.",
        [typeof(TicketInput)] = "Formulareingabe des Ticket-Dialogs.",
        [typeof(PublicTemplateRow)] = "Vorlage mit rohen Tokens und internem Arbeitstitel; nach außen geht die "
            + "gerenderte Nachricht.",
        [typeof(PublicTemplateInput)] = "Formulareingabe des Vorlagen-Dialogs.",
    };

    /// <summary>Anything that names an agent, a record id or an internal identifier.</summary>
    private static readonly string[] Forbidden =
    [
        "PersonId", "FraktionId", "FactionId", "AgentId", "UserId",
        "Codename", "RealName", "Klarname", "Dienstgrad", "Rank", "BadgeNumber",
        "PublishedBy", "CreatedBy", "ModifiedBy", "DeletedBy",
    ];

    /// <summary>Outward types plus the element types of any collection they expose.</summary>
    private static IEnumerable<Type> OutwardClosure()
    {
        var seen = new HashSet<Type>(Outward);
        foreach (var type in Outward)
        {
            foreach (var property in type.GetProperties())
            {
                // without this a list of chips on the detail record would go unchecked
                if (property.PropertyType.IsGenericType
                    && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    foreach (var argument in property.PropertyType.GetGenericArguments())
                    {
                        if (argument.Namespace?.StartsWith("NOOSE_Website.Models", StringComparison.Ordinal) == true)
                        {
                            seen.Add(argument);
                        }
                    }
                }
            }
        }
        return seen;
    }

    [Fact]
    public void EveryPublicModel_IsDecidedOutwardOrInward()
    {
        var undecided = typeof(PublicWantedCard).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "NOOSE_Website.Models.Public")
            // static classes (IsAbstract && IsSealed) are display helpers, not projections
            .Where(t => t.IsPublic && !t.IsEnum && !t.IsInterface && !t.IsNested
                && !(t.IsAbstract && t.IsSealed))
            .Where(t => !Outward.Contains(t) && !Inward.ContainsKey(t))
            .Select(t => t.Name)
            .Order()
            .ToArray();

        Assert.True(undecided.Length == 0,
            "Jedes Modell des öffentlichen Bereichs ist entweder Nach-außen oder Nach-innen — mit Begründung: "
            + string.Join(", ", undecided));
    }

    [Fact]
    public void OutwardModels_CarryNoInternalIdentifier()
    {
        var offenders = OutwardClosure()
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, p.Name)))
            .Where(x => Forbidden.Any(f => x.Name.Contains(f, StringComparison.Ordinal)))
            .Select(x => $"{x.Type.Name}.{x.Name}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Öffentliche Projektionen dürfen keinen internen Bezeichner tragen: " + string.Join(", ", offenders));
    }

    [Fact]
    public void OutwardModels_CarryNoBareRecordId()
    {
        var offenders = OutwardClosure()
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, p.Name)))
            .Where(x => x.Name == "Id")
            .Select(x => $"{x.Type.Name}.{x.Name}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Eine öffentliche Projektion wird über ihr Aktenzeichen adressiert, nie über die Zeilen-Id: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void NoOutwardModelExposesANumericThreatScore()
    {
        // the hazard level goes out, the raw 0-100 value does not: it is the output of the scoring algorithm and
        // watched over time it says when NOOSE acted
        var offenders = OutwardClosure()
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, p.Name, p.PropertyType)))
            .Where(x => x.Name.Contains("Score", StringComparison.Ordinal)
                || (x.Name.Contains("Hazard", StringComparison.Ordinal) && x.PropertyType != typeof(HazardLevel)))
            .Select(x => $"{x.Type.Name}.{x.Name}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Nach außen geht die Gefahrenstufe, nicht der Zahlenwert: " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoOutwardModelExposesAViewCounter()
    {
        // a reach figure is a statement about the agency's own operation; it lives on the internal projection only
        var offenders = OutwardClosure()
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, p.Name)))
            .Where(x => x.Name.Contains("ViewCount", StringComparison.Ordinal)
                || x.Name.Contains("Aufruf", StringComparison.Ordinal))
            .Select(x => $"{x.Type.Name}.{x.Name}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Der Aufrufzähler bleibt drinnen: " + string.Join(", ", offenders));
    }

    [Fact]
    public void TheWantedDetail_CarriesTheHazardLevelAsAnEnum()
    {
        var property = typeof(PublicWantedDetail).GetProperty(nameof(PublicWantedDetail.HazardLevel));
        Assert.NotNull(property);
        Assert.Equal(typeof(HazardLevel), property!.PropertyType);
    }

    [Fact]
    public void TheArchiveCard_CarriesNoHazardLevelNoAccusationNoAreaAndNoVehicle()
    {
        // its own type rather than nullable fields on the board card: the archive states that someone was caught,
        // it does not restate the allegation
        var names = typeof(PublicWantedArchiveCard).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain("HazardLevel", names);
        Assert.DoesNotContain("ChargeHtml", names);
        Assert.DoesNotContain("LastArea", names);
        Assert.DoesNotContain("VehicleText", names);
        Assert.Contains("CapturedAt", names);
    }

    [Fact]
    public void TheHint_CarriesOnlyALabelAndAColour()
    {
        var properties = typeof(PublicWantedHint).GetProperties()
            .Where(p => p.DeclaringType == typeof(PublicWantedHint))
            .ToArray();

        Assert.Equal(2, properties.Length);
        Assert.All(properties, p => Assert.Equal(typeof(string), p.PropertyType));
    }
}

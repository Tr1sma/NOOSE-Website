using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>
/// A REQUIRED navigation to a soft-deletable entity is joined INNER and inherits that entity's query filter, so
/// deleting the principal makes the dependent row vanish from every read that dereferences it - silently.
/// </summary>
/// <remarks>
/// <para>
/// This exact trap cost three separate defects: the objection desk (a list and its own counter disagreeing), the
/// citizen's paid reward receipt (which answered "does not exist" once its tip was deleted, although the payment
/// row is deliberately NOT soft-deletable), and the public organisation profile (which dropped out of the very
/// management list whose gate exists to keep a leftover profile manageable).
/// </para>
/// <para>
/// Reflection can see the navigation but not whether a read path dereferences it, so this is a decision registry
/// rather than a proof: every such navigation on a public-area entity is listed with what was decided about it.
/// A new one turns the build red until somebody has thought about it, which is the whole point.
/// </para>
/// </remarks>
public class PublicRequiredNavigationTests
{
    /// <summary>Every required navigation to a soft-deletable principal, and what was decided.</summary>
    private static readonly Dictionary<string, string> Decided = new(StringComparer.Ordinal)
    {
        ["FahndungEinspruch.Wanted"] = "alle vier Lesepfade gewurzelt, !IsDeleted von Hand zurück (ObjectionService)",
        ["FahndungKopfgeldAnteil.Wanted"] = "Anteile werden immer über die Ausschreibung gelesen, nie umgekehrt",
        ["HinweisBelohnung.Tip"] = "alle drei Lesepfade gewurzelt; Löschen eines belohnten Hinweises abgewiesen",
        ["Hinweis.CitizenProfile"] = "Eingang gewurzelt, !IsDeleted von Hand zurück - die Zähler berühren keine "
            + "Navigation und hätten sonst Zeilen behauptet, die die Liste nicht zeigt",
        ["TicketParticipant.Ticket"] = "Die Beteiligung wird immer über ihr Ticket gelesen, nie umgekehrt; "
            + "die eigene Liste joint bewusst über die Navigation, damit ein gelöschtes Ticket dort verschwindet",
        ["FahndungEinspruch.CitizenProfile"] = "die Einspruchs-Lesepfade sind schon gewurzelt (siehe Wanted)",
        ["FahndungWarnhinweis.Fahndung"] = "Zuordnungen werden über ihre Ausschreibung gelesen; der "
            + "Verwendungszähler joint bewusst nicht, sondern prüft die lebende Zeile per Subquery",
        ["HinweisNachricht.Hinweis"] = "Nachrichten werden nur über ihren Hinweis gelesen, der dann selbst lebt",
        ["OeffentlichesFraktionsprofil.Faction"] = "Verwaltungs-Lesepfade gewurzelt; der öffentliche Board-Pfad "
            + "darf den Filter NICHT heben, dort ist der Gürtel die Kontrolle",
        ["TicketNachricht.Ticket"] = "Nachrichten werden nur über ihr Ticket gelesen, das dann selbst lebt",
    };

    private static List<string> RequiredNavigationsToSoftDeletables()
    {
        using var ctx = new SqliteTestContext();
        using var db = ctx.NewContext();

        var found = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            // the public area only: roughly seventy internal child tables share the shape, and deciding those is
            // its own piece of work rather than a drive-by
            if (entity.ClrType.Namespace?.StartsWith("NOOSE_Website.Data.Entities.Public", StringComparison.Ordinal)
                != true)
            {
                continue;
            }

            foreach (var navigation in entity.GetNavigations())
            {
                if (navigation.IsCollection || !navigation.ForeignKey.IsRequired)
                {
                    continue;
                }
                if (!typeof(ISoftDelete).IsAssignableFrom(navigation.TargetEntityType.ClrType))
                {
                    continue;
                }
                found.Add($"{entity.ClrType.Name}.{navigation.Name}");
            }
        }
        return found;
    }

    [Fact]
    public void EveryRequiredNavigationToASoftDeletable_IsDecided()
    {
        var navigations = RequiredNavigationsToSoftDeletables();
        // a wrong namespace filter would otherwise leave this green forever
        Assert.NotEmpty(navigations);

        var undecided = navigations
            .Where(n => !Decided.ContainsKey(n))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(undecided.Length == 0,
            "Eine Pflicht-Navigation auf eine soft-löschbare Entität wird INNER gejoint und erbt deren Filter: "
            + "jeder Lesepfad, der sie dereferenziert, verliert die Zeile still. Entscheiden und in Decided "
            + "eintragen: " + string.Join(", ", undecided));
    }

    [Fact]
    public void TheRegistryNamesOnlyNavigationsThatStillExist()
    {
        var navigations = RequiredNavigationsToSoftDeletables().ToHashSet(StringComparer.Ordinal);
        var stale = Decided.Keys.Where(k => !navigations.Contains(k)).Order(StringComparer.Ordinal).ToArray();

        Assert.True(stale.Length == 0,
            "Diese Navigation gibt es nicht mehr oder sie ist nicht mehr Pflicht; Eintrag entfernen: "
            + string.Join(", ", stale));
    }

    [Fact]
    public void EveryDecisionCarriesAReason()
    {
        var blank = Decided.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key)
            .Order(StringComparer.Ordinal).ToArray();

        Assert.True(blank.Length == 0, "Eine Entscheidung braucht eine Begründung: " + string.Join(", ", blank));
    }

    /// <summary>The three services that were bitten actually root their reads.</summary>
    [Fact]
    public void TheBittenReadPathsAreRooted()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "NOOSE-Website", "Services", "Public"));
        Assert.True(Directory.Exists(root), $"Dienstordner nicht gefunden: {root}");

        foreach (var name in new[] { "ObjectionService.cs", "RewardService.cs", "PublicFactionProfileService.cs" })
        {
            var text = File.ReadAllText(Path.Combine(root, name));
            Assert.Contains("IgnoreQueryFilters", text, StringComparison.Ordinal);
        }
    }
}

using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace NOOSE_Website.Tests.Services;

/// <summary>
/// Every <c>Restrict</c> foreign key into the Identity user table blocks a hard account delete, and the cleanup in
/// <c>AgentManagementService.DeleteAccountAsync</c> is a hand-maintained list. The SQLite suite cannot see the
/// resulting failure at all — <c>SqliteTestContext</c> runs with <c>PRAGMA foreign_keys = OFF</c> — so the check has
/// to go through the model instead of through an execution.
/// </summary>
/// <remarks>
/// A new table with an agent pointer therefore turns this test red until someone decides what deleting an account
/// does to it: drop the row, or null the pointer and keep the history. <see cref="Unhandled"/> records the ones that
/// are knowingly still open, each with its reason, in the shape <c>PublicVisibility</c> uses.
/// </remarks>
public class AgentDeleteCoverageTests
{
    /// <summary>Restrict pointers to an agent that the delete does NOT clear yet, with the reason it is open.</summary>
    /// <remarks>
    /// Pre-existing debt outside the public area, listed so it is visible rather than silent. Each of these makes a
    /// hard delete of an account that touched the feature fail; none of them is reachable from the public surface.
    /// </remarks>
    private static readonly Dictionary<string, string> Unhandled = new(StringComparer.Ordinal)
    {
        ["Absence.AgentId"] = "Abmeldungen: eigene Historie, noch nicht entschieden ob löschen oder entkoppeln",
        ["AgentAbduction.VictimAgentId"] = "Entführungen: Vorfallshistorie, noch nicht entschieden",
        ["EvidenceEntry.HandlerAgentId"] = "Asservate: Bearbeiter einer Beweiskette, darf nicht stillschweigend fallen",
        ["Feedback.AgentId"] = "Feedback: eigene Einsendungen, noch nicht entschieden",
        ["FinancingBudgetPeriod.AgentId"] = "Finanzierung: Geldhistorie ist append-only",
        ["FinancingRequest.AgentId"] = "Finanzierung: Geldhistorie ist append-only",
        ["Informant.HandlerId"] = "Informanten: Führungsagent einer Quelle, sicherheitsrelevant",
        ["LlmQuotaAdjustment.AgentId"] = "KI-Kontingent: Abrechnungshistorie",
        ["LlmQuotaPeriod.AgentId"] = "KI-Kontingent: Abrechnungshistorie",
        ["LlmRequestLog.AgentId"] = "KI-Protokoll: Nachweispflicht",
        ["NooseiConversation.AgentId"] = "KI-Unterhaltungen: besitzer-privat, Hard-Delete wäre der richtige Weg",
    };

    private static string ServiceSource([CallerFilePath] string here = "")
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..",
            "NOOSE-Website", "Services", "AgentManagementService.cs"));
        Assert.True(File.Exists(path), $"AgentManagementService nicht gefunden: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>Restrict pointers into the user table, as (declaring entity, property) pairs.</summary>
    private static List<(string Entity, string Property)> AgentRestrictKeys()
    {
        using var ctx = new SqliteTestContext();
        using var db = ctx.NewContext();
        var userType = db.Model.FindEntityType(typeof(NOOSE_Website.Data.Entities.Agent))
            ?? throw new InvalidOperationException("Agent ist nicht Teil des Modells.");

        var keys = new List<(string, string)>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            foreach (var fk in entity.GetForeignKeys())
            {
                if (fk.DeleteBehavior != DeleteBehavior.Restrict
                    || fk.PrincipalEntityType.ClrType != userType.ClrType)
                {
                    continue;
                }
                foreach (var property in fk.Properties)
                {
                    keys.Add((entity.ClrType.Name, property.Name));
                }
            }
        }
        return keys;
    }

    [Fact]
    public void EveryRestrictPointerToAnAgent_IsHandledOrListedAsOpen()
    {
        var source = ServiceSource();
        var keys = AgentRestrictKeys();
        // a wrong model or a renamed entity would otherwise leave this green forever
        Assert.NotEmpty(keys);

        var offenders = keys
            .Where(k => !Unhandled.ContainsKey($"{k.Entity}.{k.Property}"))
            // the cleanup addresses a table by its property name; naming it is the decision this test asks for
            .Where(k => !source.Contains(k.Property, StringComparison.Ordinal))
            .Select(k => $"{k.Entity}.{k.Property}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Restrict-Zeiger auf einen Agenten blockieren das Löschen des Kontos. Entweder in "
            + "AgentManagementService.DeleteAccountAsync aufräumen oder mit Begründung in Unhandled eintragen: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void TheOpenList_NamesOnlyPointersThatStillExist()
    {
        var keys = AgentRestrictKeys()
            .Select(k => $"{k.Entity}.{k.Property}")
            .ToHashSet(StringComparer.Ordinal);

        var stale = Unhandled.Keys.Where(k => !keys.Contains(k)).Order(StringComparer.Ordinal).ToArray();

        Assert.True(stale.Length == 0,
            "Diese Zeiger gibt es nicht mehr; Eintrag aus Unhandled entfernen: " + string.Join(", ", stale));
    }

    [Fact]
    public void EveryOpenPointer_CarriesAReason()
    {
        var blank = Unhandled.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key)
            .Order(StringComparer.Ordinal).ToArray();

        Assert.True(blank.Length == 0,
            "Ein offener Zeiger braucht eine Begründung: " + string.Join(", ", blank));
    }

    /// <summary>The two public-area pointers are cleared rather than deleted; the rows are history.</summary>
    [Fact]
    public void ThePublicAreaPointers_AreNulledNotDeleted()
    {
        var source = ServiceSource();

        Assert.Contains("x.BlockedById, (string?)null", source, StringComparison.Ordinal);
        Assert.Contains("x.DonorAgentId, (string?)null", source, StringComparison.Ordinal);
        // and not by dropping the rows: a block note and a bounty share outlive the account that made them
        Assert.DoesNotContain("db.BuergerProfile.IgnoreQueryFilters().Where(x => x.BlockedById == agentId)\n"
            + "            .ExecuteDeleteAsync", source, StringComparison.Ordinal);
    }
}

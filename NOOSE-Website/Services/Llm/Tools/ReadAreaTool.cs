using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Reads the current state of an operating area — the till, the evidence room, the board, the roster,
/// counter-intelligence, own followups, training.</summary>
/// <remarks>
/// The counterpart to <c>finde_akten</c>, and the line between them is the shape of the question: that one
/// enumerates records by attribute, this one reports a standing state that is not a list of files at all.
/// <para>Every area hands the actor straight to its own service and reports whatever comes back. An area the
/// viewer has no right to reads exactly like an empty one — a distinct refusal would make the tool a rights
/// oracle over areas whose mere existence the schema already discloses.</para>
/// </remarks>
public sealed class ReadAreaTool(
    IDbContextFactory<AppDbContext> dbFactory,
    IKassenService treasury,
    IEvidenceService evidence,
    IAnnouncementService announcements,
    ICounterIntelService counterIntel,
    IFollowupService followups,
    ITrainingModuleService training) : INooseiTool
{
    private const string Treasury = "kasse";
    private const string EvidenceRoom = "asservatenkammer";
    private const string Board = "brett";
    private const string Roster = "personal";
    private const string CounterIntelligence = "gegenaufklaerung";
    private const string Followups = "wiedervorlagen";
    private const string Training = "ausbildung";

    private static readonly string[] Areas =
        [Treasury, EvidenceRoom, Board, Roster, CounterIntelligence, Followups, Training];

    public string Name => "lies_bereich";

    public string Description =>
        "Liest den aktuellen Stand eines Bereichs: Kassenstand und letzte Buchungen, Bestand der "
        + "Asservatenkammer, Schwarzes Brett, Personalbestand, Auffälligkeiten der Gegenaufklärung, eigene "
        + "offene Wiedervorlagen, Ausbildungsmodule. Für Fragen nach einzelnen Akten oder deren Anzahl "
        + "nimm finde_akten.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["bereich"],
          "properties": {
            "bereich": { "type": "string", "enum": {{Json(Areas)}} },
            "max": { "type": "integer", "minimum": 1, "maximum": 40, "description": "Höchstzahl gelisteter Zeilen." }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var area = NooseiLimits.Text(arguments, "bereich");
        if (area is null || !Areas.Contains(area, StringComparer.OrdinalIgnoreCase))
        {
            return new NooseiToolResult(
                "Diesen Bereich gibt es nicht. Möglich sind: " + string.Join(", ", Areas) + ".", null, true);
        }

        var max = NooseiLimits.Count(arguments, "max", 15);
        var sb = new StringBuilder();
        try
        {
            switch (area.ToLowerInvariant())
            {
                case Treasury: await TreasuryAsync(sb, max, cancellationToken); break;
                case EvidenceRoom: await EvidenceAsync(sb, max, cancellationToken); break;
                case Board: await BoardAsync(sb, max, context.Actor, cancellationToken); break;
                case Roster: await RosterAsync(sb, max, context.Scope, cancellationToken); break;
                case CounterIntelligence: await CounterIntelAsync(sb, max, context.Actor, cancellationToken); break;
                case Followups: await FollowupsAsync(sb, max, context.Actor, cancellationToken); break;
                default: await TrainingAsync(sb, max, cancellationToken); break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // identical to an empty area on purpose: see the remark on the class
            return new NooseiToolResult($"Zum Bereich {area} liegt dir nichts vor.");
        }

        return sb.Length == 0
            ? new NooseiToolResult($"Zum Bereich {area} liegt dir nichts vor.")
            : new NooseiToolResult(NooseiLimits.Clip(sb.ToString(), NooseiLimits.MaxContentResultChars));
    }

    private async Task TreasuryAsync(StringBuilder sb, int max, CancellationToken ct)
    {
        var summaries = await treasury.GetSummariesAsync(ct);
        sb.AppendLine("Kasse");
        foreach (var s in summaries)
        {
            sb.Append("• ").Append(KassenKontoDisplay.Name(s.Account)).Append(": ")
                .Append(s.Balance.ToString("N0")).Append(" $ | Buchungen: ").Append(s.Count);
            if (s.LastBookingAt is { } last)
            {
                sb.Append(" | zuletzt ").Append(Fmt(last));
            }
            sb.AppendLine();
        }

        foreach (var account in summaries.Select(s => s.Account))
        {
            var ledger = await treasury.GetLedgerAsync(account, ct);
            if (ledger.Count == 0)
            {
                continue;
            }
            sb.Append("— Letzte Buchungen ").Append(KassenKontoDisplay.Name(account))
                .Append(" (").Append(Math.Min(max, ledger.Count)).Append(" von ").Append(ledger.Count).AppendLine(") —");
            foreach (var b in ledger.Take(max))
            {
                sb.Append("• ").Append(Fmt(b.Buchung.Timestamp)).Append(" | ")
                    .Append(KassenBuchungArtDisplay.Name(b.Buchung.Kind)).Append(" | ")
                    .Append(b.Buchung.Amount.ToString("N0")).Append(" $");
                if (Free(b.Buchung.Reason) is { Length: > 0 } why)
                {
                    sb.Append(" | ").Append(why);
                }
                sb.AppendLine();
            }
        }
    }

    private async Task EvidenceAsync(StringBuilder sb, int max, CancellationToken ct)
    {
        var items = await evidence.GetItemsAsync(null, null, ct);
        if (items.Count == 0)
        {
            return;
        }
        var stocked = items.Where(i => i.OnHand != 0).ToList();
        sb.AppendLine("Asservatenkammer");
        sb.Append("Asservate insgesamt: ").Append(items.Count)
            .Append(" | davon mit Bestand: ").Append(stocked.Count).AppendLine();
        var shown = stocked.OrderByDescending(i => i.OnHand).Take(max).ToList();
        sb.Append("— Bestand (").Append(shown.Count).Append(" von ").Append(stocked.Count).AppendLine(") —");
        foreach (var i in shown)
        {
            sb.Append("• ").Append(Free(i.Item.Name)).Append(": ").Append(i.OnHand);
            if (Free(i.Item.Category) is { Length: > 0 } category)
            {
                sb.Append(" | Kategorie: ").Append(category);
            }
            sb.AppendLine();
        }
    }

    private async Task BoardAsync(StringBuilder sb, int max, ClaimsPrincipal actor, CancellationToken ct)
    {
        var rows = await announcements.GetBoardAsync(actor, ct);
        if (rows.Count == 0)
        {
            return;
        }
        sb.AppendLine("Schwarzes Brett");
        sb.Append("— Ankündigungen (").Append(Math.Min(max, rows.Count)).Append(" von ").Append(rows.Count).AppendLine(") —");
        foreach (var a in rows.Take(max))
        {
            sb.Append("• ").Append(Fmt(a.CreatedAt)).Append(a.Important ? " [wichtig] " : " ")
                .Append(Free(a.Title)).Append(" | Zielgruppe: ").Append(a.TargetDisplay);
            if (a.MustAcknowledge)
            {
                sb.Append(" | von dir noch nicht quittiert");
            }
            sb.AppendLine();
        }
    }

    private async Task RosterAsync(StringBuilder sb, int max, ViewerScope scope, CancellationToken ct)
    {
        // the roster is the personnel list, and that page is leadership-only
        if (!scope.MayClassifiedRead)
        {
            return;
        }
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // the one named rule the personnel page uses; filtering db.Users by hand here is how the search leaked
        var rows = await db.Users.AsNoTracking().OnlyWithPersonnelFile()
            .OrderBy(u => u.Rank == null).ThenByDescending(u => u.Rank).ThenBy(u => u.Codename)
            .Select(u => new { u.Codename, u.Rank, u.Status, u.IsTRU, u.IsHRB })
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return;
        }

        sb.AppendLine("Personalbestand");
        sb.Append("Personalakten: ").Append(rows.Count)
            .Append(" | aktiv: ").Append(rows.Count(r => r.Status == AgentStatus.Active)).AppendLine();
        sb.Append("— Agenten (").Append(Math.Min(max, rows.Count)).Append(" von ").Append(rows.Count).AppendLine(") —");
        foreach (var r in rows.Take(max))
        {
            sb.Append("• ").Append(string.IsNullOrWhiteSpace(r.Codename) ? "(unbenannt)" : r.Codename)
                .Append(" | ").Append(r.Rank is { } rank ? RankDisplay.Name(rank) : "ohne Dienstgrad")
                .Append(" | ").Append(AgentStatusDisplay.Name(r.Status));
            if (r.IsTRU) { sb.Append(" | TRU"); }
            if (r.IsHRB) { sb.Append(" | HRB"); }
            sb.AppendLine();
        }
    }

    private async Task CounterIntelAsync(StringBuilder sb, int max, ClaimsPrincipal actor, CancellationToken ct)
    {
        var overview = await counterIntel.GetOverviewAsync(actor, 30, ct);
        var flags = await counterIntel.GetFlagsAsync(actor, ct);

        sb.AppendLine("Gegenaufklärung");
        sb.Append("Zeitraum: letzte ").Append(overview.WindowDays).AppendLine(" Tage");
        sb.Append("Zugriffe: ").Append(overview.TotalAccesses)
            .Append(" | Agenten: ").Append(overview.DistinctAgents)
            .Append(" | Akten: ").Append(overview.DistinctRecords)
            .Append(" | außerhalb der Dienstzeit: ").Append(overview.OffHoursAccesses).AppendLine();

        if (flags.Count == 0)
        {
            sb.AppendLine("Keine Auffälligkeiten.");
            return;
        }
        sb.Append("— Auffälligkeiten (").Append(Math.Min(max, flags.Count)).Append(" von ").Append(flags.Count).AppendLine(") —");
        foreach (var f in flags.OrderByDescending(f => f.Severity).Take(max))
        {
            sb.Append("• ").Append(f.AgentName).Append(" | ").Append(CounterIntelSeverityDisplay.Name(f.Grade))
                .Append(" | ").Append(Free(f.Rule)).Append(": ").AppendLine(Free(f.Detail));
        }
    }

    private async Task FollowupsAsync(StringBuilder sb, int max, ClaimsPrincipal actor, CancellationToken ct)
    {
        var rows = await followups.GetMyDueAsync(actor, ct);
        if (rows.Count == 0)
        {
            return;
        }
        sb.AppendLine("Eigene Wiedervorlagen");
        sb.Append("— Fällig (").Append(Math.Min(max, rows.Count)).Append(" von ").Append(rows.Count).AppendLine(") —");
        foreach (var f in rows.Take(max))
        {
            sb.Append("• ").Append(Fmt(f.DueAt)).Append(" | ").Append(Free(f.Display));
            if (Free(f.Note) is { Length: > 0 } note)
            {
                sb.Append(" — ").Append(note);
            }
            sb.AppendLine();
        }
    }

    private async Task TrainingAsync(StringBuilder sb, int max, CancellationToken ct)
    {
        var modules = await training.GetActiveAsync(ct);
        if (modules.Count == 0)
        {
            return;
        }
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var completions = await db.AgentModuleCompletions.AsNoTracking()
            .GroupBy(c => c.ModuleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct);

        sb.AppendLine("Ausbildung");
        sb.Append("— Aktive Module (").Append(Math.Min(max, modules.Count)).Append(" von ").Append(modules.Count).AppendLine(") —");
        foreach (var m in modules.Take(max))
        {
            sb.Append("• ").Append(Free(m.Name))
                .Append(" | Abschlüsse: ").Append(completions.GetValueOrDefault(m.Id));
            if (Free(m.Description) is { Length: > 0 } description)
            {
                sb.Append(" | ").Append(description);
            }
            sb.AppendLine();
        }
    }

    private static string Free(string? text) => MentionParser.Strip(text).Trim();

    private static string Fmt(DateTime when) => when.ToString("dd.MM.yyyy HH:mm");

    private static string Json(IEnumerable<string> values)
        => "[" + string.Join(",", values.Select(v => "\"" + v + "\"")) + "]";
}

using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Meetings;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Reads what hangs off a record — comments, sources, followups, links, own fields, keywords, doks,
/// observations, chat, agenda, correspondence — in depth and page by page.</summary>
/// <remarks>
/// It exists because a dossier is one budget: on a well-filled file the comments are the first thing the clip
/// drops, and there was no way to ask for them on their own.
/// <para>The parent gate runs first and is not optional. Two of the services below — keywords and own fields —
/// carry no visibility check of their own for internal agents; they rely on the page having gated the record
/// before rendering them. Here this tool is that page.</para>
/// </remarks>
public sealed class ReadRecordContentTool(
    IDbContextFactory<AppDbContext> dbFactory,
    ICommentService comments,
    ISourceService sources,
    IFollowupService followups,
    ILinkService links,
    ICustomFieldValueService customFields,
    ITagService tags,
    IPersonDocService personDocs,
    IObservationService observations,
    ITaskforceChatService taskforceChat,
    IMeetingService meetings,
    IBewerbungService applications) : INooseiTool
{
    private const string Comments = "kommentare";
    private const string Sources = "quellen";
    private const string Followups = "wiedervorlagen";
    private const string Links = "verknuepfungen";
    private const string CustomFields = "zusatzfelder";
    private const string Tags = "stichworte";
    private const string Docs = "doks";
    private const string Observations = "observationen";
    private const string Chat = "chat";
    private const string Agenda = "tagesordnung";
    private const string Messages = "nachrichten";
    private const string Everything = "alles";

    /// <summary>Sections that hang off any record through the polymorphic association, in reading order.</summary>
    private static readonly string[] Universal =
        [Comments, Sources, Followups, Links, CustomFields, Tags];

    private static readonly string[] All =
        [.. Universal, Docs, Observations, Chat, Agenda, Messages, Everything];

    public string Name => "lies_akteninhalt";

    public string Description =>
        "Liest die Inhalte einer Akte in voller Länge: Kommentare, Quellen, Wiedervorlagen, Verknüpfungen, "
        + "Zusatzfelder, Stichworte, Doks, Observationen, Taskforce-Chat, Tagesordnung und Bewerbungs-Schriftwechsel. "
        + "Nimm es, wenn lies_akte einen Abschnitt gekürzt hat oder wenn genau danach gefragt ist. "
        + "Mit „ab\" blätterst du weiter.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["typ", "id"],
          "properties": {
            "typ": { "type": "string", "enum": {{NooseiRecordTypes.EnumJson}} },
            "id": { "type": "string", "description": "Id der Akte, wie von suche_akten geliefert." },
            "inhalt": { "type": "string", "enum": {{Json(All)}},
                        "description": "Welcher Abschnitt. Standard: alles." },
            "max": { "type": "integer", "minimum": 1, "maximum": 40, "description": "Höchstzahl Einträge je Abschnitt." },
            "ab": { "type": "integer", "minimum": 0, "description": "Wie viele Einträge übersprungen werden." }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var type = NooseiRecordTypes.Clr(NooseiLimits.Text(arguments, "typ"), NooseiUse.Read);
        var id = NooseiLimits.Text(arguments, "id");
        if (type is null || id is null)
        {
            return NooseiToolResult.NotFound();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // the parent gate, and it runs before anything is loaded: two of the readers below have none of their own
        if (!await Visibility.IsRecordVisibleAsync(db, type, id, context.Scope, cancellationToken))
        {
            return NooseiToolResult.NotFound();
        }

        var wanted = NooseiLimits.Text(arguments, "inhalt") ?? Everything;
        if (!All.Contains(wanted, StringComparer.OrdinalIgnoreCase))
        {
            return new NooseiToolResult(
                "Diesen Abschnitt gibt es nicht. Möglich sind: " + string.Join(", ", All) + ".", null, true);
        }

        var applicable = Applicable(type);
        var everything = string.Equals(wanted, Everything, StringComparison.OrdinalIgnoreCase);
        // a section belonging to a different kind of record is a wrong question, not a hidden answer. Without this
        // the chat reader would run against a person id, return nothing, and read as "this taskforce is silent".
        if (!everything && !applicable.Contains(wanted, StringComparer.OrdinalIgnoreCase))
        {
            return new NooseiToolResult(
                $"„{wanted}\" gibt es bei einer Akte vom Typ {NooseiRecordTypes.German(type)} nicht. "
                + "Vorhanden sind: " + string.Join(", ", applicable) + ".", null, true);
        }
        var sections = everything ? applicable : [wanted];

        var max = NooseiLimits.Count(arguments, "max", 20);
        var offset = Offset(arguments, "ab");

        var title = await TitleAsync(db, type, id, context.Scope, cancellationToken);
        var sb = new StringBuilder();
        sb.Append(NooseiRecordTypes.German(type)).Append(": ").AppendLine(title);

        var any = false;
        foreach (var section in sections)
        {
            var (heading, rows) = await LoadAsync(db, section, type, id, context, cancellationToken);
            if (rows.Count == 0)
            {
                continue;
            }
            any = true;
            var page = rows.Skip(offset).Take(max).ToList();
            // always name the total: "20 Zeilen" read as "that is all of them" is the failure this tool exists to avoid
            sb.Append("— ").Append(heading).Append(" (").Append(page.Count).Append(" von ").Append(rows.Count);
            if (offset > 0)
            {
                sb.Append(", ab ").Append(offset);
            }
            sb.AppendLine(") —");
            foreach (var row in page)
            {
                sb.Append("• ").AppendLine(row);
            }
            if (offset + page.Count < rows.Count)
            {
                sb.Append("(… ").Append(rows.Count - offset - page.Count)
                    .Append(" weitere, weiterlesen mit ab=").Append(offset + page.Count).AppendLine(")");
            }
        }

        if (!any)
        {
            sb.AppendLine(sections.Length == 1
                ? "Zu diesem Abschnitt gibt es nichts."
                : "Zu dieser Akte sind keine Inhalte hinterlegt.");
        }

        return new NooseiToolResult(
            NooseiLimits.Clip(sb.ToString(), NooseiLimits.MaxContentResultChars),
            [new LlmContextRef(type, id, title)]);
    }

    /// <summary>The sections a record of this kind actually has.</summary>
    private static string[] Applicable(string type) => type switch
    {
        // doks and observations hang off a person and off the three kinds of organisation, nothing else
        nameof(Person) or nameof(Faction) or nameof(PersonGroup) or nameof(Party) =>
            [.. Universal, Docs, Observations],
        nameof(Taskforce) => [.. Universal, Chat],
        nameof(Meeting) => [.. Universal, Agenda],
        nameof(Bewerbung) => [.. Universal, Messages],
        _ => Universal,
    };

    private async Task<(string Heading, IReadOnlyList<string> Rows)> LoadAsync(
        AppDbContext db, string section, string type, string id, NooseiToolContext context, CancellationToken ct)
    {
        var scope = context.Scope;
        switch (section)
        {
            case Comments:
            {
                var rows = await comments.GetForRecordAsync(type, id, scope, ct);
                return ("Kommentare", rows
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => $"{Fmt(c.CreatedAt)} {Who(c.AuthorName)}: {Free(c.Text)}")
                    .ToList());
            }
            case Sources:
            {
                var rows = await sources.GetForRecordAsync(type, id, scope, ct);
                return ("Quellen", rows
                    .OrderByDescending(s => s.Pinned).ThenBy(s => s.CreatedAt)
                    .Select(s =>
                    {
                        var extra = !string.IsNullOrWhiteSpace(s.Description) ? s.Description
                            : !string.IsNullOrWhiteSpace(s.Url) ? s.Url
                            : s.OriginalName;
                        var head = $"{SourceTypeDisplay.Name(s.Type)}: "
                            + (string.IsNullOrWhiteSpace(s.Title) ? "(ohne Titel)" : Free(s.Title));
                        return Free(extra) is { Length: > 0 } detail ? head + " — " + detail : head;
                    })
                    .ToList());
            }
            case Followups:
            {
                var rows = await followups.GetForRecordAsync(type, id, context.Actor, ct);
                return ("Wiedervorlagen", rows
                    .OrderBy(f => f.DueAt)
                    .Select(f =>
                    {
                        var state = f.Done ? "erledigt" : f.Overdue ? "überfällig" : "offen";
                        var who = string.IsNullOrWhiteSpace(f.ResponsibleCodename) ? "" : $" [{f.ResponsibleCodename}]";
                        return $"{Fmt(f.DueAt)} ({state}){who}: {Free(f.Note)}";
                    })
                    .ToList());
            }
            case Links:
            {
                var rows = await links.GetForRecordAsync(type, id, scope, null, ct);
                return ("Verknüpfungen", rows
                    .Select(l =>
                    {
                        var label = string.IsNullOrWhiteSpace(l.Label) ? "Verknüpfung" : Free(l.Label);
                        return $"{label}: {NooseiRecordTypes.German(l.OtherType)} {Free(l.OtherDesignation)}";
                    })
                    .ToList());
            }
            case CustomFields:
            {
                var rows = await customFields.GetForRecordAsync(type, id, ct, scope);
                return ("Zusatzfelder", rows
                    .Where(v => !string.IsNullOrWhiteSpace(v.Value))
                    .OrderBy(v => v.Definition.Order)
                    .Select(v => $"{v.Definition.Name}: {Free(v.Value)}")
                    .ToList());
            }
            case Tags:
            {
                var rows = await tags.GetForRecordAsync(type, id, ct);
                return ("Stichworte", rows.Select(t => t.Name).ToList());
            }
            case Docs:
            {
                var rows = type == nameof(Person)
                    ? await personDocs.GetForPersonAsync(id, scope, ct)
                    : await personDocs.GetForOrgAsync(type, id, scope, ct);
                return ("Doks", rows
                    .OrderByDescending(d => d.Doc.Timestamp)
                    .Select(d =>
                    {
                        var sb = new StringBuilder(Fmt(d.Doc.Timestamp));
                        sb.Append(" | ").Append(MeasureOutcomeDisplay.Name(d.Doc.Outcome));
                        if (d.Doc.TruthSerum) { sb.Append(" | Wahrheitsserum"); }
                        if (d.Doc.MemoryDeleted) { sb.Append(" | Gedächtnis gelöscht"); }
                        if (Free(d.Doc.Reason) is { Length: > 0 } why) { sb.Append(" | Anlass: ").Append(why); }
                        if (Free(d.Doc.ReceivedInformation) is { Length: > 0 } info) { sb.Append(" | ").Append(info); }
                        return sb.ToString();
                    })
                    .ToList());
            }
            case Observations:
            {
                var rows = type == nameof(Person)
                    ? await observations.GetForPersonAsync(id, scope, ct)
                    : await observations.GetForOrgAsync(type, id, scope.MayClassifiedRead, ct);
                return ("Observationen", rows
                    .OrderByDescending(o => o.Obs.Start)
                    .Select(o =>
                    {
                        var sb = new StringBuilder(Fmt(o.Obs.Start));
                        if (o.Obs.End is { } end) { sb.Append(" – ").Append(Fmt(end)); }
                        if (Free(o.Obs.Location) is { Length: > 0 } place) { sb.Append(" | ").Append(place); }
                        if (Free(o.Obs.Sighting) is { Length: > 0 } sight) { sb.Append(" | ").Append(sight); }
                        if (Free(o.Obs.Result) is { Length: > 0 } result) { sb.Append(" | Ergebnis: ").Append(result); }
                        return sb.ToString();
                    })
                    .ToList());
            }
            case Chat:
            {
                var rows = await taskforceChat.GetMessagesAsync(id, scope, NooseiLimits.MaxRowsPerTool * 4, null, ct);
                return ("Taskforce-Chat", rows
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => $"{Fmt(m.CreatedAt)} {Who(m.AuthorName)}: {Free(m.Text)}")
                    .ToList());
            }
            case Agenda:
            {
                var rows = await meetings.GetAgendaAsync(id, scope, ct);
                return ("Tagesordnung", rows
                    .OrderBy(p => p.Sorting)
                    .Select(p =>
                    {
                        var head = (p.Done ? "[erledigt] " : "[offen] ") + Free(p.Title);
                        var note = HtmlCleanup.PlainText(p.NotesHtml);
                        return string.IsNullOrWhiteSpace(note) ? head : head + " — " + Free(note);
                    })
                    .ToList());
            }
            case Messages:
            {
                // both threads: the internal one and the correspondence with the applicant, each gated by the service
                var internals = await applications.GetMessagesAsync(id, BewerbungMessageAudience.Intern, context.Actor, ct);
                var applicant = await applications.GetMessagesAsync(id, BewerbungMessageAudience.Bewerber, context.Actor, ct);
                return ("Schriftwechsel", internals.Select(m => (m, "intern"))
                    .Concat(applicant.Select(m => (m, "mit Bewerber")))
                    .OrderBy(x => x.Item1.CreatedAt)
                    .Select(x => $"{Fmt(x.Item1.CreatedAt)} [{x.Item2}] {Who(x.Item1.AuthorName)}: {Free(x.Item1.Text)}")
                    .ToList());
            }
            default:
                return (section, []);
        }
    }

    /// <summary>Names the record the content belongs to, so the answer can cite it.</summary>
    private static async Task<string> TitleAsync(
        AppDbContext db, string type, string id, ViewerScope scope, CancellationToken ct)
    {
        var resolved = await RecordsReference.ResolveAsync(db, [(type, id)], ct,
            mayAllTaskforces: scope.MayAllTaskforces, meId: scope.MeId);
        return resolved.TryGetValue((type, id), out var row) && !string.IsNullOrWhiteSpace(row.Display)
            ? row.Display
            : NooseiRecordTypes.German(type);
    }

    private static string Who(string? author) => string.IsNullOrWhiteSpace(author) ? "(unbekannt)" : author;

    private static string Free(string? text) => MentionParser.Strip(text).Trim();

    private static string Fmt(DateTime when) => when.ToString("dd.MM.yyyy HH:mm");

    /// <summary>Reads a non-negative offset; anything else starts at the beginning.</summary>
    private static int Offset(JsonElement args, string name)
        => args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number) && number > 0
                ? number
                : 0;

    private static string Json(IEnumerable<string> values)
        => "[" + string.Join(",", values.Select(v => "\"" + v + "\"")) + "]";
}

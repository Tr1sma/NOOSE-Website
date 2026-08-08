using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Llm;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services.Llm.Tools;

namespace NOOSE_Website.Services;

/// <summary>A record NOOSEI read for an answer, as the chat renders it: type, id and the name it had then.</summary>
public sealed record NooseiSource(string Type, string Id, string Name);

/// <summary>One entry of a conversation as the chat page renders it.</summary>
/// <param name="Text">Raw text as stored — the model answers in Markdown.</param>
/// <param name="Html">Sanitized HTML of <paramref name="Text"/>; null for the agent's own turns, which are
/// shown verbatim rather than interpreted as Markdown.</param>
/// <param name="Sources">Records the tools read for this answer; empty on the agent's own turns and on
/// every answer stored before answers carried sources.</param>
public sealed record NooseiChatMessage(
    string Id, bool FromUser, string Text, string? Html, DateTime CreatedAt, bool IsError, long? QuotaTokens,
    IReadOnlyList<NooseiSource> Sources)
{
    /// <summary>The agent's own message on its way into the list, before it comes back from storage.</summary>
    public static NooseiChatMessage FromAgent(string text)
        => new(Guid.NewGuid().ToString(), true, text, null, DateTime.UtcNow, false, null, []);
}

/// <summary>A conversation in the owner's sidebar.</summary>
public sealed record NooseiConversationRow(string Id, string Title, DateTime LastMessageAt, int MessageCount);

/// <summary>Record a conversation was opened from, so follow-ups keep their subject.</summary>
public sealed record NooseiAnchor(string EntityType, string EntityId)
{
    /// <summary>Reads the <c>Typ:Id</c> form the chat page carries in its query string.</summary>
    /// <remarks>Only the shape is checked here. Whether the asker may see the record is decided later, against
    /// their live scope — a hand-typed query string is not evidence of anything.</remarks>
    public static NooseiAnchor? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }
        var cut = token.IndexOf(':');
        if (cut <= 0 || cut == token.Length - 1)
        {
            return null;
        }
        var type = NooseiRecordTypes.Clr(token[..cut].Trim()) ?? Known(token[..cut].Trim());
        var id = token[(cut + 1)..].Trim();
        return type is null || id.Length == 0 ? null : new NooseiAnchor(type, id);
    }

    /// <summary>Accepts the CLR name too, so a caller can pass <c>nameof(Person)</c> without translating.</summary>
    private static string? Known(string candidate)
        => NooseiRecordTypes.Clr(NooseiRecordTypes.German(candidate)) is { } clr ? clr : null;

    public override string ToString() => EntityType + ":" + EntityId;
}

/// <summary>What one answered turn produced.</summary>
public sealed record NooseiTurn(string ConversationId, NooseiChatMessage Answer, LlmQuotaStatus Quota, long QuotaTokens, bool Degraded);

/// <summary>NOOSEI conversations: owner-private threads with real multi-turn history and record-database tools.</summary>
public interface INooseiChatService
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<NooseiConversationRow>> GetConversationsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NooseiChatMessage>> GetMessagesAsync(string conversationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Asks a question; creates the conversation when no id is given.</summary>
    /// <param name="anchor">Record the chat was opened from. Only honoured on a new conversation, and only when
    /// the asker may see it.</param>
    Task<NooseiTurn> AskAsync(string? conversationId, string question, ClaimsPrincipal actor,
        IProgress<string>? progress = null, NooseiAnchor? anchor = null, CancellationToken cancellationToken = default);

    Task RenameAsync(string conversationId, string title, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string conversationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="INooseiChatService" />
public sealed class NooseiChatService(
    IDbContextFactory<AppDbContext> dbFactory,
    INooseiGateway noosei,
    INooseiSettingsService settings,
    NooseiToolRegistry tools,
    ILogger<NooseiChatService> logger) : INooseiChatService
{
    /// <summary>How many stored messages are replayed into a new turn.</summary>
    private const int ReplayWindow = 20;

    public bool IsConfigured => noosei.IsConfigured;

    public async Task<IReadOnlyList<NooseiConversationRow>> GetConversationsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var agentId = OwnerId(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.NooseiConversations.AsNoTracking()
            .Where(c => c.AgentId == agentId)
            .OrderByDescending(c => c.LastMessageAt)
            .Take(50)
            .Select(c => new NooseiConversationRow(c.Id, c.Title, c.LastMessageAt, c.MessageCount))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NooseiChatMessage>> GetMessagesAsync(string conversationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await LoadOwnAsync(db, conversationId, actor, cancellationToken);
        var rows = await db.NooseiMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id && (m.Role == "user" || m.Role == "assistant"))
            .OrderBy(m => m.Sequence)
            .ToListAsync(cancellationToken);
        return rows.Select(Render).ToList();
    }

    public async Task<NooseiTurn> AskAsync(
        string? conversationId, string question, ClaimsPrincipal actor,
        IProgress<string>? progress = null, NooseiAnchor? anchor = null, CancellationToken cancellationToken = default)
    {
        Permission.RequireLlmUse(actor);
        var agentId = OwnerId(actor);
        var trimmed = PromptRedactor.Clip(question, PromptRedactor.MaxChatInputChars);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Bitte eine Frage eingeben.");
        }

        var context = NooseiToolContext.From(actor);
        var stamp = ScopeStamp(context.Scope);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var conversation = conversationId is null
            ? Create(db, agentId, trimmed, stamp, await VisibleAnchorAsync(db, anchor, context.Scope, cancellationToken))
            : await LoadOwnAsync(db, conversationId, actor, cancellationToken);

        var history = conversationId is null
            ? []
            : await ReplayAsync(db, conversation, stamp, cancellationToken);

        var messages = new List<LlmMessage>(history.Count + 3)
        {
            LlmMessage.System(NooseiPrompts.Combine(NooseiPrompts.Chat, await AddendumAsync(cancellationToken))),
        };
        // re-checked every turn, not once at creation: the owner's rights may have been withdrawn since
        if (await AnchorLineAsync(db, conversation, context.Scope, cancellationToken) is { } line)
        {
            messages.Add(LlmMessage.System(line));
        }
        messages.AddRange(history);
        messages.Add(LlmMessage.User(trimmed));

        var answer = await noosei.AskAsync(
            new NooseiCall(
                LlmFeature.Chat,
                messages,
                LoggedPrompt: trimmed,
                Tools: tools.Definitions,
                ToolExecutor: (call, ct) => RunToolAsync(call, context, progress, ct),
                ConversationId: conversation.Id,
                EntityType: conversation.AnchorEntityType,
                EntityId: conversation.AnchorEntityId),
            actor,
            cancellationToken);

        var text = string.IsNullOrWhiteSpace(answer.Text) ? "(Keine Antwort erhalten.)" : answer.Text!;
        var stored = await AppendTurnAsync(db, conversation, trimmed, answer.Transcript, text,
            answer.Charge.QuotaTokens, Sources(answer.Refs), stamp, cancellationToken);

        return new NooseiTurn(conversation.Id, stored, answer.Charge.Status, answer.Charge.QuotaTokens, answer.Degraded);
    }

    public async Task RenameAsync(string conversationId, string title, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Bitte einen Titel angeben.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await LoadOwnAsync(db, conversationId, actor, cancellationToken);
        conversation.Title = Shorten(title, 200);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string conversationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await LoadOwnAsync(db, conversationId, actor, cancellationToken);
        db.NooseiConversations.Remove(conversation);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- tools ----

    private async Task<NooseiToolOutcome> RunToolAsync(LlmToolCall call, NooseiToolContext context, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var tool = tools.Find(call.Name);
        if (tool is null)
        {
            return NooseiToolOutcome.Plain($"Unbekanntes Werkzeug: {call.Name}.");
        }

        JsonElement arguments;
        try
        {
            arguments = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson).RootElement.Clone();
        }
        catch (JsonException)
        {
            return NooseiToolOutcome.Plain("Die Werkzeug-Parameter waren kein gültiges JSON. Bitte erneut versuchen.");
        }

        progress?.Report(Progress(call.Name));
        var result = await tool.InvokeAsync(arguments, context, cancellationToken);
        // reported after the call, because only the result knows which record was actually reached
        if (Touched(result.Refs) is { } record)
        {
            progress?.Report($"NOOSEI hat {record} gelesen.");
        }
        return new NooseiToolOutcome(result.Text, result.Refs);
    }

    /// <summary>Names the record a tool reached, when it reached exactly one — naming forty is noise.</summary>
    private static string? Touched(IReadOnlyList<LlmContextRef>? refs)
        => refs is { Count: 1 } && refs[0].Name is { Length: > 0 } name
            ? $"{NooseiRecordTypes.German(refs[0].Kind)} {name}"
            : null;

    private static string Progress(string toolName) => toolName switch
    {
        "suche_akten" => "NOOSEI durchsucht die Akten …",
        "finde_akten" => "NOOSEI sichtet den Bestand …",
        "hole_kennzahlen" => "NOOSEI wertet Kennzahlen aus …",
        "lies_akte" => "NOOSEI liest eine Akte …",
        "zeige_verbindungen" => "NOOSEI verfolgt Verbindungen …",
        "finde_verbindungsweg" => "NOOSEI sucht einen Verbindungsweg …",
        "lies_zeitstrahl" => "NOOSEI prüft den Verlauf …",
        "letzte_aenderungen" => "NOOSEI sieht die letzten Änderungen durch …",
        "loese_erwaehnung_auf" => "NOOSEI löst eine Erwähnung auf …",
        "hole_kurzbrief" => "NOOSEI holt einen Kurzbrief …",
        _ => "NOOSEI arbeitet …",
    };

    // ---- storage ----

    private static NooseiConversation Create(AppDbContext db, string agentId, string question, string stamp, NooseiAnchor? anchor)
    {
        var conversation = new NooseiConversation
        {
            AgentId = agentId,
            Title = Shorten(question, 80),
            LastMessageAt = DateTime.UtcNow,
            ScopeStamp = stamp,
            AnchorEntityType = anchor?.EntityType,
            AnchorEntityId = anchor?.EntityId,
        };
        db.NooseiConversations.Add(conversation);
        return conversation;
    }

    /// <summary>Drops an anchor the asker may not see, so the conversation never stores one either.</summary>
    /// <remarks>Deliberately the same predicate the system line uses, not a weaker one: storing an anchor whose
    /// line would never be emitted only invites someone to "fix" the asymmetry later, in the wrong direction.</remarks>
    private static async Task<NooseiAnchor?> VisibleAnchorAsync(
        AppDbContext db, NooseiAnchor? anchor, ViewerScope scope, CancellationToken cancellationToken)
        => anchor is not null
            && await AnchorDisplayAsync(db, anchor.EntityType, anchor.EntityId, scope, cancellationToken) is not null
                ? anchor
                : null;

    /// <summary>The name of the anchored record, or null when this viewer may not have it.</summary>
    /// <remarks>
    /// Two gates, both required. <see cref="Visibility.IsRecordVisibleAsync"/> answers <c>true</c> for a type it
    /// does not know — Taskforce among them — so it cannot be the only one; <see cref="RecordsReference"/> simply
    /// omits a record whose membership or release the viewer lacks, which closes exactly that hole. Without both,
    /// a hand-typed <c>?akte=</c> would confirm a record by naming it back, which no tool would ever do.
    /// </remarks>
    private static async Task<string?> AnchorDisplayAsync(
        AppDbContext db, string type, string id, ViewerScope scope, CancellationToken cancellationToken)
    {
        if (!await Visibility.IsRecordVisibleAsync(db, type, id, scope, cancellationToken))
        {
            return null;
        }
        var resolved = await RecordsReference.ResolveAsync(db, [(type, id)], cancellationToken,
            scope.MayAllTaskforces, scope.MeId);
        return resolved.TryGetValue((type, id), out var hit) && !string.IsNullOrWhiteSpace(hit.Display)
            ? hit.Display
            : null;
    }

    /// <summary>The system line that tells NOOSEI which record the conversation is about.</summary>
    private static async Task<string?> AnchorLineAsync(
        AppDbContext db, NooseiConversation conversation, ViewerScope scope, CancellationToken cancellationToken)
    {
        if (conversation.AnchorEntityType is not { Length: > 0 } type
            || conversation.AnchorEntityId is not { Length: > 0 } id
            || await AnchorDisplayAsync(db, type, id, scope, cancellationToken) is not { } display)
        {
            return null;
        }

        return "Diese Unterhaltung wurde aus einer Akte heraus geöffnet und bezieht sich auf: "
            + $"{NooseiRecordTypes.German(type)} {display} (id={id}). "
            + "Beziehe unklare Fragen auf diese Akte und lies sie mit lies_akte, bevor du antwortest.";
    }

    private static async Task<NooseiConversation> LoadOwnAsync(
        AppDbContext db, string conversationId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        var conversation = await db.NooseiConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException("Unterhaltung nicht gefunden.");
        Permission.RequireOwnConversation(actor, conversation.AgentId);
        return conversation;
    }

    /// <summary>Replays the recent history. Tool results are dropped when the owner's scope changed since:
    /// their text was authorised under rights that may since have been taken away.</summary>
    private static async Task<List<LlmMessage>> ReplayAsync(
        AppDbContext db, NooseiConversation conversation, string stamp, CancellationToken cancellationToken)
    {
        var sameScope = string.Equals(conversation.ScopeStamp, stamp, StringComparison.Ordinal);
        var rows = await db.NooseiMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderByDescending(m => m.Sequence)
            .Take(ReplayWindow)
            .ToListAsync(cancellationToken);
        rows.Reverse();

        var replay = new List<LlmMessage>(rows.Count);
        foreach (var row in rows)
        {
            if (row.IsError)
            {
                continue;
            }
            switch (row.Role)
            {
                case "user":
                    replay.Add(LlmMessage.User(row.Content ?? string.Empty));
                    break;
                case "assistant" when !string.IsNullOrWhiteSpace(row.Content):
                    replay.Add(LlmMessage.Assistant(row.Content));
                    break;
                case "tool" when sameScope && !string.IsNullOrWhiteSpace(row.Content):
                    // replayed as plain context, not as a tool result: the matching tool_call ids are long gone
                    replay.Add(LlmMessage.Assistant($"[Frühere Werkzeug-Antwort · {row.ToolName}]\n{row.Content}"));
                    break;
            }
        }
        return replay;
    }

    /// <summary>How many records are kept under one answer. A single filter call may touch forty; three of them
    /// would bury the answer under chips and blow up the stored row for no gain.</summary>
    private const int MaxSources = 24;

    /// <summary>Turns the refs of a turn into renderable sources: only linkable records, each once, name kept.</summary>
    /// <remarks>The per-tool entries the gateway adds carry no id and drop out here; they belong in the request
    /// log, not under an answer. A type without a real route drops out too — the person fallback in
    /// <see cref="SearchNavigation.Route"/> would send the reader into the wrong record.</remarks>
    private static List<NooseiSource> Sources(IReadOnlyList<LlmContextRef>? refs)
    {
        if (refs is not { Count: > 0 })
        {
            return [];
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sources = new List<NooseiSource>();
        foreach (var reference in refs)
        {
            if (!SearchNavigation.Knows(reference.Kind))
            {
                continue;
            }
            if (reference.Id is not { Length: > 0 } id || reference.Name is not { Length: > 0 } name)
            {
                continue;
            }
            if (!seen.Add(reference.Kind + "|" + id))
            {
                continue;
            }
            sources.Add(new NooseiSource(reference.Kind, id, name));
            if (sources.Count == MaxSources)
            {
                break;
            }
        }
        return sources;
    }

    private static async Task<NooseiChatMessage> AppendTurnAsync(
        AppDbContext db, NooseiConversation conversation, string question, IReadOnlyList<LlmMessage> transcript,
        string answer, long quotaTokens, IReadOnlyList<NooseiSource> sources, string stamp, CancellationToken cancellationToken)
    {
        var next = await db.NooseiMessages
            .Where(m => m.ConversationId == conversation.Id)
            .Select(m => (int?)m.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var now = DateTime.UtcNow;
        db.NooseiMessages.Add(new NooseiMessage
        {
            ConversationId = conversation.Id,
            Sequence = ++next,
            Role = "user",
            Content = question,
            CreatedAt = now,
        });

        // keep the tool results of this turn, so a follow-up does not have to fetch them again
        foreach (var toolMessage in transcript.Where(m => m.Role == LlmRole.Tool))
        {
            db.NooseiMessages.Add(new NooseiMessage
            {
                ConversationId = conversation.Id,
                Sequence = ++next,
                Role = "tool",
                ToolName = toolMessage.Name,
                ToolCallId = toolMessage.ToolCallId,
                Content = toolMessage.Content,
                CreatedAt = now,
            });
        }

        var stored = new NooseiMessage
        {
            ConversationId = conversation.Id,
            Sequence = ++next,
            Role = "assistant",
            Content = answer,
            QuotaTokens = quotaTokens,
            SourcesJson = sources.Count > 0 ? JsonSerializer.Serialize(sources) : null,
            CreatedAt = now,
        };
        db.NooseiMessages.Add(stored);

        conversation.LastMessageAt = now;
        conversation.MessageCount = next;
        conversation.ScopeStamp = stamp;
        await db.SaveChangesAsync(cancellationToken);

        return Render(stored);
    }

    // ---- helpers ----

    private async Task<string?> AddendumAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await settings.GetAddendumAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NOOSEI-Zusatzhinweis konnte nicht gelesen werden.");
            return null;
        }
    }

    private static NooseiChatMessage Render(NooseiMessage row)
    {
        var fromUser = row.Role == "user";
        var text = row.Content ?? string.Empty;
        // the model answers in Markdown; MarkdownRenderer drops raw HTML and sanitizes what it produces
        var html = fromUser || string.IsNullOrWhiteSpace(text) ? null : MarkdownRenderer.ToSafeHtml(text);
        return new NooseiChatMessage(row.Id, fromUser, text, html, row.CreatedAt, row.IsError, row.QuotaTokens,
            ReadSources(row.SourcesJson));
    }

    /// <summary>Answers stored before sources existed, and a row corrupted by hand, both render without chips.</summary>
    private static IReadOnlyList<NooseiSource> ReadSources(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<NooseiSource>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string OwnerId(ClaimsPrincipal actor)
        => actor.GetAgentId() ?? throw new UnauthorizedAccessException("NOOSEI steht in dieser Rolle nicht zur Verfügung.");

    /// <summary>Fingerprint of everything that decides what a tool may return for this agent.</summary>
    private static string ScopeStamp(ViewerScope scope)
        => $"{scope.MayClassifiedRead}|{scope.MayAllTaskforces}|{scope.IsTru}|{scope.IsHrb}|{scope.PartnerAgency}";

    private static string Shorten(string text, int max)
    {
        var clean = text.Trim().ReplaceLineEndings(" ");
        return clean.Length <= max ? clean : clean[..max].TrimEnd() + "…";
    }
}

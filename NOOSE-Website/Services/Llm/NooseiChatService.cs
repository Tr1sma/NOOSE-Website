using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Llm;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services.Llm.Tools;

namespace NOOSE_Website.Services;

/// <summary>One entry of a conversation as the chat page renders it.</summary>
public sealed record NooseiChatMessage(string Id, bool FromUser, string Text, DateTime CreatedAt, bool IsError, long? QuotaTokens);

/// <summary>A conversation in the owner's sidebar.</summary>
public sealed record NooseiConversationRow(string Id, string Title, DateTime LastMessageAt, int MessageCount);

/// <summary>What one answered turn produced.</summary>
public sealed record NooseiTurn(string ConversationId, NooseiChatMessage Answer, LlmQuotaStatus Quota, long QuotaTokens, bool Degraded);

/// <summary>NOOSEI conversations: owner-private threads with real multi-turn history and record-database tools.</summary>
public interface INooseiChatService
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<NooseiConversationRow>> GetConversationsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NooseiChatMessage>> GetMessagesAsync(string conversationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Asks a question; creates the conversation when no id is given.</summary>
    Task<NooseiTurn> AskAsync(string? conversationId, string question, ClaimsPrincipal actor,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default);

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
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
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
            ? Create(db, agentId, trimmed, stamp)
            : await LoadOwnAsync(db, conversationId, actor, cancellationToken);

        var history = conversationId is null
            ? []
            : await ReplayAsync(db, conversation, stamp, cancellationToken);

        var messages = new List<LlmMessage>(history.Count + 2)
        {
            LlmMessage.System(NooseiPrompts.Combine(NooseiPrompts.Chat, await AddendumAsync(cancellationToken))),
        };
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
                EntityId: conversation.AnchorEntityId,
                Progress: progress),
            actor,
            cancellationToken);

        var text = string.IsNullOrWhiteSpace(answer.Text) ? "(Keine Antwort erhalten.)" : answer.Text!;
        var stored = await AppendTurnAsync(db, conversation, trimmed, answer.Transcript, text, answer.Charge.QuotaTokens, stamp, cancellationToken);

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

    private async Task<string> RunToolAsync(LlmToolCall call, NooseiToolContext context, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var tool = tools.Find(call.Name);
        if (tool is null)
        {
            return $"Unbekanntes Werkzeug: {call.Name}.";
        }

        JsonElement arguments;
        try
        {
            arguments = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson).RootElement.Clone();
        }
        catch (JsonException)
        {
            return "Die Werkzeug-Parameter waren kein gültiges JSON. Bitte erneut versuchen.";
        }

        progress?.Report(Progress(call.Name));
        var result = await tool.InvokeAsync(arguments, context, cancellationToken);
        return result.Text;
    }

    private static string Progress(string toolName) => toolName switch
    {
        "suche_akten" => "NOOSEI durchsucht die Akten …",
        "lies_akte" => "NOOSEI liest eine Akte …",
        "zeige_verbindungen" => "NOOSEI verfolgt Verbindungen …",
        "lies_zeitstrahl" => "NOOSEI prüft den Verlauf …",
        "letzte_aenderungen" => "NOOSEI sieht die letzten Änderungen durch …",
        "hole_kurzbrief" => "NOOSEI holt einen Kurzbrief …",
        _ => "NOOSEI arbeitet …",
    };

    // ---- storage ----

    private static NooseiConversation Create(AppDbContext db, string agentId, string question, string stamp)
    {
        var conversation = new NooseiConversation
        {
            AgentId = agentId,
            Title = Shorten(question, 80),
            LastMessageAt = DateTime.UtcNow,
            ScopeStamp = stamp,
        };
        db.NooseiConversations.Add(conversation);
        return conversation;
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

    private static async Task<NooseiChatMessage> AppendTurnAsync(
        AppDbContext db, NooseiConversation conversation, string question, IReadOnlyList<LlmMessage> transcript,
        string answer, long quotaTokens, string stamp, CancellationToken cancellationToken)
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
        => new(row.Id, row.Role == "user", row.Content ?? string.Empty, row.CreatedAt, row.IsError, row.QuotaTokens);

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

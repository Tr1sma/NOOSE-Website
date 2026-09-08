using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Llm;

namespace NOOSE_Website.Services.Public;

/// <summary>NOOSEI drafts an answer to a citizen; the agent edits it and sends it themselves.</summary>
public interface ITicketAssistService
{
    bool IsAvailable { get; }

    /// <summary>Draft for the citizen thread of one ticket, from the conversation so far.</summary>
    Task<TicketDraftSuggestion> SuggestReplyAsync(string ticketId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ITicketAssistService" />
/// <remarks>
/// One way to the model, like every other feature: <c>INooseiGateway.AskAsync</c> checks the right and the weekly
/// quota, books once and logs the call. Nothing here is sent — the draft lands in the composer, and
/// <c>ReplyToCitizenAsync</c> stays the only path to the citizen.
/// <para>
/// The answer is scrubbed of mention tokens before it reaches the field: the citizen thread refuses them at the
/// service, so a model that emitted one would hand the agent a message that cannot be sent.
/// </para>
/// </remarks>
public sealed class TicketAssistService(
    IDbContextFactory<AppDbContext> dbFactory,
    INooseiGateway noosei,
    INooseiSettingsService settings) : ITicketAssistService
{
    /// <summary>Lines of the conversation handed to the model; older ones are dropped, not summarised.</summary>
    private const int MaxLines = 20;

    private const int MaxCharsPerLine = 1_500;

    public bool IsAvailable => noosei.IsConfigured;

    public async Task<TicketDraftSuggestion> SuggestReplyAsync(string ticketId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        // the desk answers the citizen, so the desk drafts the answer; write guard runs inside it, before the rank
        Permission.RequireTicketHandling(actor);
        Permission.RequireLlmUse(actor);
        if (!noosei.IsConfigured)
        {
            throw new InvalidOperationException("NOOSEI ist nicht konfiguriert.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ticket = await db.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new { t.Id, t.CaseNumber, t.Subject, t.Kind, t.Category })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Ticket nicht gefunden.");
        if (ticket.Kind == TicketArt.Intern)
        {
            throw new InvalidOperationException("Ein internes Ticket hat keinen Bürger-Schriftwechsel.");
        }
        if (!await TicketVisibility.MayReadAsync(db, ticketId, actor, cancellationToken))
        {
            throw new UnauthorizedAccessException("Du bist an diesem Ticket nicht beteiligt.");
        }

        var lines = await db.TicketNachrichten.AsNoTracking()
            .Where(m => m.TicketId == ticketId && m.Audience == TicketMessageAudience.Buerger)
            // the id breaks the tie: the opening message and the automatic confirmation share one SaveChanges'
            // timestamp, and without it the two could swap places between calls
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(MaxLines)
            .Select(m => new { m.Text, m.AuthorIsCitizen, m.CreatedAt, m.Id })
            .ToListAsync(cancellationToken);
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("In diesem Ticket steht noch keine Nachricht.");
        }

        var prompt = new StringBuilder();
        prompt.AppendLine($"Betreff: {ticket.Subject}");
        prompt.AppendLine($"Kategorie: {TicketKategorieDisplay.Name(ticket.Category)}");
        prompt.AppendLine();
        prompt.AppendLine("Schriftwechsel, älteste Nachricht zuerst:");
        foreach (var line in lines.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id))
        {
            // the sender's role only; the agency rows carry no agent, so there is nothing else to say
            prompt.AppendLine(line.AuthorIsCitizen ? "Bürger:" : "Behörde:");
            prompt.AppendLine(PromptRedactor.Clip(line.Text, MaxCharsPerLine));
            prompt.AppendLine();
        }
        prompt.AppendLine("Entwirf die nächste Antwort der Behörde.");

        var body = prompt.ToString();
        var answer = await noosei.AskAsync(
            new NooseiCall(
                // no feature of its own: this is composing, and a new value would need its own token rules
                LlmFeature.Compose,
                [
                    LlmMessage.System(NooseiPrompts.Combine(NooseiPrompts.TicketReply,
                        await AddendumAsync(cancellationToken))),
                    LlmMessage.User(body),
                ],
                LoggedPrompt: body,
                Temperature: 0.3,
                MaxTokens: 1_200,
                EntityType: nameof(Data.Entities.Public.Ticket),
                EntityId: ticket.Id),
            actor,
            cancellationToken);

        var draft = Scrub(answer.Text);
        if (string.IsNullOrWhiteSpace(draft))
        {
            throw new InvalidOperationException("NOOSEI hat keinen verwertbaren Entwurf geliefert.");
        }
        return new TicketDraftSuggestion(draft, answer.Charge.QuotaTokens, answer.Charge.Status);
    }

    /// <summary>Strips what the citizen thread would refuse and clips to one message.</summary>
    private static string Scrub(string? text)
    {
        var plain = MentionParser.Strip(text).Trim();
        return plain.Length > TicketRules.MaxMessageLength
            ? plain[..TicketRules.MaxMessageLength].TrimEnd()
            : plain;
    }

    private async Task<string?> AddendumAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await settings.GetAddendumAsync(cancellationToken);
        }
        catch (Exception)
        {
            // a missing house rule must never block a draft
            return null;
        }
    }
}

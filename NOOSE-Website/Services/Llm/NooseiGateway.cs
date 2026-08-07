using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using NOOSE_Website.Authorization;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>Executes one tool call the model asked for and returns the German plain-text result.</summary>
public delegate Task<string> NooseiToolExecutor(LlmToolCall call, CancellationToken cancellationToken);

/// <summary>One user-visible NOOSEI request. May cost several rounds; it is billed exactly once.</summary>
public sealed record NooseiCall(
    LlmFeature Feature,
    IReadOnlyList<LlmMessage> Messages,
    string? LoggedPrompt = null,
    LlmResponseFormat? ResponseFormat = null,
    IReadOnlyList<LlmToolDefinition>? Tools = null,
    NooseiToolExecutor? ToolExecutor = null,
    double Temperature = 0.3,
    int? MaxTokens = null,
    string? EntityType = null,
    string? EntityId = null,
    string? ConversationId = null,
    IReadOnlyList<LlmContextRef>? ContextRefs = null,
    bool RequireCapableProviders = false,
    IProgress<string>? Progress = null);

/// <summary>What one request produced, plus what it cost the agent's weekly quota.</summary>
public sealed record NooseiAnswer(
    string? Text,
    LlmUsage Usage,
    LlmQuotaCharge Charge,
    int Rounds,
    IReadOnlyList<LlmMessage> Transcript,
    bool Degraded);

/// <summary>The only way to reach the model. Enforces use permission, the weekly quota and the request log,
/// so a new feature cannot get free tokens by calling the transport directly.</summary>
public interface INooseiGateway
{
    bool IsConfigured { get; }

    Task<NooseiAnswer> AskAsync(NooseiCall call, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="INooseiGateway" />
public class NooseiGateway(
    ILlmService llm,
    ILlmQuotaService quota,
    IOptions<LlmOptions> options,
    ILogger<NooseiGateway> logger) : INooseiGateway
{
    private readonly LlmOptions _o = options.Value;

    public bool IsConfigured => llm.IsConfigured;

    public async Task<NooseiAnswer> AskAsync(NooseiCall call, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var status = await quota.EnsureAvailableAsync(actor, cancellationToken);
        var agentId = actor.GetAgentId() ?? throw new UnauthorizedAccessException("NOOSEI steht in dieser Rolle nicht zur Verfügung.");

        // the whole turn gets one budget; HttpClient.Timeout only bounds a single round
        using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        turnCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _o.TurnTimeoutSeconds)));

        var watch = Stopwatch.StartNew();
        var messages = new List<LlmMessage>(call.Messages);
        var refs = new List<LlmContextRef>(call.ContextRefs ?? []);
        var total = LlmUsage.Empty;
        var rounds = 0;
        var degraded = false;
        LlmResult? last = null;

        try
        {
            while (true)
            {
                rounds++;
                var offerTools = OffersTools(call, rounds, total);
                LlmResult result;
                try
                {
                    result = await llm.CompleteAsync(Round(call, messages, offerTools), actor, turnCts.Token);
                }
                catch (LlmCapabilityException ex) when (ex.ToolsRelated && offerTools)
                {
                    // the endpoint cannot do tools at all — answer without file access rather than failing
                    logger.LogWarning(ex, "NOOSEI: Endpunkt unterstützt keine Werkzeuge, Anfrage ohne Aktenzugriff wiederholt.");
                    degraded = true;
                    result = await llm.CompleteAsync(Round(call, messages, offerTools: false), actor, turnCts.Token);
                }

                total += result.Usage;
                last = result;

                if (!result.HasToolCalls || !offerTools || call.ToolExecutor is null)
                {
                    break;
                }

                messages.Add(LlmMessage.Assistant(result.Text, result.ToolCalls));
                foreach (var toolCall in result.ToolCalls.Take(Math.Max(1, _o.MaxToolCallsPerRound)))
                {
                    call.Progress?.Report($"NOOSEI nutzt {toolCall.Name} …");
                    messages.Add(LlmMessage.Tool(toolCall.Id, toolCall.Name,
                        await RunToolAsync(call.ToolExecutor, toolCall, turnCts.Token)));
                    refs.Add(new LlmContextRef("tool", null, toolCall.Name));
                }
                foreach (var dropped in result.ToolCalls.Skip(Math.Max(1, _o.MaxToolCallsPerRound)))
                {
                    messages.Add(LlmMessage.Tool(dropped.Id, dropped.Name,
                        "Zu viele Werkzeugaufrufe in einer Runde. Bitte auf das Wesentliche beschränken."));
                }
            }

            watch.Stop();
            var charge = await ChargeAsync(agentId, call, total, last, refs, rounds, watch, success: true, error: null);
            return new NooseiAnswer(last?.Text, total, charge, rounds, messages, degraded);
        }
        catch (Exception ex)
        {
            watch.Stop();
            // a failed call still gets a log row: it may have cost real money, and it must show up in the overview
            var charge = await ChargeAsync(agentId, call, total, last, refs, rounds, watch, success: false, error: ex.Message);
            logger.LogWarning(ex, "NOOSEI-Anfrage fehlgeschlagen (Funktion {Feature}, {Rounds} Runden, Kontingent {Tokens})",
                call.Feature, rounds, charge.QuotaTokens);
            throw;
        }
        finally
        {
            _ = status;
        }
    }

    private bool OffersTools(NooseiCall call, int round, LlmUsage spent)
        => _o.ToolsEnabled
            && call.Tools is { Count: > 0 }
            && call.ToolExecutor is not null
            && round <= Math.Clamp(_o.MaxToolRounds, 1, 10)
            && spent.CostUsd < _o.MaxCostPerTurnUsd
            && spent.TotalTokens < _o.MaxTokensPerTurn;

    private LlmRequest Round(NooseiCall call, IReadOnlyList<LlmMessage> messages, bool offerTools) => new(
        messages,
        new LlmCallContext(call.Feature, call.ConversationId, call.EntityType, call.EntityId),
        offerTools ? call.Tools : null,
        call.ResponseFormat,
        call.Temperature,
        call.MaxTokens,
        offerTools ? LlmToolChoice.Auto : LlmToolChoice.None,
        call.RequireCapableProviders);

    private async Task<string> RunToolAsync(NooseiToolExecutor executor, LlmToolCall call, CancellationToken turnToken)
    {
        using var toolCts = CancellationTokenSource.CreateLinkedTokenSource(turnToken);
        toolCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _o.ToolTimeoutSeconds)));
        try
        {
            return await executor(call, toolCts.Token);
        }
        // a dead tool must not kill the turn: hand the model a German error it can recover from
        catch (OperationCanceledException) when (!turnToken.IsCancellationRequested)
        {
            return "Werkzeug hat nicht rechtzeitig geantwortet.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NOOSEI-Werkzeug {Tool} fehlgeschlagen", call.Name);
            return "Werkzeug konnte nicht ausgeführt werden.";
        }
    }

    private async Task<LlmQuotaCharge> ChargeAsync(
        string agentId, NooseiCall call, LlmUsage total, LlmResult? last, IReadOnlyList<LlmContextRef> refs,
        int rounds, Stopwatch watch, bool success, string? error)
    {
        var input = new LlmChargeInput(
            agentId,
            call.Feature,
            total,
            last?.Model ?? _o.Model,
            last?.Provider,
            (int)Math.Min(watch.ElapsedMilliseconds, int.MaxValue),
            Math.Max(0, rounds - 1),
            success,
            error,
            call.LoggedPrompt,
            success ? last?.Text : null,
            refs);

        // charged with None on purpose: a circuit torn down mid-answer must not skip the meter
        return await quota.TryChargeAsync(input, CancellationToken.None);
    }
}

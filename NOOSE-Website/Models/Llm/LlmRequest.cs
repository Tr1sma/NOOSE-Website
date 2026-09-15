using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Llm;

/// <summary>Where a call came from and where it goes; travels to the usage sink so a request log row can be
/// attributed.</summary>
/// <param name="Provider">Upstream this round takes, resolved once per turn by the gateway. Here and not on
/// <see cref="LlmRequest" /> because the charge needs it too, and because a model id is only meaningful together
/// with the endpoint it was addressed to.</param>
public sealed record LlmCallContext(
    LlmFeature Feature,
    string? ConversationId = null,
    string? EntityType = null,
    string? EntityId = null,
    int Round = 1,
    LlmProvider Provider = LlmProvider.OpenRouter);

/// <summary>One round: exactly one HTTP call to the endpoint.</summary>
public sealed record LlmRequest(
    IReadOnlyList<LlmMessage> Messages,
    LlmCallContext Context,
    IReadOnlyList<LlmToolDefinition>? Tools = null,
    LlmResponseFormat? ResponseFormat = null,
    double Temperature = 0.3,
    int? MaxTokens = null,
    LlmToolChoice ToolChoice = LlmToolChoice.Auto,
    bool RequireCapableProviders = false)
{
    public bool OffersTools => Tools is { Count: > 0 } && ToolChoice != LlmToolChoice.None;
}

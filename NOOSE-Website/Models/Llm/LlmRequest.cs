using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Llm;

/// <summary>Where a call came from; travels to the usage sink so a request log row can be attributed.</summary>
public sealed record LlmCallContext(
    LlmFeature Feature,
    string? ConversationId = null,
    string? EntityType = null,
    string? EntityId = null,
    int Round = 1);

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

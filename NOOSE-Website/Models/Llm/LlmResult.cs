namespace NOOSE_Website.Models.Llm;

/// <summary>Answer of one round, including what it cost. Text is returned raw — an empty answer stays empty.</summary>
public sealed record LlmResult(
    string? Text,
    IReadOnlyList<LlmToolCall> ToolCalls,
    LlmUsage Usage,
    string? Provider,
    string? Model,
    string? FinishReason,
    string? GenerationId,
    int Attempts,
    long ElapsedMs)
{
    public bool HasToolCalls => ToolCalls.Count > 0;
}

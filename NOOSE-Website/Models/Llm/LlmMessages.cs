namespace NOOSE_Website.Models.Llm;

public enum LlmRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3,
}

/// <summary>One tool invocation the model asked for.</summary>
public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>One entry of the wire conversation; Tool rows carry the result of a prior tool call.</summary>
public sealed record LlmMessage(
    LlmRole Role,
    string? Content,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? Name = null)
{
    public static LlmMessage System(string text) => new(LlmRole.System, text);

    public static LlmMessage User(string text) => new(LlmRole.User, text);

    public static LlmMessage Assistant(string? text, IReadOnlyList<LlmToolCall>? calls = null)
        => new(LlmRole.Assistant, text, calls);

    /// <summary>Result handed back to the model for one of its tool calls.</summary>
    public static LlmMessage Tool(string callId, string name, string result)
        => new(LlmRole.Tool, result, null, callId, name);
}

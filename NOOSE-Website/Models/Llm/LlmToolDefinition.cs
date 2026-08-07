using System.Text.Json;

namespace NOOSE_Website.Models.Llm;

/// <summary>A function the model may call. The schema is parsed once at startup and reused.</summary>
public sealed record LlmToolDefinition(string Name, string Description, JsonElement ParameterSchema);

public enum LlmToolChoice
{
    Auto = 0,
    None = 1,
    Required = 2,
}

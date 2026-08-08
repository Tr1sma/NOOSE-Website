using System.Text.Json;

namespace NOOSE_Website.Models.Llm;

public enum LlmResponseFormatKind
{
    /// <summary>Provider-enforced JSON schema (structured outputs).</summary>
    JsonSchema = 0,

    /// <summary>Plain JSON mode; the shape is only described in the prompt.</summary>
    JsonObject = 1,
}

/// <summary>Shape the answer must satisfy. Null on a request means free text.</summary>
public sealed record LlmResponseFormat(LlmResponseFormatKind Kind, string Name, JsonElement Schema, bool Strict)
{
    public static LlmResponseFormat ForSchema(string name, JsonElement schema, bool strict = true)
        => new(LlmResponseFormatKind.JsonSchema, name, schema, strict);

    /// <summary>Last rung of the fallback ladder: JSON mode with the schema pasted into the prompt.</summary>
    public static LlmResponseFormat JsonObject { get; } =
        new(LlmResponseFormatKind.JsonObject, "json_object", default, false);
}

namespace NOOSE_Website.Services;

/// <summary>The endpoint cannot serve this request shape (schema or tools). Permanent for this shape, so it must
/// bypass the transient retry loop; the caller downgrades and reissues once instead.</summary>
public sealed class LlmCapabilityException(bool schemaRelated, bool toolsRelated, string? detail = null)
    : InvalidOperationException(detail ?? "Der KI-Endpunkt unterstützt diese Anfrageform nicht.")
{
    public bool SchemaRelated { get; } = schemaRelated;

    public bool ToolsRelated { get; } = toolsRelated;
}

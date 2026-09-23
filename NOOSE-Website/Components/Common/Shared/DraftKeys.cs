namespace NOOSE_Website.Components.Common.Shared;

/// <summary>Browser draft keys of the plain text fields; the field and the caller that drops them build the same one.</summary>
/// <remarks>
/// A dialog closes before its caller saves, so both sides have to agree on the key without talking to each other.
/// Every field of a form hangs under one scope, and the caller drops the whole scope once the save went through.
/// </remarks>
public static class DraftKeys
{
    // the rich text editor stores "{agent}:{key}"; this segment keeps the two kinds from ever meeting
    private const string Kind = "text";

    /// <summary>A field's key inside its form's scope; null while the form has no scope.</summary>
    public static string? In(string? scope, string field)
        => string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(field) ? null : $"{scope}:{field}";

    /// <summary>The stored key of one field; null without an agent or a key, which switches the draft off.</summary>
    public static string? Field(string? agentId, string? draftKey)
        => string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(draftKey) ? null : $"{agentId}:{Kind}:{draftKey}";

    /// <summary>The prefix covering every field of a scope.</summary>
    /// <remarks>Ends on the separator: without it "dok:1" would also drop the drafts of "dok:12".</remarks>
    public static string? Scope(string? agentId, string? scope)
        => string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(scope) ? null : $"{agentId}:{Kind}:{scope}:";
}

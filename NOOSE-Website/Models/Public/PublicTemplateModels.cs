using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>One template as the settings panel and the message pickers see it.</summary>
/// <remarks>Inward: the rendered message goes outside, the raw template with its tokens never does.</remarks>
public sealed record PublicTemplateRow(
    string Id,
    PublicTemplateKind Kind,
    string Title,
    string Text,
    bool IsActive,
    int SortOrder);

/// <summary>What the editor dialog sends back; a null Id creates.</summary>
public sealed record PublicTemplateInput(
    string? Id,
    PublicTemplateKind Kind,
    string Title,
    string Text,
    bool IsActive,
    int SortOrder);

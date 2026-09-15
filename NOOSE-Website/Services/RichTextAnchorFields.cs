using NOOSE_Website.Data.Entities.Handbook;

namespace NOOSE_Website.Services;

/// <summary>Rich-text columns whose headings are given anchors on save.</summary>
/// <remarks>
/// A superset of <see cref="RichTextImageFields"/>, and deliberately a separate list. An anchor is only an id
/// inside the text, so it carries none of the delivery and visibility concerns that keep the image list short —
/// tying the two together meant the table of contents worked in exactly the six carriers that happened to store
/// pictures, and pointed nowhere everywhere else. Long-form carriers belong here; a field without headings
/// costs one parse and comes back unchanged.
/// </remarks>
public static class RichTextAnchorFields
{
    /// <summary>Carriers that take anchors but keep their images inline.</summary>
    private static readonly Dictionary<Type, string[]> Extra = new()
    {
        [typeof(HandbookArticle)] = ["ContentHtml", "RoleplayHtml"],
        [typeof(GlossaryTerm)] = ["ExplanationHtml"],
    };

    /// <summary>The registered carrier types, for the invariants that hold the table against the model.</summary>
    public static IReadOnlyCollection<Type> Types => [.. RichTextImageFields.Types, .. Extra.Keys];

    /// <summary>Rich-text property names of that entity; empty when it takes no anchors.</summary>
    public static IReadOnlyList<string> For(object entity) => For(entity.GetType());

    /// <summary>Rich-text property names of that type; empty when it takes no anchors.</summary>
    public static IReadOnlyList<string> For(Type type)
    {
        var images = RichTextImageFields.For(type);
        if (!Extra.TryGetValue(type, out var extra))
        {
            return images;
        }
        return images.Count == 0 ? extra : [.. images, .. extra];
    }
}

using NOOSE_Website.Data.Entities.Public;

namespace NOOSE_Website.Models.Public;

/// <summary>One value-list row plus how many notices carry it; the delete dialog states the number.</summary>
public sealed record WarnhinweisUsage(Warnhinweis Hinweis, int Count);

/// <summary>One selectable warning in the editor's picker.</summary>
public sealed record WarnhinweisOption(string Id, string Name, string? Colour);

/// <summary>What the admin dialog hands back; a class so the form can two-way bind.</summary>
public class WarnhinweisInput
{
    public string Name { get; set; } = string.Empty;
    public string? Colour { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

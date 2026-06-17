namespace NOOSE_Website.Models.Abstractions;

/// <summary>Marks an entity as soft-deletable; hidden by a global query filter, restorable by leadership.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedById { get; set; }
}

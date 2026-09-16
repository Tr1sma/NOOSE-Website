namespace NOOSE_Website.Models.Abstractions;

/// <summary>Marks a record as archivable; filtered out of stock listings, still readable everywhere else.</summary>
public interface IArchivable
{
    bool IsArchived { get; set; }
    DateTime? ArchivedAt { get; set; }
    string? ArchivedById { get; set; }
    string? ArchiveReason { get; set; }
}

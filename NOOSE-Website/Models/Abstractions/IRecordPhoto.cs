namespace NOOSE_Website.Models.Abstractions;

/// <summary>Marks a gallery photo that can carry its record's profile picture mark; at most one per record.</summary>
public interface IRecordPhoto
{
    string Id { get; set; }
    bool IsTitleImage { get; set; }
}

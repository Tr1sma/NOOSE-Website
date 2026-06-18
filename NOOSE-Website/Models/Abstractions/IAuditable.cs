namespace NOOSE_Website.Models.Abstractions;

/// <summary>Audit metadata stamped automatically by the audit interceptor.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    string? CreatedById { get; set; }
    DateTime? ModifiedAt { get; set; }
    string? ModifiedById { get; set; }
}

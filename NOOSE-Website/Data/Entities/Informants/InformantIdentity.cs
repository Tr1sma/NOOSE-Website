using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Informants;

/// <summary>The real identity behind an informant. DELIBERATELY NOT IAuditable and NOT ISoftDelete —
/// an audit row would expose the real name via ChangesJson to anyone who can read the protocol.</summary>
[Table("InformantIdentitaeten")]
public class InformantIdentity
{
    /// <summary>Shared 1:1 key with <see cref="Informant.Id"/>.</summary>
    public string InformantId { get; set; } = string.Empty;

    [Column("Klarname")]
    public string RealName { get; set; } = string.Empty;

    [Column("Kontakt")]
    public string? ContactInfo { get; set; }

    [Column("Notizen")]
    public string? Notes { get; set; }
}

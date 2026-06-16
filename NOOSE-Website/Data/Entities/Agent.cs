using Microsoft.AspNetCore.Identity;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities;

/// <summary>NOOSE user account; Discord OAuth only, no password.</summary>
public class Agent : IdentityUser
{
    /// <summary>Codename; visible to all.</summary>
    public string Codename { get; set; } = string.Empty;

    /// <summary>Real name; supervisory+ only.</summary>
    [Column("Klarname")]
    public string? RealName { get; set; }

    /// <summary>Badge number; public.</summary>
    [Column("Dienstnummer")]
    public string? BadgeNumber { get; set; }

    /// <summary>Discord user ID.</summary>
    public string DiscordId { get; set; } = string.Empty;

    /// <summary>Discord username.</summary>
    public string? DiscordUsername { get; set; }

    /// <summary>Avatar URL.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>NOOSE rank.</summary>
    [Column("Dienstgrad")]
    public Rank? Rank { get; set; }

    /// <summary>TRU member flag.</summary>
    [Column("IstTRU")]
    public bool IsTRU { get; set; }

    /// <summary>HRB member flag.</summary>
    [Column("IstHRB")]
    public bool IsHRB { get; set; }

    /// <summary>Admin flag.</summary>
    [Column("IstAdmin")]
    public bool IsAdmin { get; set; }

    /// <summary>Team lead flag; visibility marker only.</summary>
    [Column("IstTeamLeitung")]
    public bool IsTeamLead { get; set; }

    /// <summary>Account lifecycle status.</summary>
    public AgentStatus Status { get; set; } = AgentStatus.Pending;

    [Column("RegistriertAm")]
    public DateTime RegisteredAt { get; set; }
    [Column("FreigegebenAm")]
    public DateTime? ReleasedAt { get; set; }
    [Column("FreigegebenVonId")]
    public string? ReleasedById { get; set; }

    /// <summary>Block reason.</summary>
    [Column("GesperrtGrund")]
    public string? BlockedReason { get; set; }

    /// <summary>Last Discord roles sync.</summary>
    [Column("DiscordRollenSyncAm")]
    public DateTime? DiscordRolesSyncAt { get; set; }

    // --- pending name change ---

    /// <summary>Pending codename.</summary>
    [Column("AusstehenderCodename")]
    public string? PendingCodename { get; set; }

    /// <summary>Pending real name.</summary>
    [Column("AusstehenderKlarname")]
    public string? PendingRealName { get; set; }

    /// <summary>Pending badge number.</summary>
    [Column("AusstehendeDienstnummer")]
    public string? PendingBadgeNumber { get; set; }

    /// <summary>Name change requested at.</summary>
    [Column("NamensaenderungBeantragtAm")]
    public DateTime? NameChangeRequestedAt { get; set; }
}

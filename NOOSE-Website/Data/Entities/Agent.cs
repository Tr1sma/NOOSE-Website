using Microsoft.AspNetCore.Identity;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities;

/// <summary>NOOSE user account; Discord OAuth only, no password.</summary>
public class Agent : IdentityUser
{
    public string Codename { get; set; } = string.Empty;

    [Column("Klarname")]
    public string? RealName { get; set; }

    [Column("Dienstnummer")]
    public string? BadgeNumber { get; set; }

    public string DiscordId { get; set; } = string.Empty;

    public string? DiscordUsername { get; set; }

    public string? AvatarUrl { get; set; }

    [Column("Dienstgrad")]
    public Rank? Rank { get; set; }

    [Column("IstTRU")]
    public bool IsTRU { get; set; }

    [Column("IstHRB")]
    public bool IsHRB { get; set; }

    [Column("IstAdmin")]
    public bool IsAdmin { get; set; }

    /// <summary>Visibility marker only; grants no rights.</summary>
    [Column("IstTeamLeitung")]
    public bool IsTeamLead { get; set; }

    /// <summary>Null = internal NOOSE agent.</summary>
    [Column("Partnerbehoerde")]
    public PartnerAgency? PartnerAgency { get; set; }

    /// <summary>Null = internal NOOSE agent.</summary>
    [Column("Partnerrang")]
    public PartnerRank? PartnerRank { get; set; }

    public AgentStatus Status { get; set; } = AgentStatus.Pending;

    [Column("RegistriertAm")]
    public DateTime RegisteredAt { get; set; }
    [Column("FreigegebenAm")]
    public DateTime? ReleasedAt { get; set; }
    [Column("FreigegebenVonId")]
    public string? ReleasedById { get; set; }

    [Column("GesperrtGrund")]
    public string? BlockedReason { get; set; }

    [Column("DiscordRollenSyncAm")]
    public DateTime? DiscordRolesSyncAt { get; set; }

    [Column("AusstehenderCodename")]
    public string? PendingCodename { get; set; }

    [Column("AusstehenderKlarname")]
    public string? PendingRealName { get; set; }

    [Column("AusstehendeDienstnummer")]
    public string? PendingBadgeNumber { get; set; }

    [Column("NamensaenderungBeantragtAm")]
    public DateTime? NameChangeRequestedAt { get; set; }

    /// <summary>Per-user nav preferences (favorites, order, recents) as JSON.</summary>
    [Column("NavEinstellungen")]
    public string? NavPreferencesJson { get; set; }
}

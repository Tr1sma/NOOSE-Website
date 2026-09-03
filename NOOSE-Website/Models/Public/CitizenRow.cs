namespace NOOSE_Website.Models.Public;

/// <summary>One citizen account for the admin roster.</summary>
public sealed record CitizenRow(
    string Id,
    string UserId,
    string FirstName,
    string LastName,
    string? DiscordUsername,
    bool IsBlocked,
    string? BlockedReason,
    DateTime? BlockedAt,
    bool AccountBlocked,
    int ConfirmedTips,
    string? LinkedPersonId,
    DateTime RegisteredAt)
{
    /// <summary>Name the public area shows this citizen by; falls back to the Discord handle before nothing.</summary>
    public string DisplayName
    {
        get
        {
            var name = $"{FirstName} {LastName}".Trim();
            return name.Length > 0 ? name : DiscordUsername ?? "—";
        }
    }

    /// <summary>True once first and last name are set; without both the citizen may submit nothing.</summary>
    public bool HasCompleteProfile
        => !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName);

    /// <summary>Once named, only leadership may rename; the citizen's own form refuses.</summary>
    public bool NameLocked => HasCompleteProfile;
}

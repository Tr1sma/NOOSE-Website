using System.Security.Claims;
using MudBlazor;
using NOOSE_Website.Authorization;

namespace NOOSE_Website.Services.Public;

/// <summary>One entry of the personal navigation row.</summary>
public sealed record CitizenNavEntry(string Label, string Href, string Icon);

/// <summary>The tabs of a signed-in visitor's own area; the single source both shells read.</summary>
/// <remarks>
/// It used to be a private array inside <c>BuergerLayout</c>, which is why the landing page could not show it: the
/// public tab bar had no way to reach it. Deliberately not a <c>PublicModules</c> entry — these routes are private,
/// they carry no switch and never appear for an anonymous visitor.
/// </remarks>
public static class CitizenNav
{
    public static readonly IReadOnlyList<CitizenNavEntry> Citizen =
    [
        new("Übersicht", "/buerger", Icons.Material.Filled.Home),
        new("Meine Hinweise", "/buerger/hinweise", Icons.Material.Filled.TipsAndUpdates),
        new("Meine Tickets", "/buerger/tickets", Icons.Material.Filled.Forum),
        new("Einspruch", "/buerger/einspruch", Icons.Material.Filled.Gavel),
        new("Mein Konto", "/buerger/profil", Icons.Material.Filled.Badge),
    ];

    /// <summary>Shown on top for an applicant; their own portal is a second personal area, not a citizen page.</summary>
    public static readonly CitizenNavEntry Application =
        new("Meine Bewerbung", "/portal", Icons.Material.Filled.Badge);

    /// <summary>Entries this account may see; empty for an anonymous visitor.</summary>
    public static IReadOnlyList<CitizenNavEntry> For(ClaimsPrincipal user)
    {
        if (!user.MayUseCitizenPortal())
        {
            return [];
        }
        return user.IsApplicant() ? [Application, .. Citizen] : Citizen;
    }
}

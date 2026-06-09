namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>Aktuell handelnder Agent (Id + Anzeigename), oder System bei Hintergrundaktionen.</summary>
public readonly record struct CurrentUserInfo(string? Id, string? Name)
{
    public static readonly CurrentUserInfo System = new(null, "System");
}

/// <summary>
/// Ermittelt den aktuell handelnden Agent – funktioniert sowohl in HTTP-Anfragen (z. B. den
/// OAuth-Endpoints) als auch in interaktiven Blazor-Circuits. Wird vom Audit-Interceptor und von
/// Protokoll-Diensten genutzt.
/// </summary>
public interface ICurrentUserService
{
    Task<CurrentUserInfo> GetAsync();
}

namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>Current acting agent (id, codename, read-only supervisor, partner flag), or System for background work.</summary>
public readonly record struct CurrentUserInfo(string? Id, string? Name, bool IsOnlyReader, bool IsPartner)
{
    public static readonly CurrentUserInfo System = new(null, "System", false, false);
}

/// <summary>
/// Ermittelt den aktuell handelnden Agent – funktioniert sowohl in HTTP-Anfragen (z. B. den
/// OAuth-Endpoints) als auch in interaktiven Blazor-Circuits. Wird vom Audit-Interceptor und von
/// Protokoll-Diensten genutzt.
/// </summary>
public interface ICurrentUserService
{
    Task<CurrentUserInfo> GetAsync();

    /// <summary>
    /// Synchrone Variante: ermittelt den Agent ausschließlich aus dem <see cref="HttpContext"/> (ohne den
    /// async AuthenticationStateProvider). Für synchrone Aufrufpfade (z. B. sync SaveChanges im Interceptor),
    /// damit dort nicht blockierend auf async gewartet wird. Liefert System, wenn kein HttpContext-Nutzer existiert.
    /// </summary>
    CurrentUserInfo Get();
}

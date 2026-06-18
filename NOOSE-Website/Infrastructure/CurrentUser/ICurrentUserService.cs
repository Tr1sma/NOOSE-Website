namespace NOOSE_Website.Infrastructure.CurrentUser;

/// <summary>Current acting agent (id, codename, read-only supervisor, partner flag), or System for background work.</summary>
public readonly record struct CurrentUserInfo(string? Id, string? Name, bool IsOnlyReader, bool IsPartner)
{
    public static readonly CurrentUserInfo System = new(null, "System", false, false);
}

/// <summary>Resolves the current acting agent in both HTTP requests and interactive Blazor circuits.</summary>
public interface ICurrentUserService
{
    Task<CurrentUserInfo> GetAsync();

    /// <summary>Sync variant resolving the agent from <see cref="HttpContext"/> only (no async provider); for sync save paths. Returns System if no HTTP user.</summary>
    CurrentUserInfo Get();
}

using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Replaces placeholder tokens in template HTML with concrete record/user values; the result stays editable.</summary>
public interface IPlaceholderService
{
    /// <summary>Replaces known placeholders; record context is optional. Unknown tokens are left unchanged.</summary>
    Task<string> ApplyAsync(string html, string? entityType, string? entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Supported placeholders (token + description) for the template editor help.</summary>
    IReadOnlyList<(string Token, string Description)> AvailablePlaceholder { get; }
}

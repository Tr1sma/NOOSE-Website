using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Replaces placeholder tokens in template HTML with concrete record/user values; the result stays editable.</summary>
public interface IPlaceholderService
{
    /// <summary>Replaces known placeholders; record context is optional. Unknown tokens are left unchanged.</summary>
    Task<string> ApplyAsync(string html, string? entityType, string? entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The same replacement for a PLAIN TEXT target: nothing is encoded, and a record token
    /// without a record stays raw.</summary>
    /// <remarks>
    /// Two differences from <see cref="ApplyAsync"/>, both deliberate. Encoding an umlaut into a numeric
    /// entity is right for markup and wrong in a comment field, where the reader would see the entity itself.
    /// And a record token that cannot resolve is blanked on the HTML path, which reads as "nothing belonged
    /// here"; in a field somebody is about to send, the raw token is the more honest answer - it shows that
    /// something is missing.
    /// </remarks>
    Task<string> ApplyPlainAsync(string text, string? entityType, string? entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Supported placeholders (token + description) for the template editor help.</summary>
    IReadOnlyList<(string Token, string Description)> AvailablePlaceholder { get; }
}

using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Personal text snippets: every method works on the caller's own collection and no other.</summary>
public interface ITextSnippetService
{
    /// <summary>The caller's snippets, in their own order.</summary>
    Task<List<TextSnippet>> GetMineAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<TextSnippet> CreateAsync(TextSnippetInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, TextSnippetInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

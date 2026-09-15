using System.Security.Claims;
using NOOSE_Website.Data.Entities.Handbook;
using NOOSE_Website.Models.Handbook;

namespace NOOSE_Website.Services.Handbook;

/// <summary>Reads and edits the internal handbook shown on <c>/handbuch</c>.</summary>
/// <remarks>
/// Reads carry no viewer scope: like the law book, the handbook is open to every internal agent. Writes are HRB or
/// leadership - the people who look after new agents are the people who keep the instructions right.
/// </remarks>
public interface IHandbookService
{
    /// <summary>Visible chapters in order, each with its visible articles; empty chapters are kept (they are a rail).</summary>
    Task<List<HandbookChapterView>> GetChaptersAsync(CancellationToken cancellationToken = default);

    /// <summary>One article by its slug, with steps; null when it does not exist or is hidden.</summary>
    Task<HandbookArticleView?> GetArticleAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>The article that explains a menu entry, for the help button; null when none is assigned.</summary>
    Task<HandbookArticleCard?> GetArticleForNavKeyAsync(string navKey, CancellationToken cancellationToken = default);

    /// <summary>Visible glossary terms, alphabetical.</summary>
    Task<List<GlossaryTermView>> GetGlossaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Every term including withdrawn ones; for the editor, which is the only way back from hiding.</summary>
    Task<List<GlossaryTermView>> GetAllTermsAsync(CancellationToken cancellationToken = default);

    /// <summary>The glossary prepared for matching, cached: every rich-text block on a page asks for it.</summary>
    Task<GlossaryMatcher> GetGlossaryMatcherAsync(CancellationToken cancellationToken = default);

    /// <summary>Every chapter including hidden ones, in order; for the editor.</summary>
    Task<List<HandbookChapter>> GetAllChaptersAsync(CancellationToken cancellationToken = default);

    /// <summary>Every article of one chapter including hidden ones; for the editor.</summary>
    Task<List<HandbookArticle>> GetAllArticlesAsync(string chapterId, CancellationToken cancellationToken = default);

    Task<HandbookChapter> CreateChapterAsync(HandbookChapterInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshChapterAsync(string id, HandbookChapterInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteChapterAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<HandbookArticle> CreateArticleAsync(HandbookArticleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshArticleAsync(string id, HandbookArticleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteArticleAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<GlossaryTerm> CreateTermAsync(GlossaryTermInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshTermAsync(string id, GlossaryTermInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Soft-deleted chapters and articles; glossary terms are hidden rather than deleted.</summary>
    Task<List<HandbookChapter>> GetChapterTrashAsync(CancellationToken cancellationToken = default);
    Task<List<HandbookArticle>> GetArticleTrashAsync(CancellationToken cancellationToken = default);
    Task RestoreChapterAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreArticleAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

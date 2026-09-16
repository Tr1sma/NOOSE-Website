namespace NOOSE_Website.Models.Handbook;

/// <summary>One chapter with the articles the reader may see.</summary>
/// <param name="IsVisible">
/// Always true on the reading path, which never hands out a withdrawn chapter. The redaction view builds its own
/// list from the full one, and needs to tell the two apart: the rail is the only way to a chapter, so a withdrawn
/// one that the rail also hides can never be switched back on.
/// </param>
public sealed record HandbookChapterView(
    string Id,
    string Slug,
    string Title,
    string? Description,
    string? IconName,
    IReadOnlyList<HandbookArticleCard> Articles,
    bool IsVisible = true);

/// <summary>An article as it appears in a chapter list or a search result.</summary>
/// <param name="IsVisible">See <see cref="HandbookChapterView.IsVisible"/>; the same one-way door.</param>
public sealed record HandbookArticleCard(
    string Id,
    string Slug,
    string Title,
    string? Summary,
    string ChapterSlug,
    string ChapterTitle,
    bool IsVisible = true);

/// <summary>A full article, as the reader page renders it.</summary>
public sealed record HandbookArticleView(
    string Id,
    string Slug,
    string Title,
    string? Summary,
    string? ContentHtml,
    string? RoleplayHtml,
    string? DiagramKey,
    string ChapterSlug,
    string ChapterTitle,
    IReadOnlyList<HandbookStepView> Steps);

/// <summary>One numbered card of a walkthrough.</summary>
public sealed record HandbookStepView(int Number, string? IconName, string Title, string Text);

/// <summary>A glossary term, with the article that goes into detail.</summary>
/// <param name="ArticleId">The stored link, kept apart from the slug on purpose: the slug is only filled for an
/// article the reader may see, so rebuilding the id from it dropped the assignment as soon as the target was
/// hidden — and the next unrelated save wrote that emptiness back.</param>
/// <param name="IsVisible">False for a withdrawn term. Readers never receive one; the editor list does, because
/// a term has no trash and its name stays taken, so an invisible one would be unreachable forever.</param>
public sealed record GlossaryTermView(
    string Id,
    string Term,
    string? Synonyms,
    string ShortDefinition,
    string? ExplanationHtml,
    string? ArticleSlug,
    string? ArticleTitle,
    string? ArticleId = null,
    bool IsVisible = true);

/// <summary>Editable fields of a chapter.</summary>
public sealed record HandbookChapterInput(
    string Slug,
    string Title,
    string? Description,
    string? IconName,
    int SortOrder,
    bool IsVisible);

/// <summary>Editable fields of an article. Steps are edited separately; the reader sees them as one page.</summary>
public sealed record HandbookArticleInput(
    string ChapterId,
    string Slug,
    string Title,
    string? Summary,
    string? ContentHtml,
    string? RoleplayHtml,
    string? DiagramKey,
    string? NavKey,
    int SortOrder,
    bool IsVisible);

/// <summary>Editable fields of a glossary term.</summary>
public sealed record GlossaryTermInput(
    string Term,
    string? Synonyms,
    string ShortDefinition,
    string? ExplanationHtml,
    string? ArticleId,
    bool IsVisible);

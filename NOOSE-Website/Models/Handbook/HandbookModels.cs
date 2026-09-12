namespace NOOSE_Website.Models.Handbook;

/// <summary>One chapter with the articles the reader may see.</summary>
public sealed record HandbookChapterView(
    string Id,
    string Slug,
    string Title,
    string? Description,
    string? IconName,
    IReadOnlyList<HandbookArticleCard> Articles);

/// <summary>An article as it appears in a chapter list or a search result.</summary>
public sealed record HandbookArticleCard(
    string Id,
    string Slug,
    string Title,
    string? Summary,
    string ChapterSlug,
    string ChapterTitle);

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
public sealed record GlossaryTermView(
    string Id,
    string Term,
    string? Synonyms,
    string ShortDefinition,
    string? ExplanationHtml,
    string? ArticleSlug,
    string? ArticleTitle);

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

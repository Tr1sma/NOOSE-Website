using Markdig;

namespace NOOSE_Website.Services;

/// <summary>Renders trusted-but-unverified LLM markdown to sanitized HTML for display.</summary>
public static class MarkdownRenderer
{
    // DisableHtml: never pass raw HTML from the model through; HtmlCleanup sanitizes the rendered output on top.
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().DisableHtml().UseSoftlineBreakAsHardlineBreak().UsePipeTables().Build();

    /// <summary>Markdown → HTML → sanitized HTML. Empty in, empty out.</summary>
    public static string ToSafeHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }
        var html = Markdown.ToHtml(markdown, Pipeline);
        return HtmlCleanup.Clean(html);
    }
}

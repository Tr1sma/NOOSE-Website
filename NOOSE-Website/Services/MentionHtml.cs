using System.Net;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Turns @-mention tokens inside stored WYSIWYG HTML into links, for the viewer asking.</summary>
/// <remarks>
/// Only text nodes are touched: a token that happens to sit in an attribute value stays untouched, and a token
/// inside an existing link renders as a plain chip rather than a nested anchor.
/// Runs on the render path, after <see cref="HtmlCleanup.Clean"/>, so its output never meets the sanitizer.
/// </remarks>
public static class MentionHtml
{
    /// <summary>Every mention ref in the text nodes of the fragment; empty when there is nothing to resolve.</summary>
    public static IReadOnlyList<(string Type, string Id)> Refs(string? html)
    {
        if (!MayContainToken(html))
        {
            return Array.Empty<(string, string)>();
        }
        var body = Parse(html!);
        if (body is null)
        {
            return Array.Empty<(string, string)>();
        }
        var refs = new List<(string, string)>();
        foreach (var node in TextNodes(body))
        {
            foreach (var token in MentionParser.Parse(node.Data))
            {
                refs.Add((token.Type, token.Id));
            }
        }
        return refs.Distinct().ToList();
    }

    /// <summary>Replaces the tokens with markup the viewer may see; <paramref name="plain"/> writes bare text for print.</summary>
    public static string Rewrite(string? html, Dictionary<(string, string), RecordsReference.Resolution> map,
        bool isLeadership, bool plain = false)
    {
        if (!MayContainToken(html))
        {
            return html ?? string.Empty;
        }
        var body = Parse(html!);
        if (body is null)
        {
            return html!;
        }

        var touched = false;
        foreach (var node in TextNodes(body))
        {
            var tokens = MentionParser.Parse(node.Data);
            if (tokens.Count == 0 || node.Parent is null)
            {
                continue;
            }
            // same segmentation as the plain-text path, so "Verschlusssache"/"(nicht verfügbar)" is decided once
            var segments = MentionService.Segment(node.Data, tokens, map, isLeadership);
            var markup = Markup(segments, plain || InsideLink(node));

            var holder = body.Owner!.CreateElement("span");
            holder.InnerHtml = markup;
            while (holder.FirstChild is { } child)
            {
                node.Parent.InsertBefore(child, node);
            }
            node.Parent.RemoveChild(node);
            touched = true;
        }
        return touched ? body.InnerHtml : html!;
    }

    private static string Markup(IReadOnlyList<MentionSegment> segments, bool plain)
    {
        var sb = new StringBuilder();
        foreach (var seg in segments)
        {
            if (!seg.IsReference || plain)
            {
                sb.Append(WebUtility.HtmlEncode(seg.Text));
            }
            else if (seg.Hidden)
            {
                sb.Append("<span class=\"erwaehnung erwaehnung-vs\">Verschlusssache</span>");
            }
            else if (!string.IsNullOrEmpty(seg.Href))
            {
                sb.Append("<a class=\"erwaehnung\" href=\"").Append(WebUtility.HtmlEncode(seg.Href))
                    .Append("\">@").Append(WebUtility.HtmlEncode(seg.Text)).Append("</a>");
            }
            else
            {
                sb.Append("<span class=\"erwaehnung erwaehnung-fehlt\">")
                    .Append(WebUtility.HtmlEncode(seg.Text)).Append("</span>");
            }
        }
        return sb.ToString();
    }

    private static bool MayContainToken(string? html) =>
        !string.IsNullOrEmpty(html) && html.Contains("@{", StringComparison.Ordinal);

    private static IElement? Parse(string html) => new HtmlParser().ParseDocument(html).Body;

    // materialized: the rewrite splices nodes into the same tree it walks
    private static List<IText> TextNodes(IElement body)
    {
        var found = new List<IText>();
        Collect(body, found);
        return found;
    }

    private static void Collect(INode node, List<IText> found)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text)
            {
                if (text.Data.Contains("@{", StringComparison.Ordinal))
                {
                    found.Add(text);
                }
                continue;
            }
            Collect(child, found);
        }
    }

    private static bool InsideLink(INode node)
    {
        for (var p = node.ParentElement; p is not null; p = p.ParentElement)
        {
            if (p.NodeName == "A")
            {
                return true;
            }
        }
        return false;
    }
}

using Ganss.Xss;

namespace NOOSE_Website.Services;

/// <summary>
/// Serverseitige HTML-Bereinigung für die im WYSIWYG-Editor (Quill) erzeugten Dokument-/Vorlagen-Inhalte.
/// Maßgebliche Sicherheitskontrolle: Der gespeicherte HTML-String wird hier auf eine enge Whitelist
/// reduziert (nur Quill-Formatierungen), bevor er persistiert und später per <c>MarkupString</c> gerendert
/// wird. Skripte, Event-Handler und gefährliche Schemata (javascript:/data:) werden entfernt.
/// </summary>
public static class HtmlCleanup
{
    /// <summary>Bereinigt den HTML-Inhalt auf die erlaubte Formatierungs-Teilmenge. Liefert nie null.</summary>
    public static string Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }
        return Generate().Sanitize(html);
    }

    // Pro Aufruf eine frische Instanz – vermeidet Thread-Sicherheits-Fragen (Dokumente werden nicht
    // hochfrequent gespeichert, der Aufwand ist vernachlässigbar).
    private static HtmlSanitizer Generate()
    {
        var s = new HtmlSanitizer();

        s.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "span", "b", "strong", "i", "em", "u", "s",
            "h1", "h2", "h3", "ul", "ol", "li", "blockquote", "pre", "code", "a",
            // Tabellen (vom RichTextEditor / vendored quill1.3.7-table-module). Das Modul speichert
            // eine spezifische, aber harmlose Struktur: <div.ql-table-wrapper> umschließt die <table>,
            // Zellinhalte stehen in einem eigenen <contain>-Element. Diese Tags müssen erhalten bleiben,
            // sonst lässt sich eine gespeicherte Tabelle nicht wieder im Editor öffnen (Round-Trip).
            "table", "thead", "tbody", "tr", "td", "th", "caption", "colgroup", "col", "div", "contain",
        })
        {
            s.AllowedTags.Add(tag);
        }

        s.AllowedAttributes.Clear();
        foreach (var attr in new[]
        {
            "href", "target", "rel", "class", "style",
            // Tabellen-Struktur-Attribute des Moduls. Allesamt unkritisch (kein Script/Handler);
            // die data-*-IDs werden beim erneuten Öffnen gebraucht, um die Tabelle zu rekonstruieren.
            "colspan", "rowspan", "width", "cellpadding", "cellspacing", "contenteditable",
            "data-table-id", "data-row-id", "data-col-id", "data-rowspan", "data-colspan",
            "data-row", "data-col", "data-w", "data-full",
        })
        {
            s.AllowedAttributes.Add(attr);
        }

        s.AllowedCssProperties.Clear();
        foreach (var prop in new[]
        {
            "color", "background-color", "text-align",
            // Für Zell-/Tabellen-Layout (Spaltenbreiten, Zellrahmen/-farben aus dem Kontextmenü).
            "width", "height", "vertical-align",
            "border", "border-color", "border-style", "border-width",
        })
        {
            s.AllowedCssProperties.Add(prop);
        }

        // Nur sichere Link-Schemata (kein javascript:/data: → kein Stored-XSS über Links).
        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("https");
        s.AllowedSchemes.Add("mailto");

        return s;
    }
}

using Ganss.Xss;

namespace NOOSE_Website.Services;

/// <summary>
/// Serverseitige HTML-Bereinigung für die im WYSIWYG-Editor (Quill) erzeugten Dokument-/Vorlagen-Inhalte.
/// Maßgebliche Sicherheitskontrolle: Der gespeicherte HTML-String wird hier auf eine enge Whitelist
/// reduziert (nur Quill-Formatierungen), bevor er persistiert und später per <c>MarkupString</c> gerendert
/// wird. Skripte, Event-Handler und gefährliche Schemata (javascript:/data:) werden entfernt.
/// </summary>
public static class HtmlBereinigung
{
    /// <summary>Bereinigt den HTML-Inhalt auf die erlaubte Formatierungs-Teilmenge. Liefert nie null.</summary>
    public static string Bereinige(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }
        return Erzeugen().Sanitize(html);
    }

    // Pro Aufruf eine frische Instanz – vermeidet Thread-Sicherheits-Fragen (Dokumente werden nicht
    // hochfrequent gespeichert, der Aufwand ist vernachlässigbar).
    private static HtmlSanitizer Erzeugen()
    {
        var s = new HtmlSanitizer();

        s.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "span", "b", "strong", "i", "em", "u", "s",
            "h1", "h2", "h3", "ul", "ol", "li", "blockquote", "pre", "code", "a",
            // Tabellen (Quill „table-better"): rein strukturell, keine Script-Vektoren.
            "table", "thead", "tbody", "tfoot", "tr", "td", "th", "colgroup", "col", "caption",
        })
        {
            s.AllowedTags.Add(tag);
        }

        s.AllowedAttributes.Clear();
        foreach (var attr in new[]
        {
            "href", "target", "rel", "class", "style",
            // Tabellen-Layout-Attribute.
            "colspan", "rowspan", "span", "width", "height",
        })
        {
            s.AllowedAttributes.Add(attr);
        }
        // Das Tabellen-Modul hängt seinen Zustand an data-*-Attribute (z. B. data-row) → zulassen,
        // damit Tabellen beim erneuten Laden in den Editor korrekt rekonstruiert werden. data-* kann
        // kein Skript ausführen, ist also sicher.
        s.AllowDataAttributes = true;

        s.AllowedCssProperties.Clear();
        foreach (var prop in new[]
        {
            "color", "background-color", "text-align",
            // Tabellen-Darstellung (Rahmen, Größen, Zell-Innenabstand, vertikale Ausrichtung).
            "width", "height", "min-width", "max-width", "vertical-align", "padding",
            "border", "border-top", "border-right", "border-bottom", "border-left",
            "border-color", "border-style", "border-width", "border-collapse",
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

namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Schlanke Zeile für die Dokumenten-Bibliothek/Auswahl (ohne den großen HTML-Body).</summary>
public record DokumentListeItem(
    string Id,
    string Titel,
    string? Kategorie,
    bool IstVerschlusssache,
    DateTime Aktualisiert,
    bool Angepinnt);

/// <summary>Eine Akte, an die ein Dokument angehängt ist (für die „Angehängt an"-Liste im Viewer).</summary>
public record DokumentAnhang(string EntitaetTyp, string EntitaetId, string Anzeige, string? Href);

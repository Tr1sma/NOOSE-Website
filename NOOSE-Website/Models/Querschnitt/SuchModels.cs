using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;

namespace NOOSE_Website.Models.Querschnitt;

/// <summary>
/// Kriterien einer globalen Suche. <see cref="Kategorien"/> enthält die CLR-Typnamen der zu
/// durchsuchenden Akten (leer = alle). <see cref="TagIds"/> schränkt auf Akten mit mindestens einem
/// dieser Tags ein. Wird für gespeicherte Suchen als JSON abgelegt.
/// </summary>
public class SuchKriterien
{
    public string? Text { get; set; }
    public List<string> Kategorien { get; set; } = new();
    public List<string> TagIds { get; set; } = new();
}

/// <summary>
/// Ein einzelner Suchtreffer. <see cref="Kategorie"/> ist der CLR-Typname der Trefferquelle;
/// <see cref="ZielId"/> + Kategorie bestimmen die Zielroute (siehe <see cref="SuchNavigation"/>).
/// Bei Doks/Quellen/Kommentaren verweist <see cref="ZielId"/> auf die zugehörige Person.
/// </summary>
public record SuchTreffer(string Kategorie, string ZielId, string Titel, string Schnipsel, string Aktenzeichen);

/// <summary>Treffer einer Kategorie gebündelt (für die kategorisierte Ergebnisanzeige).</summary>
public record SuchErgebnisGruppe(string Kategorie, string Anzeige, List<SuchTreffer> Treffer);

/// <summary>Kompakter Treffer für die Command-Palette (Schnellzugriff).</summary>
public record SchnellTreffer(string Kategorie, string ZielId, string Name, string Aktenzeichen);

/// <summary>Zielroute eines Treffers je Kategorie. Doks/Quellen/Kommentare verweisen auf die Person-Akte.</summary>
public static class SuchNavigation
{
    public static string Route(string kategorie, string zielId) => kategorie switch
    {
        nameof(Fraktion) => $"/fraktionen/{zielId}",
        nameof(Personengruppe) => $"/personengruppen/{zielId}",
        _ => $"/personen/{zielId}",
    };
}

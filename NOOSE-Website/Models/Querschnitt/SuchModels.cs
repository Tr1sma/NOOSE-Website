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
/// Ein einzelner Suchtreffer. <see cref="Kategorie"/> ist der CLR-Typname der Trefferquelle (für die
/// Gruppierung). <see cref="ZielId"/> verweist auf die anzuspringende Akte; <see cref="ZielTyp"/> ist deren
/// Aktentyp (Person/Fraktion/Personengruppe) – bei Doks/Quellen/Kommentaren der Eltern-Akten-Typ. Ist
/// <see cref="ZielTyp"/> null, dient die Kategorie als Zieltyp (siehe <see cref="SuchNavigation"/>).
/// </summary>
public record SuchTreffer(string Kategorie, string ZielId, string Titel, string Schnipsel, string Aktenzeichen, string? ZielTyp = null);

/// <summary>Treffer einer Kategorie gebündelt (für die kategorisierte Ergebnisanzeige).</summary>
public record SuchErgebnisGruppe(string Kategorie, string Anzeige, List<SuchTreffer> Treffer);

/// <summary>Kompakter Treffer für die Command-Palette (Schnellzugriff).</summary>
public record SchnellTreffer(string Kategorie, string ZielId, string Name, string Aktenzeichen);

/// <summary>Zielroute eines Treffers. Doks/Quellen/Kommentare verweisen auf ihre Eltern-Akte (Person/Fraktion/Gruppe).</summary>
public static class SuchNavigation
{
    public static string Route(string aktenTyp, string zielId) => aktenTyp switch
    {
        nameof(Fraktion) => $"/fraktionen/{zielId}",
        nameof(Personengruppe) => $"/personengruppen/{zielId}",
        _ => $"/personen/{zielId}",
    };

    /// <summary>Route eines Treffers: nutzt den expliziten Zieltyp, sonst die Kategorie.</summary>
    public static string Route(SuchTreffer treffer) => Route(treffer.ZielTyp ?? treffer.Kategorie, treffer.ZielId);
}

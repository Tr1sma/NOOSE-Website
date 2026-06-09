using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Gemeinsamer Vorschlagskatalog für die Steckbrief-Mehrfachfelder (Waffen/Fahrzeuge/Orte). Liefert
/// die Autocomplete-Vorschläge und wächst, sobald neue Werte erfasst werden. Wird ausschließlich aus
/// unklassifizierten Personen befüllt (siehe <c>PersonService</c>), damit keine Verschlusssachen-Werte
/// in die geteilte Vorschlagsliste gelangen.
/// </summary>
public interface ISteckbriefVorschlagService
{
    /// <summary>Alphabetisch sortierte, distinkte Werte eines Typs – Datenquelle für das Autocomplete.</summary>
    Task<IReadOnlyList<string>> GetAsync(VorschlagTyp typ, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merkt fehlende Werte im Katalog vor: fügt sie dem <b>übergebenen</b> Context hinzu, ohne selbst zu
    /// speichern. Der Aufrufer persistiert sie mit seinem eigenen <c>SaveChanges</c> (atomar mit der
    /// Person) – nötig, seit jeder Dienst seinen eigenen Context aus der Factory bezieht. Vorhandene
    /// Werte werden case-insensitiv übersprungen; der Unique-Index sichert gegen Races.
    /// </summary>
    Task VormerkenAsync(AppDbContext db, VorschlagTyp typ, IEnumerable<string> werte, CancellationToken cancellationToken = default);
}

using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Liefert und verwaltet die Schwellwerte der Aktualitäts-Ampel je Aktentyp und bewertet Akten anhand ihres
/// letzten Änderungsdatums. Im Code stehen Standardwerte je Typ; die Führung kann sie im Admin überschreiben
/// (in der DB). Die Schwellen werden gecacht, damit Listen die Ampel ohne DB-Treffer je Zeile berechnen können.
/// </summary>
public interface IAktualitaetService
{
    /// <summary>Die unterstützten Aktentypen (mit Anzeigename und Standardwerten) – für den Admin-Bereich.</summary>
    IReadOnlyList<AktualitaetsTypInfo> UnterstuetzteTypen { get; }

    /// <summary>
    /// Aktuelle Schwellwerte je Aktentyp (Standard aus dem Code, überschrieben durch gespeicherte Werte). Gecacht;
    /// enthält stets einen Eintrag für jeden unterstützten Typ.
    /// </summary>
    Task<IReadOnlyDictionary<string, (int WarnungTage, int VeraltetTage)>> GetSchwellenAsync(CancellationToken cancellationToken = default);

    /// <summary>Die Schwellwerte eines einzelnen Aktentyps (Standard, falls nicht überschrieben).</summary>
    Task<(int WarnungTage, int VeraltetTage)> GetSchwelleAsync(string aktenTyp, CancellationToken cancellationToken = default);

    /// <summary>Bewertet eine Akte anhand ihres Referenzdatums (<c>GeaendertAm ?? ErstelltAm</c>, UTC).</summary>
    Task<AktualitaetsStufe> BewertenAsync(string aktenTyp, DateTime referenzdatum, CancellationToken cancellationToken = default);

    /// <summary>Speichert die Schwellwerte eines Aktentyps (nur Führung) und leert den Cache.</summary>
    Task SpeichernAsync(string aktenTyp, int warnungTage, int veraltetTage, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}

/// <summary>Ein unterstützter Aktentyp der Aktualitäts-Ampel inkl. Anzeigename und Standard-Schwellwerten.</summary>
public sealed record AktualitaetsTypInfo(string Typ, string Anzeige, int StandardWarnungTage, int StandardVeraltetTage);

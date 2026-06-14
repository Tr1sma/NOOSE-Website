using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Liefert und verwaltet die Schwellwerte der Aktualitäts-Ampel je Aktentyp und bewertet Akten anhand ihres
/// letzten Änderungsdatums. Im Code stehen Standardwerte je Typ; die Führung kann sie im Admin überschreiben
/// (in der DB). Die Schwellen werden gecacht, damit Listen die Ampel ohne DB-Treffer je Zeile berechnen können.
/// </summary>
public interface IRecencyService
{
    /// <summary>Die unterstützten Aktentypen (mit Anzeigename und Standardwerten) – für den Admin-Bereich.</summary>
    IReadOnlyList<RecencyTypeInfo> SupportedTypes { get; }

    /// <summary>
    /// Aktuelle Schwellwerte je Aktentyp (Standard aus dem Code, überschrieben durch gespeicherte Werte). Gecacht;
    /// enthält stets einen Eintrag für jeden unterstützten Typ.
    /// </summary>
    Task<IReadOnlyDictionary<string, (int WarningDays, int StaleDays)>> GetThresholdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Die Schwellwerte eines einzelnen Aktentyps (Standard, falls nicht überschrieben).</summary>
    Task<(int WarningDays, int StaleDays)> GetThresholdAsync(string recordsType, CancellationToken cancellationToken = default);

    /// <summary>Bewertet eine Akte anhand ihres Referenzdatums (<c>GeaendertAm ?? ErstelltAm</c>, UTC).</summary>
    Task<RecencyLevel> AssessAsync(string recordsType, DateTime referenceDate, CancellationToken cancellationToken = default);

    /// <summary>Speichert die Schwellwerte eines Aktentyps (nur Führung) und leert den Cache.</summary>
    Task SaveAsync(string recordsType, int warningDays, int staleDays, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <summary>Ein unterstützter Aktentyp der Aktualitäts-Ampel inkl. Anzeigename und Standard-Schwellwerten.</summary>
public sealed record RecencyTypeInfo(string Type, string Display, int DefaultWarningDays, int DefaultStaleDays);

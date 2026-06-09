using System.Globalization;
using System.Text.Json;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Bereitet das <c>AenderungenJson</c> eines Audit-Eintrags für die Anzeige auf: macht aus dem rohen
/// „Feld → [alt, neu]"-JSON eine Liste lesbarer Feldänderungen mit deutschen Labels. Technische Felder
/// (Audit-/Soft-Delete-Plumbing) werden ausgeblendet; Enums/Datumswerte werden in Klartext übersetzt.
/// </summary>
public static class AuditAnzeige
{
    /// <summary>Eine einzelne Feldänderung „alt → neu" für die Timeline.</summary>
    public record Feldaenderung(string Feld, string Alt, string Neu);

    // Audit-/Soft-Delete-/Fremdschlüssel-Felder interessieren den Leser nicht.
    private static readonly HashSet<string> Versteckt = new(StringComparer.Ordinal)
    {
        "ErstelltAm", "ErstelltVonId", "GeaendertAm", "GeaendertVonId",
        "GeloeschtAm", "GeloeschtVonId", "IstGeloescht",
        "PersonId", "FraktionId", "PersonengruppeId", "AgentId",
    };

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["Name"] = "Name", ["Beschreibung"] = "Beschreibung", ["Aktenzeichen"] = "Aktenzeichen",
        ["Lebensstatus"] = "Lebensstatus", ["TotBis"] = "Tot-Fenster", ["Einstufung"] = "Einstufung",
        ["IstVerschlusssache"] = "Verschlusssache",
        ["Grund"] = "Grund", ["Fraktion"] = "Fraktion (Freitext)", ["OrgTyp"] = "Verknüpfte Org (Typ)",
        ["OrgId"] = "Verknüpfte Org", ["ErhalteneInformationen"] = "Erhaltene Informationen",
        ["Wahrheitsserum"] = "Wahrheitsserum", ["Ausgang"] = "Maßnahme-Ausgang",
        ["GedaechtnisGeloescht"] = "Gedächtnisverlust", ["Zeitpunkt"] = "Zeitpunkt",
        ["Art"] = "Art", ["Funk"] = "Funk", ["Darkchat"] = "Darkchat",
        ["Ausstellungszeiten"] = "Ausstellungszeiten", ["Anwesen"] = "Anwesen",
        ["Erkennungsfarbe"] = "Erkennungsfarbe", ["Ziele"] = "Ziele",
        ["Rang"] = "Rang", ["Rolle"] = "Rolle", ["IstLeitung"] = "Leitung",
        ["GeschaetzteMitgliederzahl"] = "Geschätzte Mitgliederzahl", ["Label"] = "Bezeichnung",
        ["Codename"] = "Codename", ["Klarname"] = "Klarname", ["Dienstnummer"] = "Dienstnummer",
    };

    /// <summary>Parst das JSON in lesbare Feldänderungen; bei null/ungültig eine leere Liste.</summary>
    public static IReadOnlyList<Feldaenderung> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<Feldaenderung>();
        }

        Dictionary<string, JsonElement[]>? roh;
        try
        {
            roh = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(json);
        }
        catch (JsonException)
        {
            return Array.Empty<Feldaenderung>();
        }
        if (roh is null)
        {
            return Array.Empty<Feldaenderung>();
        }

        var liste = new List<Feldaenderung>();
        foreach (var (feld, werte) in roh)
        {
            if (Versteckt.Contains(feld))
            {
                continue;
            }
            var alt = werte.Length > 0 ? Format(feld, werte[0]) : "—";
            var neu = werte.Length > 1 ? Format(feld, werte[1]) : "—";
            liste.Add(new Feldaenderung(Labels.GetValueOrDefault(feld, feld), alt, neu));
        }
        return liste;
    }

    private static string Format(string feld, JsonElement wert)
    {
        switch (wert.ValueKind)
        {
            case JsonValueKind.Null:
                return "—";
            case JsonValueKind.True:
                return "Ja";
            case JsonValueKind.False:
                return "Nein";
            case JsonValueKind.Number when wert.TryGetInt32(out var n):
                return FormatEnum(feld, n);
            case JsonValueKind.String:
                var s = wert.GetString();
                if (string.IsNullOrEmpty(s))
                {
                    return "—";
                }
                // ISO-Datumswerte (Zeitpunkt/TotBis) lesbar machen.
                if ((feld is "Zeitpunkt" or "TotBis")
                    && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    return dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                }
                return s;
            default:
                return wert.ToString();
        }
    }

    // Bekannte Enum-Felder als Klartext; sonst die rohe Zahl.
    private static string FormatEnum(string feld, int n) => feld switch
    {
        "Einstufung" => EinstufungAnzeige.Name((Einstufung)n),
        "Lebensstatus" => LebensstatusAnzeige.Name((Lebensstatus)n),
        "Ausgang" => MassnahmeAusgangAnzeige.Name((MassnahmeAusgang)n),
        _ => n.ToString(),
    };
}

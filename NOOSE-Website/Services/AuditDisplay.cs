using System.Globalization;
using System.Text.Json;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Formats audit entry JSON into readable field changes.</summary>
public static class AuditDisplay
{
    /// <summary>Single field change record.</summary>
    public record FieldChange(string Field, string Alt, string New);

    // skip meta fields
    private static readonly HashSet<string> Hidden = new(StringComparer.Ordinal)
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
        ["Ausstellungszeiten"] = "Aufstellungszeiten", ["Anwesen"] = "Anwesen",
        ["Erkennungsfarbe"] = "Erkennungsfarbe", ["Ziele"] = "Ziele",
        ["Rang"] = "Rang", ["Rolle"] = "Rolle", ["IstLeitung"] = "Leitung",
        ["GeschaetzteMitgliederzahl"] = "Geschätzte Mitgliederzahl", ["Label"] = "Bezeichnung",
        ["Codename"] = "Codename", ["Klarname"] = "Klarname", ["Dienstnummer"] = "Dienstnummer",
    };

    /// <summary>Parses JSON into field changes; empty on null/invalid.</summary>
    public static IReadOnlyList<FieldChange> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<FieldChange>();
        }

        Dictionary<string, JsonElement[]>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(json);
        }
        catch (JsonException)
        {
            return Array.Empty<FieldChange>();
        }
        if (raw is null)
        {
            return Array.Empty<FieldChange>();
        }

        var list = new List<FieldChange>();
        foreach (var (field, values) in raw)
        {
            if (Hidden.Contains(field))
            {
                continue;
            }
            var alt = values.Length > 0 ? Format(field, values[0]) : "—";
            var @new = values.Length > 1 ? Format(field, values[1]) : "—";
            list.Add(new FieldChange(Labels.GetValueOrDefault(field, field), alt, @new));
        }
        return list;
    }

    private static string Format(string field, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                return "—";
            case JsonValueKind.True:
                return "Ja";
            case JsonValueKind.False:
                return "Nein";
            case JsonValueKind.Number when value.TryGetInt32(out var n):
                return FormatEnum(field, n);
            case JsonValueKind.String:
                var s = value.GetString();
                if (string.IsNullOrEmpty(s))
                {
                    return "—";
                }
                // format dates
                if ((field is "Zeitpunkt" or "TotBis")
                    && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    return dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                }
                return s;
            default:
                return value.ToString();
        }
    }

    // enum to string
    private static string FormatEnum(string field, int n) => field switch
    {
        "Einstufung" => ClassificationDisplay.Name((Classification)n),
        "Lebensstatus" => LifeStatusDisplay.Name((LifeStatus)n),
        "Ausgang" => MeasureOutcomeDisplay.Name((MeasureOutcome)n),
        _ => n.ToString(),
    };
}

using System.Text.Json;

namespace NOOSE_Website.Services;

/// <summary>The fixed answer shapes NOOSEI must satisfy. Parsed once; bump the version when a shape changes.</summary>
public static class NooseiSchemas
{
    /// <summary>Generation of <see cref="Kurzbrief"/>; part of the brief content hash, so a change invalidates every cached brief.</summary>
    public const int KurzbriefVersion = 1;

    public const string KurzbriefName = "noose_kurzbrief";

    private const string KurzbriefJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["tldr", "kernpunkte", "einstufung_bewertung", "verbindungen", "verlauf", "offene_punkte", "risiko"],
          "properties": {
            "tldr": {
              "type": "string",
              "description": "Zwei bis drei Sätze mit der Kernaussage der Akte."
            },
            "kernpunkte": {
              "type": "array",
              "description": "Die wichtigsten Fakten, je einer pro Eintrag, ohne Wiederholung des TL;DR.",
              "items": { "type": "string" }
            },
            "einstufung_bewertung": {
              "type": "string",
              "description": "Passt die vergebene Einstufung zur Aktenlage? Kurze Begründung, keine Empfehlung."
            },
            "verbindungen": {
              "type": "array",
              "description": "Wichtige Verbindungen zu anderen Akten, nur solche, die im Kontext stehen.",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["wer", "art", "relevanz"],
                "properties": {
                  "wer": { "type": "string", "description": "Name oder Bezeichnung, genau wie im Kontext." },
                  "art": { "type": "string", "description": "Art der Verbindung, z. B. Mitglied, Kontakt, Konflikt." },
                  "relevanz": { "type": "string", "description": "Warum diese Verbindung zählt." }
                }
              }
            },
            "verlauf": {
              "type": "array",
              "description": "Ereignisse in zeitlicher Reihenfolge, älteste zuerst.",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["wann", "was"],
                "properties": {
                  "wann": { "type": "string", "description": "Datum wie im Kontext angegeben; leer lassen, wenn unbekannt." },
                  "was": { "type": "string" }
                }
              }
            },
            "offene_punkte": {
              "type": "array",
              "description": "Was die Akte offen lässt: fehlende Angaben, ungeklärte Widersprüche.",
              "items": { "type": "string" }
            },
            "risiko": {
              "type": "object",
              "additionalProperties": false,
              "required": ["stufe", "begruendung"],
              "properties": {
                "stufe": { "type": "string", "enum": ["niedrig", "mittel", "hoch"] },
                "begruendung": { "type": "string", "description": "Kurze Begründung, ausschließlich aus den gelieferten Fakten." }
              }
            }
          }
        }
        """;

    /// <summary>Parsed once at first use; a JsonElement keeps its own document alive.</summary>
    public static JsonElement Kurzbrief { get; } = JsonDocument.Parse(KurzbriefJson).RootElement.Clone();

    /// <summary>The schema as text, for the prompt-only rung of the fallback ladder.</summary>
    public static string KurzbriefText => KurzbriefJson;
}

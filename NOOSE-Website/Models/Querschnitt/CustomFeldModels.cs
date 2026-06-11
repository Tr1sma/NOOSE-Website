using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Formular-/Eingabemodell zum Anlegen/Bearbeiten einer Custom-Feld-Definition (Admin).</summary>
public class CustomFeldDefinitionEingabe
{
    public string Name { get; set; } = string.Empty;
    public string EntitaetTyp { get; set; } = string.Empty;
    public CustomFeldTyp FeldTyp { get; set; } = CustomFeldTyp.Text;

    /// <summary>Auswahl-Optionen (eine pro Zeile), nur bei <see cref="CustomFeldTyp.Auswahl"/> relevant.</summary>
    public string? Optionen { get; set; }

    public bool Pflicht { get; set; }
    public int Reihenfolge { get; set; }
    public bool IstAktiv { get; set; } = true;
}

/// <summary>Eine Definition samt aktuellem Wert einer Akte – Grundlage für das Zusatzfelder-Panel.</summary>
public class CustomFeldWertAnzeige
{
    public required CustomFeldDefinition Definition { get; init; }

    /// <summary>Aktueller gespeicherter Wert (String) bzw. null, wenn noch nicht erfasst.</summary>
    public string? Wert { get; set; }
}

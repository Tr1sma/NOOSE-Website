namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Art einer generischen Verknüpfung. Trennt fachliche Organisations-Beziehungen (Konflikt/Bündnis)
/// von den übrigen, allgemeinen Verknüpfungen (Standard, z. B. manuelle Quer-Verweise oder die
/// automatischen „Fraktionskollege"/„Gruppenkollege"-Links).
/// </summary>
public enum VerknuepfungArt
{
    Standard = 0,
    Konflikt = 1,
    Buendnis = 2,
}

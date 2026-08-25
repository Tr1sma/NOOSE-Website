namespace NOOSE_Website.Models.Enums;

/// <summary>Which slice of the tip inbox a handler is looking at.</summary>
public enum TipInboxScope
{
    /// <summary>Untouched submissions.</summary>
    Eingang = 0,
    /// <summary>Being worked on or waiting for the citizen.</summary>
    Bearbeitung = 1,
    /// <summary>Decided.</summary>
    Erledigt = 2,
}

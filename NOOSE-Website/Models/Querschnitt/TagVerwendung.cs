using NOOSE_Website.Data.Entities.Querschnitt;

namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Ein Tag samt Anzahl seiner aktuellen Zuordnungen (für die Tag-Verwaltung <c>/tags</c>).</summary>
public record TagVerwendung(Tag Tag, int Anzahl);

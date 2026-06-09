namespace NOOSE_Website.Models.Enums;

/// <summary>Art einer protokollierten Änderung im Änderungs-Log (<c>AuditLog</c>).</summary>
public enum AuditAktion
{
    Erstellt = 0,
    Geaendert = 1,
    Geloescht = 2,
    Wiederhergestellt = 3,
}

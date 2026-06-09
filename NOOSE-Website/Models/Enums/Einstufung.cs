namespace NOOSE_Website.Models.Enums;

/// <summary>Sicherheitseinstufung einer Akte: Prüffall → Verdachtsfall → Gesichert staatsgefährdend.</summary>
public enum Einstufung
{
    Unbekannt = 0,
    Prueffall = 1,
    Verdachtsfall = 2,
    GesichertStaatsgefaehrdend = 3,
}

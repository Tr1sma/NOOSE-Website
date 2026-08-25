namespace NOOSE_Website.Models.Common;

/// <summary>Suggested roles for a person linked to a case; free text stays possible.</summary>
public static class CasePersonRoles
{
    public static readonly string[] Suggestions =
    {
        "Beschuldigter", "Tatverdächtiger", "Zeuge", "Geschädigter",
        "Informant", "Anzeigeerstatter", "Mittäter", "Kontaktperson",
    };
}

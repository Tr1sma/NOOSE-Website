namespace NOOSE_Website.Models.Enums;

/// <summary>Personnel template category; matches the three personnel-file record types.</summary>
public enum PersonnelTemplateKind
{
    Commendation = 0,
    Disciplinary = 1,
    Promotion = 2,
}

/// <summary>Display labels.</summary>
public static class PersonnelTemplateKindDisplay
{
    public static string Name(PersonnelTemplateKind kind) => kind switch
    {
        PersonnelTemplateKind.Commendation => "Belobigung",
        PersonnelTemplateKind.Disciplinary => "Disziplinarisch",
        PersonnelTemplateKind.Promotion => "Beförderung",
        _ => "—",
    };

    public static readonly PersonnelTemplateKind[] All =
    {
        PersonnelTemplateKind.Commendation,
        PersonnelTemplateKind.Disciplinary,
        PersonnelTemplateKind.Promotion,
    };
}

namespace NOOSE_Website.Models.Enums;

/// <summary>Question type in a recruiting test.</summary>
public enum TestQuestionType
{
    MultipleChoice = 0,
    YesNo = 1,
    FreeText = 2,
}

/// <summary>Display labels.</summary>
public static class TestQuestionTypeDisplay
{
    public static string Name(TestQuestionType type) => type switch
    {
        TestQuestionType.MultipleChoice => "Multiple Choice",
        TestQuestionType.YesNo => "Ja / Nein",
        TestQuestionType.FreeText => "Freitext",
        _ => "—",
    };
}

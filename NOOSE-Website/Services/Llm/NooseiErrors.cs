namespace NOOSE_Website.Services;

/// <summary>Maps any NOOSEI failure to a German message safe to show an agent. Detail stays in the log,
/// and no message ever names a model, a provider or an HTTP status of the upstream endpoint.</summary>
public static class NooseiErrors
{
    public static string Describe(Exception exception) => exception switch
    {
        LlmQuotaExceededException quota => quota.Message,
        UnauthorizedAccessException denied => denied.Message,
        LlmCapabilityException { SchemaRelated: true } =>
            "NOOSEI konnte die Antwort nicht im geforderten Format liefern. Bitte einen Admin informieren.",
        LlmCapabilityException =>
            "NOOSEI konnte diesmal nicht auf die Aktendatenbank zugreifen.",
        OperationCanceledException =>
            "NOOSEI hat nicht rechtzeitig geantwortet. Bitte später erneut versuchen.",
        InvalidOperationException known => known.Message,
        _ => "NOOSEI ist derzeit nicht erreichbar.",
    };
}

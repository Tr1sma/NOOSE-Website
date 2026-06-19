namespace NOOSE_Website.Authorization;

/// <summary>Relative routes a partner may open; everything else is blocked centrally (MainLayout/PrintLayout).</summary>
public static class PartnerRoutes
{
    private static readonly string[] AllowedPrefixes =
    {
        "personen",
        "fraktionen",
        "personengruppen",
        "parteien",
        "operationen",
        "vorgaenge",
        "taskforces",
        "dokumente",
        "gesetze",
        "suche",
    };

    private static readonly string[] BlockedSuffixes = { "/neu", "/bearbeiten", "/papierkorb" };

    /// <summary>True if a partner may open this relative path (dashboard and own profile always allowed).</summary>
    public static bool IsAllowed(string? relativePath)
    {
        var path = (relativePath ?? string.Empty).Split('?')[0].Split('#')[0].Trim('/').ToLowerInvariant();
        if (path.Length == 0 || path == "dashboard" || path == "profil" || path.StartsWith("profil/"))
        {
            return true;
        }
        if (BlockedSuffixes.Any(s => ("/" + path).EndsWith(s)))
        {
            return false;
        }
        return AllowedPrefixes.Any(p => path == p || path.StartsWith(p + "/"));
    }
}

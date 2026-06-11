using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace NOOSE_Website.Data;

/// <summary>
/// Wählt beim App-Start die zu verwendende Datenbank aus:
/// Bevorzugt wird die Produktiv-DB (Connection-String <c>ProductionConnection</c>).
/// Ist diese nicht erreichbar – etwa weil die App lokal läuft und der Hosting-MySQL-Server
/// von außen nicht zugänglich ist –, wird automatisch auf die lokale Entwicklungs-DB
/// (<c>DefaultConnection</c>) zurückgefallen.
///
/// Dadurch muss der Connection-String nie manuell zwischen „lokal" und „Server" umgestellt
/// werden: Auf dem Server greift die Produktiv-DB, zu Hause die lokale XAMPP-DB – derselbe Build.
/// </summary>
public static class DatabaseConnectionResolver
{
    // Kurzer Connect-Timeout NUR für die Erreichbarkeitsprüfung, damit der Fallback auf
    // die lokale DB nicht unnötig lange hängt. Im echten Betrieb gilt weiterhin der Timeout
    // aus dem ursprünglichen Connection-String.
    private const uint ProbeTimeoutSeconds = 5;

    /// <summary>
    /// Ermittelt den zu verwendenden Connection-String und die passende Server-Version.
    /// </summary>
    public static (string ConnectionString, ServerVersion ServerVersion) Resolve(
        IConfiguration configuration, ILogger logger)
    {
        var production = configuration.GetConnectionString("ProductionConnection");
        var local = configuration.GetConnectionString("DefaultConnection");

        // 1) Produktiv-DB bevorzugen, sofern konfiguriert UND erreichbar.
        if (!string.IsNullOrWhiteSpace(production) && TryReach(production, logger, out var prodVersion))
        {
            logger.LogInformation("Datenbank: Produktiv-Verbindung wird verwendet.");
            return (production, prodVersion);
        }

        // 2) Sonst lokale Entwicklungs-DB als Fallback.
        if (!string.IsNullOrWhiteSpace(local))
        {
            if (!string.IsNullOrWhiteSpace(production))
            {
                logger.LogWarning(
                    "Datenbank: Produktiv-DB nicht erreichbar – es wird auf die lokale Fallback-DB ausgewichen.");
            }
            else
            {
                logger.LogInformation("Datenbank: lokale Entwicklungs-DB wird verwendet (keine Produktiv-DB konfiguriert).");
            }

            return (local, ServerVersion.AutoDetect(local));
        }

        throw new InvalidOperationException(
            "Kein Connection-String konfiguriert. Bitte 'ConnectionStrings:ProductionConnection' (Server) " +
            "bzw. 'ConnectionStrings:DefaultConnection' (lokal) per User Secrets / Umgebungsvariable hinterlegen.");
    }

    /// <summary>
    /// Prüft mit kurzem Timeout, ob die DB erreichbar ist, und liest dabei die Server-Version.
    /// </summary>
    private static bool TryReach(string connectionString, ILogger logger, out ServerVersion version)
    {
        version = default!;
        try
        {
            // Connect-Timeout nur für die Prüfung kurz halten; der ermittelte Server-Version-Wert
            // gilt anschließend auch für den eigentlichen (langlebigen) Connection-String.
            var probe = new MySqlConnectionStringBuilder(connectionString)
            {
                ConnectionTimeout = ProbeTimeoutSeconds,
            }.ConnectionString;

            version = ServerVersion.AutoDetect(probe);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Datenbank: Erreichbarkeitsprüfung der Produktiv-DB fehlgeschlagen.");
            return false;
        }
    }
}

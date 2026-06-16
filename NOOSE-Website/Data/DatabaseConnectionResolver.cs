using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace NOOSE_Website.Data;

/// <summary>Selects DB at startup; falls back to local if production unreachable.</summary>
public static class DatabaseConnectionResolver
{
    // short timeout
    private const uint ProbeTimeoutSeconds = 5;

    /// <summary>Resolves connection string and server version.</summary>
    public static (string ConnectionString, ServerVersion ServerVersion) Resolve(
        IConfiguration configuration, ILogger logger)
    {
        var production = configuration.GetConnectionString("ProductionConnection");
        var local = configuration.GetConnectionString("DefaultConnection");

        // 1) prefer production
        if (!string.IsNullOrWhiteSpace(production) && TryReach(production, logger, out var prodVersion))
        {
            logger.LogInformation("DB: using production.");
            return (production, prodVersion);
        }

        // 2) local fallback
        if (!string.IsNullOrWhiteSpace(local))
        {
            if (!string.IsNullOrWhiteSpace(production))
            {
                logger.LogWarning(
                    "DB: production unreachable, using local fallback.");
            }
            else
            {
                logger.LogInformation("DB: using local (no production configured).");
            }

            return (local, ServerVersion.AutoDetect(local));
        }

        throw new InvalidOperationException(
            "No connection string configured. Set 'ConnectionStrings:ProductionConnection' or 'ConnectionStrings:DefaultConnection'.");
    }

    /// <summary>Probes DB reachability; reads server version.</summary>
    private static bool TryReach(string connectionString, ILogger logger, out ServerVersion version)
    {
        version = default!;
        try
        {
            // probe timeout
            var probe = new MySqlConnectionStringBuilder(connectionString)
            {
                ConnectionTimeout = ProbeTimeoutSeconds,
            }.ConnectionString;

            version = ServerVersion.AutoDetect(probe);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DB: production probe failed.");
            return false;
        }
    }
}

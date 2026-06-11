using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NOOSE_Website.Data;

/// <summary>
/// Design-Time-Factory für die EF-Core-Tools (<c>dotnet ef migrations add</c> /
/// <c>dotnet ef database update</c>).
///
/// Nutzt bewusst IMMER die lokale Entwicklungs-DB (<c>ConnectionStrings:DefaultConnection</c>
/// aus den User Secrets) – unabhängig von der Laufzeit-Auswahl in <see cref="DatabaseConnectionResolver"/>.
/// So können Migrationen niemals versehentlich gegen die Produktiv-DB ausgeführt werden, und die
/// EF-Tools brauchen keine Erreichbarkeitsprüfung gegen den Hosting-Server.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<AppDbContextDesignTimeFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Design-Time: 'ConnectionStrings:DefaultConnection' (lokale DB) fehlt in den User Secrets.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        return new AppDbContext(options);
    }
}

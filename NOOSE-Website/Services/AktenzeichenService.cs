using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAktenzeichenService" />
public class AktenzeichenService : IAktenzeichenService
{
    public async Task<string> NaechstesAsync(AppDbContext db, string praefix, CancellationToken cancellationToken = default)
    {
        var jahr = DateTime.UtcNow.Year;
        // Atomarer, race-sicherer Zähler-Inkrement (MariaDB/MySQL-nativ) auf dem Kontext des Aufrufers,
        // damit Zähler und Akte in einer Transaktion gemeinsam committen.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO AktenzeichenZaehler (Praefix, Jahr, LetzteNummer) VALUES ({praefix}, {jahr}, 1) ON DUPLICATE KEY UPDATE LetzteNummer = LetzteNummer + 1;",
            cancellationToken);
        var nummer = (await db.AktenzeichenZaehler.AsNoTracking()
            .FirstAsync(z => z.Praefix == praefix && z.Jahr == jahr, cancellationToken)).LetzteNummer;
        return $"NOOSE-{praefix}-{jahr}-{nummer:0000}";
    }
}

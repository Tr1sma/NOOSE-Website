using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAktenzeichenService" />
public class CaseNumberService : ICaseNumberService
{
    public async Task<string> NextAsync(AppDbContext db, string prefix, CancellationToken cancellationToken = default)
    {
        // Race-Sicherheit setzt eine umschließende Transaktion des Aufrufers voraus: Erst der Commit gibt den
        // Row-Lock auf dem Zähler frei. Ohne Transaktion (Autocommit) könnten zwei parallele Anlagen dieselbe
        // Nummer lesen → Unique-Index-Crash. Daher Fail-fast, statt die Race unbemerkt wieder zu öffnen.
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Aktenzeichen-Vergabe erfordert eine umschließende Transaktion des Aufrufers (Race-Sicherheit).");
        }

        var year = DateTime.UtcNow.Year;
        // Atomarer, race-sicherer Zähler-Inkrement (MariaDB/MySQL-nativ) auf dem Kontext des Aufrufers,
        // damit Zähler und Akte in einer Transaktion gemeinsam committen.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO AktenzeichenZaehler (Praefix, Jahr, LetzteNummer) VALUES ({prefix}, {year}, 1) ON DUPLICATE KEY UPDATE LetzteNummer = LetzteNummer + 1;",
            cancellationToken);
        var number = (await db.CaseNumberCounter.AsNoTracking()
            .FirstAsync(z => z.Prefix == prefix && z.Year == year, cancellationToken)).LastNumber;
        return $"NOOSE-{prefix}-{year}-{number:0000}";
    }
}

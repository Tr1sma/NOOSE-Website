using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Antraege;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAntragService" />
public class AntragService(IDbContextFactory<AppDbContext> dbFactory) : IAntragService
{
    public async Task<bool> HatOffenenAntragAsync(string zielTyp, string zielId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Antraege.AnyAsync(
            a => a.ZielTyp == zielTyp && a.ZielId == zielId && a.Status == AntragStatus.Beantragt, cancellationToken);
    }

    public async Task HochstufungBeantragenAsync(string zielTyp, string zielId, string zielBezeichnung, Einstufung ziel,
        string begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        // Ein Antrag ist nur für die höchste Einstufung nötig – die darunterliegenden setzt jeder Agent direkt.
        if (ziel != Einstufung.GesichertStaatsgefaehrdend)
        {
            throw new InvalidOperationException("Ein Antrag ist nur für die Einstufung „Gesichert staatsgefährdend“ erforderlich.");
        }
        if (string.IsNullOrWhiteSpace(begruendung))
        {
            throw new InvalidOperationException("Bitte eine Begründung für den Hochstufungs-Antrag angeben.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Nur auf einer für den Antragsteller sichtbaren Akte beantragbar (Verschlusssache-/Papierkorb-Schutz).
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, zielTyp, zielId, handelnder.IstFuehrung(), cancellationToken))
        {
            throw new InvalidOperationException("Die Ziel-Akte wurde nicht gefunden.");
        }

        // Dedup: pro Ziel nur ein offener Antrag.
        if (await db.Antraege.AnyAsync(a => a.ZielTyp == zielTyp && a.ZielId == zielId && a.Status == AntragStatus.Beantragt, cancellationToken))
        {
            throw new InvalidOperationException("Für diese Akte läuft bereits ein Hochstufungs-Antrag.");
        }

        db.Antraege.Add(new Antrag
        {
            Typ = AntragTyp.Hochstufung,
            ZielTyp = zielTyp,
            ZielId = zielId,
            ZielBezeichnung = zielBezeichnung.Trim(),
            ZielEinstufung = ziel,
            Begruendung = begruendung.Trim(),
            Status = AntragStatus.Beantragt,
            AntragstellerName = handelnder.GetCodename(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Antrag>> GetOffeneAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var offen = await db.Antraege
            .Where(a => a.Status == AntragStatus.Beantragt)
            .OrderBy(a => a.ErstelltAm)
            .ToListAsync(cancellationToken);

        // VS-Schutz: nur Anträge zurückgeben, deren Ziel-Akte für den Betrachter sichtbar ist (falls eine
        // Akte nach Antragstellung als Verschlusssache eingestuft oder in den Papierkorb verschoben wurde).
        var sichtbar = new List<Antrag>();
        foreach (var a in offen)
        {
            if (await Sichtbarkeit.IstAkteSichtbarAsync(db, a.ZielTyp, a.ZielId, istFuehrung, cancellationToken))
            {
                sichtbar.Add(a);
            }
        }
        return sichtbar;
    }

    public async Task<int> GetOffeneAnzahlAsync(bool istFuehrung, CancellationToken cancellationToken = default)
        => (await GetOffeneAsync(istFuehrung, cancellationToken)).Count;

    public async Task<List<Antrag>> GetMeineAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Antraege
            .Where(a => a.ErstelltVonId == agentId)
            .OrderByDescending(a => a.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task EntscheidenAsync(string antragId, bool genehmigt, string? notiz, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeHoechsteEinstufung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var antrag = await db.Antraege.FirstOrDefaultAsync(a => a.Id == antragId, cancellationToken)
            ?? throw new InvalidOperationException("Antrag nicht gefunden.");
        if (antrag.Status != AntragStatus.Beantragt)
        {
            throw new InvalidOperationException("Dieser Antrag wurde bereits entschieden.");
        }

        // Verschlusssache-/Papierkorb-Schutz wie in allen direkt setzenden Diensten (PersonService etc.):
        // die Ziel-Akte muss für den Entscheider sichtbar sein. Greift, falls die Akte nach Antragstellung
        // als Verschlusssache eingestuft oder gelöscht wurde – der schreibende Dienst setzt das selbst durch.
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, antrag.ZielTyp, antrag.ZielId, handelnder.IstFuehrung(), cancellationToken))
        {
            throw new InvalidOperationException("Die Ziel-Akte ist nicht (mehr) für dich sichtbar.");
        }

        if (genehmigt)
        {
            // Einstufung der Ziel-Akte setzen – bewusst OHNE Rang-Gate (ein autorisierter Senior+ genehmigt) –
            // und im polymorphen Einstufungs-Verlauf mit Antrags-Bezug protokollieren.
            if (!await EinstufungAnZielSetzenAsync(db, antrag, cancellationToken))
            {
                throw new InvalidOperationException("Die Ziel-Akte ist nicht mehr vorhanden.");
            }
            var eintrag = EinstufungHelfer.Eintrag(antrag.ZielTyp, antrag.ZielId, antrag.ZielEinstufung, antrag.Begruendung, handelnder);
            eintrag.AntragId = antrag.Id;
            db.EinstufungVerlauf.Add(eintrag);
        }

        antrag.Status = genehmigt ? AntragStatus.Genehmigt : AntragStatus.Abgelehnt;
        antrag.EntscheiderName = handelnder.GetCodename();
        antrag.EntschiedenAm = DateTime.UtcNow;
        antrag.Entscheidungsnotiz = string.IsNullOrWhiteSpace(notiz) ? null : notiz.Trim();

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    /// <summary>Setzt die Einstufung der polymorphen Ziel-Akte. Liefert false, wenn die Akte nicht (mehr) existiert.</summary>
    private static async Task<bool> EinstufungAnZielSetzenAsync(AppDbContext db, Antrag antrag, CancellationToken cancellationToken)
    {
        switch (antrag.ZielTyp)
        {
            case nameof(Person):
                var person = await db.Personen.FirstOrDefaultAsync(x => x.Id == antrag.ZielId, cancellationToken);
                if (person is null) return false;
                person.Einstufung = antrag.ZielEinstufung;
                return true;
            case nameof(Fraktion):
                var fraktion = await db.Fraktionen.FirstOrDefaultAsync(x => x.Id == antrag.ZielId, cancellationToken);
                if (fraktion is null) return false;
                fraktion.Einstufung = antrag.ZielEinstufung;
                return true;
            case nameof(Personengruppe):
                var gruppe = await db.Personengruppen.FirstOrDefaultAsync(x => x.Id == antrag.ZielId, cancellationToken);
                if (gruppe is null) return false;
                gruppe.Einstufung = antrag.ZielEinstufung;
                return true;
            case nameof(Partei):
                var partei = await db.Parteien.FirstOrDefaultAsync(x => x.Id == antrag.ZielId, cancellationToken);
                if (partei is null) return false;
                partei.Einstufung = antrag.ZielEinstufung;
                return true;
            case nameof(Operation):
                var operation = await db.Operationen.FirstOrDefaultAsync(x => x.Id == antrag.ZielId, cancellationToken);
                if (operation is null) return false;
                operation.Einstufung = antrag.ZielEinstufung;
                return true;
            case nameof(Vorgang):
                var vorgang = await db.Vorgaenge.FirstOrDefaultAsync(x => x.Id == antrag.ZielId, cancellationToken);
                if (vorgang is null) return false;
                vorgang.Einstufung = antrag.ZielEinstufung;
                return true;
            default:
                return false;
        }
    }
}

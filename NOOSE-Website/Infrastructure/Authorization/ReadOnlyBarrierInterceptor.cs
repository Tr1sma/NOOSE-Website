using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;

namespace NOOSE_Website.Infrastructure.Authorization;

/// <summary>
/// Harte, zentrale Schreibsperre für die Nur-Lese-Aufsicht (TeamLeitung ohne Admin). Vetiert JEDEN
/// EF-Speichervorgang eines Nur-Lesers, sobald eine nicht freigegebene Entität geschrieben würde –
/// unabhängig davon, welcher Dienst/welche Seite den Vorgang ausgelöst hat. Deckt damit alle
/// Schreibpfade über <see cref="DbContext.SaveChangesAsync(System.Threading.CancellationToken)"/> ab,
/// inklusive des geteilten scoped Kontexts von <c>AgentVerwaltungService</c> (gleiche DbContext-Options).
///
/// Wird ZUERST in der Interceptor-Kette registriert (vor Audit/Watchlist), damit der Veto greift, bevor
/// der Audit-Interceptor stempelt oder seinen re-entranten AuditLog-Save auslöst.
///
/// Grenze (wie beim <see cref="AuditSaveChangesInterceptor"/>): der synchrone Pfad kann den Agent im
/// interaktiven Circuit nicht auflösen (nur HttpContext). In der Praxis schreiben alle Dienste async,
/// daher greift der async-Pfad. Zusätzliche Absicherung liefern die <c>Berechtigung.VerlangeSchreibrecht</c>-
/// Guards in der Service-Schicht (u. a. für die Roh-SQL-/Bulk-Pfade, die SaveChanges umgehen).
/// </summary>
public class ReadOnlyBarrierInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    // Reine Protokoll-/Lesenebenwirkungs-Entitäten, die auch ein Nur-Leser beim bloßen Betrachten schreibt:
    // Audit-/Zugriffsprotokoll (Pflicht-Logging) und das Markieren eigener Benachrichtigungen als gelesen.
    private static readonly HashSet<Type> Whitelist =
    [
        typeof(AuditLog),
        typeof(AccessLog),
        typeof(Notification),
    ];

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var user = await currentUserService.GetAsync();
        Require(eventData.Context, user);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        // Synchroner Pfad: den Agent synchron (HttpContext-only) ermitteln, statt blockierend auf den async
        // AuthenticationStateProvider zu warten (Deadlock-/Starvation-Risiko) – analog zum Audit-Interceptor.
        var user = currentUserService.Get();
        Require(eventData.Context, user);
        return base.SavingChanges(eventData, result);
    }

    private static void Require(DbContext? context, CurrentUserInfo user)
    {
        if (context is null || !user.IsOnlyReader)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }
            if (!Whitelist.Contains(entry.Entity.GetType()))
            {
                throw new UnauthorizedAccessException(
                    "Nur-Lese-Modus: Änderungen sind in der Aufsichtsrolle nicht möglich.");
            }
        }
    }
}

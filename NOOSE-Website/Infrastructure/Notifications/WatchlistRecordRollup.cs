using Microsoft.Extensions.Logging;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Announcements;
using NOOSE_Website.Data.Entities.Requests;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Watchlist;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Infrastructure.Notifications;

/// <summary>
/// Rollt eine geänderte Entität auf die Akte(n) hoch, der man folgen kann – die „Kind → Eltern"-Abbildung der
/// Watchlist. Bewusst eine <b>Allowlist</b>: nur die hier bekannten Akten-, Kind- und Querschnitts-Typen liefern
/// eine Eltern-Akte; alles andere (inkl. Benachrichtigung/Watchlist/AuditLog selbst) liefert nichts – damit sind
/// Benachrichtigungs-Schleifen strukturell unmöglich. Für einen auditierbaren Typ, der hier (noch) fehlt, wird
/// fail-loud gewarnt (nicht geworfen – der Speichervorgang darf nie gefährdet werden).
/// </summary>
public static class WatchlistRecordRollup
{
    public static IReadOnlyList<(string Type, string Id)> Map(object entity, ILogger logger)
    {
        switch (entity)
        {
            // ---- Akten selbst ----
            case Person p: return One(nameof(Person), p.Id);
            case Faction f: return One(nameof(Faction), f.Id);
            case PersonGroup g: return One(nameof(PersonGroup), g.Id);
            case Party pa: return One(nameof(Party), pa.Id);
            case Operation o: return One(nameof(Operation), o.Id);
            case Case v: return One(nameof(Case), v.Id);
            case Taskforce t: return One(nameof(Taskforce), t.Id);

            // ---- Typisierte Kind-Daten → ihre Eltern-Akte ----
            case PersonDoc d: return One(nameof(Person), d.PersonId);
            case Observation ob: return One(nameof(Person), ob.PersonId);
            case FactionMember fm: return One(nameof(Faction), fm.FactionId);
            case FactionAgent fa: return One(nameof(Faction), fa.FactionId);
            case PersonGroupMember gm: return One(nameof(PersonGroup), gm.PersonGroupId);
            case PersonGroupAgent ga: return One(nameof(PersonGroup), ga.PersonGroupId);
            case PartyMember pm: return One(nameof(Party), pm.PartyId);
            case PartyAgent paa: return One(nameof(Party), paa.PartyId);
            case OperationAgent oa: return One(nameof(Operation), oa.OperationId);
            case CaseAgent va: return One(nameof(Case), va.CaseId);
            case TaskforceAgent ta: return One(nameof(Taskforce), ta.TaskforceId);
            case TaskforceMessage tn: return One(nameof(Taskforce), tn.TaskforceId);
            case AgentNote av: return One(nameof(Agent), av.AgentId);
            case AgentPromotionRequest from: return One(nameof(Agent), from.AgentId);
            // Die Personalakte IST der Agent (Identity-User): direkte Stammdaten-/Rang-/Status-Änderungen am Agent
            // (Rang, Sperre, Codename, Admin/TRU, Namensänderung) sowie der Dienstgrad-Verlauf melden ebenfalls an
            // dessen Folger – sonst meldete eine Beförderung per Antrag, eine per Rangänderung aber nicht.
            case Agent ag: return One(nameof(Agent), ag.Id);
            case AgentRankHistory adv: return One(nameof(Agent), adv.AgentId);

            // ---- Querschnitt: polymorphes Ziel direkt verwenden ----
            case Comment k: return One(k.EntityType, k.EntityId);
            case Source q: return One(q.EntityType, q.EntityId);
            case TagMapping tz: return One(tz.EntityType, tz.EntityId);

            // ---- Beziehungen → beide Seiten ----
            case Link vk: return Two((vk.SourceType, vk.SourceId), (vk.TargetType, vk.TargetId));
            case PersonRelation pb: return Two((nameof(Person), pb.PersonAId), (nameof(Person), pb.PersonBId));

            // ---- Bewusst ignoriert: auditierbar, aber keine folgbare Akte (verhindert Schleifen/Rauschen) ----
            // Aufgaben laufen über ein eigenes Team-Board (keine Watchlist) → nicht folgbar; ein Verknüpfungs-Edit
            // Akte↔Aufgabe rollt weiter korrekt auf die andere (folgbare) Seite, die Aufgabe-Seite hat keine Folger.
            case Job:
            case JobAssignment:
            // Ankündigungen/Broadcasts laufen über das Schwarze Brett (eigene Sichtbarkeit) + die Glocke,
            // sind aber keine folgbare Akte → nicht in die Watchlist hochrollen.
            case Announcement:
            case AnnouncementAcknowledgment:
            case Request:
            case Tag:
            case Notification:
            case SavedSearch:
            case WatchlistEntry:
                return Array.Empty<(string, string)>();

            default:
                // Ein NEUER auditierbarer Typ, der hier fehlt, würde Folger stillschweigend nicht erreichen → warnen.
                if (entity is IAuditable)
                {
                    logger.LogWarning(
                        "Watchlist-Rollup kennt den auditierbaren Typ {Typ} nicht – Änderungen daran benachrichtigen keine Folger.",
                        entity.GetType().Name);
                }
                return Array.Empty<(string, string)>();
        }
    }

    private static IReadOnlyList<(string, string)> One(string? type, string? id)
        => string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id)
            ? Array.Empty<(string, string)>()
            : new[] { (type, id) };

    private static IReadOnlyList<(string, string)> Two((string? Type, string? Id) a, (string? Type, string? Id) b)
    {
        var list = new List<(string, string)>(2);
        if (!string.IsNullOrWhiteSpace(a.Type) && !string.IsNullOrWhiteSpace(a.Id))
        {
            list.Add((a.Type, a.Id));
        }
        if (!string.IsNullOrWhiteSpace(b.Type) && !string.IsNullOrWhiteSpace(b.Id))
        {
            list.Add((b.Type, b.Id));
        }
        return list;
    }
}

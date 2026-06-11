using Microsoft.Extensions.Logging;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Ankuendigungen;
using NOOSE_Website.Data.Entities.Antraege;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Benachrichtigungen;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personal;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;
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
public static class WatchlistAkteRollup
{
    public static IReadOnlyList<(string Typ, string Id)> Map(object entity, ILogger logger)
    {
        switch (entity)
        {
            // ---- Akten selbst ----
            case Person p: return Eine(nameof(Person), p.Id);
            case Fraktion f: return Eine(nameof(Fraktion), f.Id);
            case Personengruppe g: return Eine(nameof(Personengruppe), g.Id);
            case Partei pa: return Eine(nameof(Partei), pa.Id);
            case Operation o: return Eine(nameof(Operation), o.Id);
            case Vorgang v: return Eine(nameof(Vorgang), v.Id);
            case Taskforce t: return Eine(nameof(Taskforce), t.Id);

            // ---- Typisierte Kind-Daten → ihre Eltern-Akte ----
            case PersonDok d: return Eine(nameof(Person), d.PersonId);
            case Observation ob: return Eine(nameof(Person), ob.PersonId);
            case FraktionMitglied fm: return Eine(nameof(Fraktion), fm.FraktionId);
            case FraktionAgent fa: return Eine(nameof(Fraktion), fa.FraktionId);
            case PersonengruppeMitglied gm: return Eine(nameof(Personengruppe), gm.PersonengruppeId);
            case PersonengruppeAgent ga: return Eine(nameof(Personengruppe), ga.PersonengruppeId);
            case ParteiMitglied pm: return Eine(nameof(Partei), pm.ParteiId);
            case ParteiAgent paa: return Eine(nameof(Partei), paa.ParteiId);
            case OperationAgent oa: return Eine(nameof(Operation), oa.OperationId);
            case VorgangAgent va: return Eine(nameof(Vorgang), va.VorgangId);
            case TaskforceAgent ta: return Eine(nameof(Taskforce), ta.TaskforceId);
            case TaskforceNachricht tn: return Eine(nameof(Taskforce), tn.TaskforceId);
            case AgentVermerk av: return Eine(nameof(Agent), av.AgentId);
            case AgentBefoerderungsantrag ab: return Eine(nameof(Agent), ab.AgentId);
            // Die Personalakte IST der Agent (Identity-User): direkte Stammdaten-/Rang-/Status-Änderungen am Agent
            // (Rang, Sperre, Codename, Admin/TRU, Namensänderung) sowie der Dienstgrad-Verlauf melden ebenfalls an
            // dessen Folger – sonst meldete eine Beförderung per Antrag, eine per Rangänderung aber nicht.
            case Agent ag: return Eine(nameof(Agent), ag.Id);
            case AgentDienstgradVerlauf adv: return Eine(nameof(Agent), adv.AgentId);

            // ---- Querschnitt: polymorphes Ziel direkt verwenden ----
            case Kommentar k: return Eine(k.EntitaetTyp, k.EntitaetId);
            case Quelle q: return Eine(q.EntitaetTyp, q.EntitaetId);
            case TagZuordnung tz: return Eine(tz.EntitaetTyp, tz.EntitaetId);

            // ---- Beziehungen → beide Seiten ----
            case Verknuepfung vk: return Zwei((vk.VonTyp, vk.VonId), (vk.NachTyp, vk.NachId));
            case PersonBeziehung pb: return Zwei((nameof(Person), pb.PersonAId), (nameof(Person), pb.PersonBId));

            // ---- Bewusst ignoriert: auditierbar, aber keine folgbare Akte (verhindert Schleifen/Rauschen) ----
            // Aufgaben laufen über ein eigenes Team-Board (keine Watchlist) → nicht folgbar; ein Verknüpfungs-Edit
            // Akte↔Aufgabe rollt weiter korrekt auf die andere (folgbare) Seite, die Aufgabe-Seite hat keine Folger.
            case Aufgabe:
            case AufgabeZuweisung:
            // Ankündigungen/Broadcasts laufen über das Schwarze Brett (eigene Sichtbarkeit) + die Glocke,
            // sind aber keine folgbare Akte → nicht in die Watchlist hochrollen.
            case Ankuendigung:
            case AnkuendigungQuittierung:
            case Antrag:
            case Tag:
            case Benachrichtigung:
            case GespeicherteSuche:
            case WatchlistEintrag:
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

    private static IReadOnlyList<(string, string)> Eine(string? typ, string? id)
        => string.IsNullOrWhiteSpace(typ) || string.IsNullOrWhiteSpace(id)
            ? Array.Empty<(string, string)>()
            : new[] { (typ, id) };

    private static IReadOnlyList<(string, string)> Zwei((string? Typ, string? Id) a, (string? Typ, string? Id) b)
    {
        var list = new List<(string, string)>(2);
        if (!string.IsNullOrWhiteSpace(a.Typ) && !string.IsNullOrWhiteSpace(a.Id))
        {
            list.Add((a.Typ, a.Id));
        }
        if (!string.IsNullOrWhiteSpace(b.Typ) && !string.IsNullOrWhiteSpace(b.Id))
        {
            list.Add((b.Typ, b.Id));
        }
        return list;
    }
}

using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Kasse;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Services;

/// <summary>Demo data for the citizen side: profiles, tips with their threads, tickets, objections and paid rewards.</summary>
public partial class DemoDataService
{
    /// <summary>Citizen profiles; the account rows already exist at this point.</summary>
    private static async Task<(Dictionary<string, BuergerProfil> Map, int Added)> SeedCitizenProfilesAsync(
        AppDbContext db, CancellationToken ct)
    {
        var map = (await db.BuergerProfile.IgnoreQueryFilters().ToListAsync(ct))
            .GroupBy(p => p.UserId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var added = 0;
        foreach (var spec in Citizens)
        {
            if (map.ContainsKey(spec.Id))
            {
                continue;
            }
            var profile = new BuergerProfil
            {
                UserId = spec.Id,
                FirstName = spec.FirstName,
                LastName = spec.LastName,
                ConfirmedTips = spec.ConfirmedTips,
                IsBlocked = spec.Blocked,
                BlockedReason = spec.BlockReason,
                BlockedById = spec.Blocked ? DemoIdentity.AgentId : null,
                BlockedAt = spec.Blocked ? DateTime.UtcNow.AddDays(-14) : null,
            };
            db.BuergerProfile.Add(profile);
            map[spec.Id] = profile;
            added++;
        }
        return (map, added);
    }

    private sealed record TipSpec(
        int CitizenIndex, int NoticeIndex, TipStatus Status, bool Anonymous, string Text,
        int DaysAgo, string[] CitizenThread, string[] InternalThread);

    private static readonly TipSpec[] TipSpecs =
    [
        new(0, 0, TipStatus.Bestaetigt, false,
            "Die gesuchte Person war gestern gegen 22:30 Uhr am Pier von Elysian Island. Sie stieg in einen dunklen "
            + "Kombi und fuhr Richtung Norden; das Kennzeichen begann mit 45HGF. Zwei weitere Personen warteten im Fahrzeug.",
            9,
            [
                "Danke für Ihren Hinweis. Können Sie das vollständige Kennzeichen nennen?",
                "Leider nicht, es war zu dunkel. Der Wagen hatte aber einen auffälligen Aufkleber auf der Heckscheibe.",
                "Vielen Dank, das hilft uns weiter. Wir melden uns, falls wir Rückfragen haben.",
            ],
            [
                "Kennzeichenfragment deckt sich mit dem Fahrzeug aus dem Steckbrief. Observation am Pier angeordnet.",
                "Bestätigt: Fahrzeug am Folgetag im Hafenbereich gesichtet. Hinweis als bestätigt gewertet.",
            ]),
        // points at the captured notice: paying the reward moves its shares out of the advertised sum
        new(3, 6, TipStatus.FuehrteZurErgreifung, false,
            "Der Gesuchte hält sich regelmäßig in einer Werkstatt an der Popular Street auf. Ich habe ihn dort heute "
            + "Morgen gesehen, er kam mit zwei anderen Männern aus dem Hinterhof.",
            21,
            [
                "Vielen Dank. Bitte nähern Sie sich der Person nicht und beobachten Sie nichts weiter.",
                "Verstanden, ich halte mich raus.",
                "Der Hinweis hat zur Ergreifung geführt. Die ausgelobte Belohnung wird ausgezahlt.",
            ],
            [
                "Adresse deckt sich mit der Observation vom Vortag. Zugriff für morgen früh vorbereitet.",
                "Zugriff erfolgreich. Belohnung freigegeben.",
            ]),
        new(1, 2, TipStatus.InPruefung, false,
            "In der Tiefgarage an der Vinewood Boulevard steht seit drei Tagen ein Sportwagen mit laufendem Motor. "
            + "Es wechseln ständig verschiedene Personen, die Taschen ein- und ausladen.",
            4,
            ["Danke für die Meldung. In welchem Stockwerk befindet sich das Fahrzeug?"],
            ["Adresse an die Observation weitergegeben. Rückmeldung des Bürgers abwarten."]),
        new(2, 3, TipStatus.Rueckfrage, true,
            "Am Containerhafen werden nachts Menschen in einen LKW verladen. Ich möchte anonym bleiben, "
            + "ich wohne in der Nähe und habe Angst.",
            6,
            ["Ihre Meldung wird vertraulich behandelt. Können Sie den Wochentag und die Uhrzeit eingrenzen?"],
            ["Anonymität zugesagt. Bei Bestätigung Rücksprache mit der Führung wegen Auflösung."]),
        new(4, 5, TipStatus.Verworfen, false,
            "Ich glaube, mein Nachbar ist die gesuchte Person auf der Fahndungsseite. Er sieht dem Bild sehr ähnlich.",
            12,
            [
                "Vielen Dank für Ihre Meldung. Nach Prüfung handelt es sich nicht um die gesuchte Person.",
            ],
            ["Ähnlichkeit geprüft, Personalien weichen ab. Kein Anfangsverdacht — verworfen."]),
        new(0, 4, TipStatus.Neu, false,
            "Rauchentwicklung an einem Lagerschuppen in Sandy Shores, dazu ein flüchtendes Fahrzeug. "
            + "Es könnte mit den Bränden der letzten Wochen zusammenhängen.",
            1, [], []),
        new(1, -1, TipStatus.Neu, true,
            "In einer Bar an der Vespucci Beach wird offen mit Waffen gehandelt. Ich möchte anonym bleiben.",
            2, [], []),
    ];

    private async Task<int> SeedTipsAsync(
        AppDbContext db, Dictionary<string, BuergerProfil> citizens, List<SeededNotice> notices,
        List<TimeStamp> stamps, CancellationToken ct)
    {
        var existing = (await db.Hinweise.IgnoreQueryFilters()
                .Select(h => new { h.CitizenProfileId, h.Text }).ToListAsync(ct))
            .Select(h => h.CitizenProfileId + "|" + h.Text)
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        var order = Citizens.Select(c => citizens.GetValueOrDefault(c.Id)).ToList();

        foreach (var spec in TipSpecs)
        {
            var citizen = order.ElementAtOrDefault(spec.CitizenIndex);
            if (citizen is null || !existing.Add(citizen.Id + "|" + spec.Text))
            {
                continue;
            }
            var link = spec.NoticeIndex >= 0 ? notices.ElementAtOrDefault(spec.NoticeIndex) : null;
            var created = DateTime.UtcNow.AddDays(-spec.DaysAgo);

            var tip = new Hinweis
            {
                CaseNumber = await caseNumbers.NextAsync(db, TipPrefix, ct),
                Kind = TipKind.Beobachtung,
                CitizenProfileId = citizen.Id,
                WantsAnonymity = spec.Anonymous,
                WantedId = link?.Notice.Id,
                Text = spec.Text,
                Status = spec.Status,
                HandlerId = spec.Status == TipStatus.Neu ? null : DemoIdentity.AgentId,
                Priority = TipPriority.Compute(link?.Bounty ?? 0m, link?.Hazard, citizen.ConfirmedTips),
                AgentLastReadAt = spec.Status == TipStatus.Neu ? null : created.AddHours(3),
                CitizenLastReadAt = created.AddHours(4),
            };
            db.Hinweise.Add(tip);
            stamps.Add(new TimeStamp(nameof(Hinweis), tip.Id, created));
            added++;

            added += AddTipMessages(db, tip, created, spec.CitizenThread, TipMessageAudience.Buerger, stamps);
            added += AddTipMessages(db, tip, created, spec.InternalThread, TipMessageAudience.Intern, stamps);
        }
        return added;
    }

    /// <summary>Citizen lines never carry an agent; internal ones always do.</summary>
    private static int AddTipMessages(
        AppDbContext db, Hinweis tip, DateTime created, string[] texts,
        TipMessageAudience audience, List<TimeStamp> stamps)
    {
        var added = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            var citizenLine = audience == TipMessageAudience.Buerger && i % 2 == 1;
            var message = new HinweisNachricht
            {
                HinweisId = tip.Id,
                Audience = audience,
                Text = texts[i],
                AuthorIsCitizen = citizenLine,
                AuthorAgentId = audience == TipMessageAudience.Intern ? DemoIdentity.AgentId : null,
            };
            db.HinweisNachrichten.Add(message);
            stamps.Add(new TimeStamp(nameof(HinweisNachricht), message.Id, created.AddHours(4 + (i * 6))));
            added++;
        }
        return added;
    }

    /// <summary>Pays a reward for every solved tip that has none yet. A separate pass, so it also repairs a database seeded before the payout existed.</summary>
    private async Task<int> SeedRewardsAsync(AppDbContext db, CancellationToken ct)
    {
        var solved = await db.Hinweise
            .Where(h => h.Status == TipStatus.FuehrteZurErgreifung && h.WantedId != null)
            .Select(h => new { h.Id, h.WantedId })
            .ToListAsync(ct);
        if (solved.Count == 0)
        {
            return 0;
        }
        var alreadyPaid = (await db.HinweisBelohnungen.Select(b => b.TipId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var entry in solved.Where(s => !alreadyPaid.Contains(s.Id)))
        {
            added += await PayRewardAsync(db, entry.Id, entry.WantedId!, ct);
        }
        return added;
    }

    private async Task<int> PayRewardAsync(AppDbContext db, string tipId, string wantedId, CancellationToken ct)
    {
        var shares = await db.FahndungKopfgeldAnteile
            .Where(s => s.WantedId == wantedId && s.Status != BountyShareStatus.Ausgezahlt
                && s.Status != BountyShareStatus.Zurueckgezogen)
            .ToListAsync(ct);
        if (shares.Count == 0)
        {
            return 0;
        }
        var caseNumber = await db.OeffentlicheFahndungen.IgnoreQueryFilters()
            .Where(w => w.Id == wantedId).Select(w => w.CaseNumber).FirstOrDefaultAsync(ct);
        var paidAt = DateTime.UtcNow.AddDays(-19);

        var receipt = await caseNumbers.NextAsync(db, ReceiptPrefix, ct);
        var added = 0;
        foreach (var share in shares)
        {
            var needsBooking = share.Origin == BountyOrigin.NooseKasse || share.Status == BountyShareStatus.Gesichert;
            string? bookingId = null;
            if (needsBooking)
            {
                var payout = new KassenBuchung
                {
                    CaseNumber = await caseNumbers.NextAsync(db, KassePrefix, ct),
                    Account = share.Account ?? KassenKonto.Gruengeld,
                    Kind = KassenBuchungArt.Auszahlung,
                    Amount = share.Amount,
                    Reason = $"Belohnung {receipt} · {caseNumber}",
                    BookedById = DemoIdentity.AgentId,
                    Timestamp = paidAt,
                };
                db.KassenBuchungen.Add(payout);
                bookingId = payout.Id;
                added++;
            }
            db.HinweisBelohnungen.Add(new HinweisBelohnung
            {
                ReceiptNumber = receipt,
                TipId = tipId,
                ShareId = share.Id,
                Amount = share.Amount,
                KassenBuchungId = bookingId,
                SelfPaidAt = needsBooking ? null : paidAt,
                PaidAt = paidAt,
            });
            share.Status = BountyShareStatus.Ausgezahlt;
            added++;
        }
        return added;
    }

    private sealed record TicketSpec(
        int CitizenIndex, TicketArt Kind, string Subject, TicketStatus Status, int DaysAgo,
        (bool FromCitizen, string Text)[] CitizenThread, string[] InternalThread);

    private static readonly TicketSpec[] TicketSpecs =
    [
        new(2, TicketArt.Fuehrungsebene, "Frage zur Löschung meiner Daten", TicketStatus.Geschlossen, 18,
            [
                (true, "Guten Tag, ich hatte vor einigen Wochen einen Hinweis eingereicht. Wie lange speichern Sie meine Angaben?"),
                (false, "Guten Tag, Ihre Angaben werden für die Dauer des Vorgangs gespeichert und danach gelöscht. "
                    + "Details finden Sie in unserer Datenschutzerklärung."),
                (true, "Vielen Dank für die schnelle Antwort."),
            ],
            ["Standardauskunft erteilt, Vorgang geschlossen."]),
        new(0, TicketArt.Fuehrungsebene, "Rückfrage zu einer Ausschreibung", TicketStatus.WartetAufBuerger, 3,
            [
                (true, "Auf der Fahndungsseite steht eine Person, die ich zu kennen glaube. Ist die Ausschreibung noch aktuell?"),
                (false, "Vielen Dank für Ihre Nachricht. Die Ausschreibung ist weiterhin gültig. "
                    + "Bitte nutzen Sie für konkrete Beobachtungen das Hinweisformular."),
            ],
            ["Bürger auf das Hinweisformular verwiesen. Rückmeldung abwarten."]),
        new(4, TicketArt.Fuehrungsebene, "Beschwerde über eine Kontrolle", TicketStatus.InBearbeitung, 5,
            [
                (true, "Ich wurde gestern Abend an der Route 68 kontrolliert und möchte mich über den Ablauf beschweren."),
            ],
            [
                "Beschwerde eingegangen. Einsatzprotokoll des Abends wird angefordert.",
                "Protokoll liegt vor, Rücksprache mit der Streife läuft.",
            ]),
        new(1, TicketArt.Fuehrungsebene, "Hinweis auf einen Fehler der Webseite", TicketStatus.Offen, 1,
            [
                (true, "Die Druckansicht eines Steckbriefs zeigt bei mir keine Bilder an."),
            ],
            []),
        new(-1, TicketArt.Intern, "Zugriff auf die Asservatenkammer außerhalb der Dienstzeit", TicketStatus.InBearbeitung, 7,
            [],
            [
                "Bitte um Klärung, wer außerhalb der Dienstzeit Zugriff auf die Kammer hat.",
                "Regelung wird mit der Leitung abgestimmt, Zwischenstand bis Ende der Woche.",
            ]),
    ];

    private async Task<int> SeedTicketsAsync(
        AppDbContext db, Dictionary<string, BuergerProfil> citizens, List<TimeStamp> stamps, CancellationToken ct)
    {
        var existing = (await db.Tickets.IgnoreQueryFilters().Select(t => t.Subject).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        var order = Citizens.Select(c => citizens.GetValueOrDefault(c.Id)).ToList();

        foreach (var spec in TicketSpecs)
        {
            if (!existing.Add(spec.Subject))
            {
                continue;
            }
            var citizen = spec.CitizenIndex >= 0 ? order.ElementAtOrDefault(spec.CitizenIndex) : null;
            if (spec.Kind == TicketArt.Fuehrungsebene && citizen is null)
            {
                continue;
            }
            var created = DateTime.UtcNow.AddDays(-spec.DaysAgo);

            var ticket = new Ticket
            {
                CaseNumber = await caseNumbers.NextAsync(db, TicketPrefix, ct),
                Kind = spec.Kind,
                CitizenProfileId = spec.Kind == TicketArt.Fuehrungsebene ? citizen!.Id : null,
                OpenedByAgentId = spec.Kind == TicketArt.Intern ? DemoIdentity.AgentId : null,
                Subject = spec.Subject,
                Status = spec.Status,
                HandlerId = spec.Status == TicketStatus.Offen ? null : DemoIdentity.AgentId,
                LastActivityAt = created.AddHours(6),
                AgentLastReadAt = spec.Status == TicketStatus.Offen ? null : created.AddHours(6),
                CitizenLastReadAt = spec.Status == TicketStatus.Geschlossen ? created.AddHours(8) : null,
                ClosedAt = spec.Status == TicketStatus.Geschlossen ? created.AddHours(7) : null,
                ClosedById = spec.Status == TicketStatus.Geschlossen ? DemoIdentity.AgentId : null,
            };
            db.Tickets.Add(ticket);
            stamps.Add(new TimeStamp(nameof(Ticket), ticket.Id, created));
            added++;

            var step = 0;
            foreach (var (fromCitizen, text) in spec.CitizenThread)
            {
                var message = new TicketNachricht
                {
                    TicketId = ticket.Id,
                    Audience = TicketMessageAudience.Buerger,
                    Text = text,
                    AuthorIsCitizen = fromCitizen,
                    AuthorAgentId = null,
                };
                db.TicketNachrichten.Add(message);
                stamps.Add(new TimeStamp(nameof(TicketNachricht), message.Id, created.AddHours(step++ * 5)));
                added++;
            }
            foreach (var text in spec.InternalThread)
            {
                var message = new TicketNachricht
                {
                    TicketId = ticket.Id,
                    Audience = TicketMessageAudience.Intern,
                    Text = text,
                    AuthorIsCitizen = false,
                    AuthorAgentId = DemoIdentity.AgentId,
                };
                db.TicketNachrichten.Add(message);
                stamps.Add(new TimeStamp(nameof(TicketNachricht), message.Id, created.AddHours(step++ * 5)));
                added++;
            }

            if (spec.Kind == TicketArt.Intern)
            {
                db.TicketBeteiligte.Add(new TicketParticipant
                {
                    TicketId = ticket.Id,
                    AgentId = DemoIdentity.AgentId,
                    LastReadAt = created.AddHours(6),
                });
                added++;
            }
        }
        return added;
    }

    /// <summary>Objections: only the listed person may file one, so the citizen account is named after the notice.</summary>
    private async Task<int> SeedObjectionsAsync(
        AppDbContext db, Dictionary<string, BuergerProfil> citizens, List<SeededNotice> notices,
        List<TimeStamp> stamps, CancellationToken ct)
    {
        var existing = (await db.FahndungEinsprueche.IgnoreQueryFilters()
                .Select(o => new { o.CitizenProfileId, o.WantedId }).ToListAsync(ct))
            .Select(o => o.CitizenProfileId + "|" + o.WantedId)
            .ToHashSet(StringComparer.Ordinal);

        var open = notices
            .Where(n => n.Status == PublicWantedStatus.Veroeffentlicht && n.Notice.Kind == PublicWantedKind.Fahndung)
            .ToList();
        if (open.Count == 0)
        {
            return 0;
        }

        var added = 0;
        (int NoticeIndex, ObjectionStatus Status, string Text, string? Note, int DaysAgo)[] specs =
        [
            (open.Count - 1, ObjectionStatus.Abgelehnt,
                "Ich bin nicht die gesuchte Person. Zum genannten Zeitpunkt war ich nachweislich in Paleto Bay, "
                + "das belegt die Schichtliste meines Arbeitgebers. Ich bitte um Löschung der Ausschreibung.",
                "Die vorgelegte Schichtliste deckt den Tatzeitraum nicht ab. Die Ausschreibung bleibt bestehen.", 8),
            (Math.Max(0, open.Count - 2), ObjectionStatus.Neu,
                "Der angegebene Vorwurf trifft nicht zu. Das Verfahren gegen mich wurde eingestellt, "
                + "ich lege dem Einspruch die Verfahrensnummer bei.",
                null, 2),
        ];

        foreach (var (noticeIndex, status, text, note, daysAgo) in specs)
        {
            var notice = open.ElementAtOrDefault(noticeIndex);
            if (notice is null)
            {
                continue;
            }
            // the objection is filed by the listed person, so the citizen account carries the same name
            var profile = citizens.Values.FirstOrDefault(
                p => string.Equals(p.FirstName + " " + p.LastName, notice.Notice.DisplayName, StringComparison.OrdinalIgnoreCase));
            if (profile is null)
            {
                profile = await EnsureNamedCitizenAsync(db, citizens, notice.Notice.DisplayName, ct);
                if (profile is null)
                {
                    continue;
                }
                added++;
            }
            if (!existing.Add(profile.Id + "|" + notice.Notice.Id))
            {
                continue;
            }

            var created = DateTime.UtcNow.AddDays(-daysAgo);
            var objection = new FahndungEinspruch
            {
                CaseNumber = await caseNumbers.NextAsync(db, ObjectionPrefix, ct),
                WantedId = notice.Notice.Id,
                CitizenProfileId = profile.Id,
                Text = text,
                Status = status,
                DecisionNote = note,
                DecidedById = note is null ? null : DemoIdentity.AgentId,
                DecidedAt = note is null ? null : created.AddDays(2),
            };
            db.FahndungEinsprueche.Add(objection);
            stamps.Add(new TimeStamp(nameof(FahndungEinspruch), objection.Id, created));
            added++;
        }
        return added;
    }

    /// <summary>Creates the citizen account and profile for a listed person so the objection has a plausible author.</summary>
    private async Task<BuergerProfil?> EnsureNamedCitizenAsync(
        AppDbContext db, Dictionary<string, BuergerProfil> citizens, string displayName, CancellationToken ct)
    {
        var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;
        }
        var id = "demo-buerger-" + new string(displayName.ToLowerInvariant()
            .Where(c => char.IsAsciiLetterLower(c) || c == ' ').ToArray()).Replace(' ', '-');

        if (await userManager.FindByIdAsync(id) is null)
        {
            var account = new Agent
            {
                Id = id,
                UserName = id,
                DiscordId = id,
                DiscordUsername = parts[0].ToLowerInvariant(),
                Codename = string.Empty,
                Status = AgentStatus.Civilian,
                RegisteredAt = DateTime.UtcNow.AddDays(-52),
            };
            var result = await userManager.CreateAsync(account);
            if (!result.Succeeded)
            {
                return null;
            }
        }

        var existing = await db.BuergerProfile.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.UserId == id, ct);
        if (existing is not null)
        {
            citizens[id] = existing;
            return existing;
        }

        var profile = new BuergerProfil
        {
            UserId = id,
            FirstName = parts[0],
            LastName = parts[1],
            ConfirmedTips = 0,
        };
        db.BuergerProfile.Add(profile);
        citizens[id] = profile;
        return profile;
    }

    /// <summary>A citizen reporting that they held a wanted person themselves; needs its own tip kind and a handover place.</summary>
    private async Task<int> SeedCaptureReportAsync(
        AppDbContext db, Dictionary<string, BuergerProfil> citizens, List<SeededNotice> notices,
        List<TimeStamp> stamps, CancellationToken ct)
    {
        var notice = notices.FirstOrDefault(
            n => n.Status == PublicWantedStatus.Veroeffentlicht && n.Notice.Kind == PublicWantedKind.Fahndung);
        var citizen = citizens.GetValueOrDefault("demo-buerger-cruz");
        if (notice is null || citizen is null)
        {
            return 0;
        }

        const string text = "Die gesuchte Person hat versucht, in meinen Laden einzubrechen. Wir konnten sie festhalten, "
            + "bis Hilfe kommt. Bitte schicken Sie jemanden, wir warten im Hinterhof.";
        var taken = await db.Hinweise.IgnoreQueryFilters()
            .AnyAsync(h => h.CitizenProfileId == citizen.Id && h.Text == text, ct);
        if (taken)
        {
            return 0;
        }

        var created = DateTime.UtcNow.AddHours(-20);
        var report = new Hinweis
        {
            CaseNumber = await caseNumbers.NextAsync(db, TipPrefix, ct),
            Kind = TipKind.Ergreifung,
            Handover = TipHandover.Festgehalten,
            HandoverLocation = "Lagerhalle an der Popular Street, Hinterhof",
            CitizenProfileId = citizen.Id,
            WantsAnonymity = false,
            WantedId = notice.Notice.Id,
            Text = text,
            Status = TipStatus.InPruefung,
            HandlerId = DemoIdentity.AgentId,
            Priority = TipPriority.Compute(
                notice.Bounty, notice.Hazard, citizen.ConfirmedTips, TipKind.Ergreifung, TipHandover.Festgehalten),
            AgentLastReadAt = created.AddMinutes(20),
        };
        db.Hinweise.Add(report);
        stamps.Add(new TimeStamp(nameof(Hinweis), report.Id, created));

        var reply = new HinweisNachricht
        {
            HinweisId = report.Id,
            Audience = TipMessageAudience.Buerger,
            Text = "Wir haben Ihre Meldung erhalten. Eine Streife ist unterwegs. Bitte gehen Sie kein Risiko ein "
                + "und halten Sie Abstand, bis die Kollegen eingetroffen sind.",
            AuthorIsCitizen = false,
        };
        db.HinweisNachrichten.Add(reply);
        stamps.Add(new TimeStamp(nameof(HinweisNachricht), reply.Id, created.AddMinutes(25)));
        return 2;
    }
}

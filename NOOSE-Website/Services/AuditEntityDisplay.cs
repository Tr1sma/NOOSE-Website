namespace NOOSE_Website.Services;

/// <summary>German labels and deep-links for audit entity type names (CLR type names).</summary>
public static class AuditEntityDisplay
{
    // keys are CLR type names as stamped by the audit interceptor
    public static string Label(string type) => type switch
    {
        "Person" => "Person",
        "PersonDoc" => "Personen-Dok",
        "Observation" => "Observation",
        "PersonRelation" => "Personen-Beziehung",
        "Faction" => "Fraktion",
        "PersonGroup" => "Personengruppe",
        "Party" => "Partei",
        "Operation" => "Operation",
        "AgentActivity" => "Aktivität",
        "Taskforce" => "Taskforce",
        "Case" => "Vorgang",
        "Job" => "Aufgabe",
        "Appointment" => "Termin",
        "Document" => "Dokument",
        "Law" => "Gesetz",
        "Announcement" => "Ankündigung",
        "Agent" => "Agent",
        "Meeting" => "Besprechung",
        "MeetingAgendaItem" => "Tagesordnungspunkt",
        "MeetingAttendance" => "Anwesenheit",
        "MeetingSignOff" => "Abmeldung (Besprechung)",
        "Absence" => "Abmeldung",
        "KassenBuchung" => "Kassenbuchung",
        "FinancingRequest" => "Finanzierungsantrag",
        "FinancingRequestLine" => "Finanzierungsposition (Antrag)",
        "FinancingItem" => "Finanzierungs-Katalogposition",
        "FinancingBudgetConfig" => "Finanzierungs-Budgets",
        _ => type,
    };

    /// <summary>Deep-link to the record, or null for child entities without their own page.</summary>
    public static string? Route(string type, string id) => type switch
    {
        "Person" => $"/personen/{id}",
        "Faction" => $"/fraktionen/{id}",
        "PersonGroup" => $"/personengruppen/{id}",
        "Party" => $"/parteien/{id}",
        "Operation" => $"/operationen/{id}",
        "AgentActivity" => $"/aktivitaeten/{id}",
        "Taskforce" => $"/taskforces/{id}",
        "Case" => $"/vorgaenge/{id}",
        "Job" => $"/aufgaben/{id}",
        "Appointment" => $"/kalender/{id}",
        "Document" => $"/dokumente/{id}",
        "Law" => $"/gesetze/{id}",
        "Agent" => $"/personal/{id}",
        "Meeting" => $"/besprechungen/{id}",
        // absences have no detail page; the audit viewer is admin-only, so point at the overview
        "Absence" => "/abmeldungen/uebersicht",
        "KassenBuchung" => $"/kasse/buchung/{id}",
        "FinancingRequest" => $"/finanzierungen/{id}",
        // catalog positions and config have no own page; point at the section that edits them
        "FinancingItem" => "/finanzierungen?tab=katalog",
        "FinancingBudgetConfig" => "/einstellungen?tab=finanzierung",
        _ => null,
    };
}

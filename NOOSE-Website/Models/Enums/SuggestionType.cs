namespace NOOSE_Website.Models.Enums;

/// <summary>Autocomplete suggestion category.</summary>
public enum SuggestionType
{
    Weapon = 0,
    Vehicle = 1,
    Location = 2,
    // Phase 4: faction inventory stocks.
    Inventory = 3,
    // Phase 4: faction kind (gang/mafia/…).
    Kind = 4,
    // Phase 5a: party member role.
    PartyRole = 5,
    // Phase 5b: operation type/category.
    OperationType = 6,
    // Phase 5: case type/category.
    CaseType = 7,
    // Drug routes for factions.
    DrugRoute = 8,
}

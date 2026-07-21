# NOOSE-Website.Tests

xUnit test project for the NOOSE-Website Blazor app. Targets `net10.0`, references the web
project, and covers the business logic in `Services/`, `Authorization/`, and `Models/`.

## Layout

- `Infrastructure/` — shared test helpers:
  - `SqliteTestContext` — an open in-memory SQLite database with the full EF schema
    (`EnsureCreated`), FK enforcement off (the app runs on MySQL; tests exercise logic, not
    referential integrity). Exposes `Factory` (an `IDbContextFactory<AppDbContext>`) and
    `NewContext()`.
  - `Seed` — factories for the most-referenced entities (each `.CaseNumber` is unique).
  - `ClaimsPrincipalBuilder` — fluent builder for agent/partner `ClaimsPrincipal`s.
  - `TestParallelization.cs` — disables cross-collection parallelism (shared in-memory
    connections + a global seed counter are not parallel-safe).
- `Authorization/`, `Services/`, `Models/` — pure-logic unit tests.
- `Services/Integration/` — service tests backed by `SqliteTestContext`, with collaborators
  substituted via NSubstitute (`ICaseNumberService` is stubbed because the real one uses
  MySQL-only `ON DUPLICATE KEY` SQL).

## Run the tests

```bash
dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj
```

## Coverage

Coverage uses the built-in **Microsoft Code Coverage** engine — coverlet is not used because its
Mono.Cecil build cannot instrument .NET 10 assemblies.

```bash
dotnet test NOOSE-Website.Tests/NOOSE-Website.Tests.csproj \
    --collect "Code Coverage;Format=cobertura"
python NOOSE-Website.Tests/coverage/coverage-summary.py \
    <path printed above, e.g. NOOSE-Website.Tests/TestResults/<guid>/*.cobertura.xml>
```

`coverage-summary.py` reports line coverage for `Services` / `Authorization` / `Models` only.

### Scope & exclusions

Coverage is measured over the hand-written logic namespaces. Excluded by design:

- **Generated / non-logic:** EF migrations, Razor components, `Program.cs`, plain entity/DTO
  types.
- **`[ExcludeFromCodeCoverage]` in the web project** (genuinely not unit-testable):
  `AuthorizationRegistration` (DI composition), `DiscordWebhookService` (outbound HTTP),
  `DemoDataSeed` / `DemoDataService` (dev-only demo fixtures).

### Latest result

| Namespace     | Line coverage |
| ------------- | ------------- |
| Services      | 91.4 %        |
| Authorization | 100 %         |
| Models        | 91.3 %        |
| **Total**     | **91.4 %**    |

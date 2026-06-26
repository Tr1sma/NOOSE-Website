# Demo-Modus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an admin-toggled Demo-Mode that presents anonymous visitors as a read-only "Demo Agent" with full read visibility, plus a rich idempotent demo-data seeder, for a public portfolio showcase on `demo.noose.info`.

**Architecture:** A synthetic `ClaimsPrincipal` (Director + TRU + HRB + `IsDemo`, not admin) is applied to anonymous requests via middleware (static SSR + authorization) and via a custom `RevalidatingServerAuthenticationStateProvider` (interactive circuit). Writes are blocked at the UI (`MayWrite()` hides controls) and at the DB (`ReadOnlyBarrierInterceptor` vetoes). A `SystemSetting` key toggles the mode behind a 3-stage admin confirmation dialog. Demo data is seeded idempotently via an admin-triggered service.

**Tech Stack:** .NET 10, Blazor Web App (Interactive Server), MudBlazor 9.5, EF Core 9 (Pomelo), ASP.NET Core Identity.

## Global Constraints

- **No test project exists** — verification is `dotnet build NOOSE-Website.slnx` + manual checks. Do NOT add a test project.
- **Stop the dev server before building** (bin-lock); run all commands from repo root.
- **Comments: English only**, inline `//`, 2–3 words, describe the *why*. No block comments. `catch {}` → `/* best effort */` or `/* ignore */`.
- **DB columns German, C# members English.** Soft-delete (`ISoftDelete`) + audit (`IAuditable`) are the norm.
- **Always `IDbContextFactory<AppDbContext>`** + `await using var db = await dbFactory.CreateDbContextAsync(ct)` per operation.
- **Policy strings via `Policies.*`**, never hardcoded. Authorization enforced in the service layer (`Permission.Require*` first statement of writes).
- **Demo-Mode must never auto-seed at startup** (would inject fake data into prod). Seeding is an explicit admin action only.
- EF/Identity stay on the 9.0.x line. `?v=` cache-busters are not touched (no JS module edits in this plan).

## File Structure

**New files**
- `NOOSE-Website/Infrastructure/DemoIdentity.cs` — demo agent id constant + synthetic principal builder.
- `NOOSE-Website/Infrastructure/DemoModeMiddleware.cs` — applies demo principal to anonymous requests (SSR/authorization).
- `NOOSE-Website/Components/Account/DemoAwareAuthenticationStateProvider.cs` — circuit auth state for demo + real users.
- `NOOSE-Website/Components/Pages/Admin/DemoModeConfirmDialog.razor` — 3-stage enable wizard (non-copyable word challenge).
- `NOOSE-Website/Services/IDemoDataService.cs` + `NOOSE-Website/Services/DemoDataService.cs` — idempotent demo seeder.

**Modified files**
- `NOOSE-Website/Models/Common/SystemConfiguration.cs` — `DemoModeActive` key + record field + input field.
- `NOOSE-Website/Services/SystemSettingService.cs` — read/write/default the new setting.
- `NOOSE-Website/Authorization/AgentClaimTypes.cs` — `IsDemo` claim constant.
- `NOOSE-Website/Authorization/AgentPrincipalExtensions.cs` — `IsDemo()` + `MayWrite()` update.
- `NOOSE-Website/Infrastructure/CurrentUser/ICurrentUserService.cs` — `IsDemo` on `CurrentUserInfo`.
- `NOOSE-Website/Infrastructure/CurrentUser/CurrentUserService.cs` — populate `IsDemo`.
- `NOOSE-Website/Infrastructure/Authorization/ReadOnlyBarrierInterceptor.cs` — veto demo writes.
- `NOOSE-Website/Program.cs` — middleware + auth-state provider + service registration.
- `NOOSE-Website/Components/Layout/MainLayout.razor` — demo banner.
- `NOOSE-Website/Components/Pages/Admin/SystemManagement.razor` — toggle, wizard wiring, seed button.

**Deleted**
- `NOOSE-Website/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs` — superseded by `DemoAwareAuthenticationStateProvider`.

---

### Task 1: Demo-Mode setting plumbing

**Files:**
- Modify: `NOOSE-Website/Models/Common/SystemConfiguration.cs`
- Modify: `NOOSE-Website/Services/SystemSettingService.cs`

**Interfaces:**
- Produces: `SystemSettingKeys.DemoModeActive` (string const), `SystemConfiguration.DemoModeActive` (bool), `SystemConfigurationInput.DemoModeActive` (bool). Consumed by Tasks 5, 6, 8.

- [ ] **Step 1: Add the setting key, record field, and input field**

In `SystemConfiguration.cs`, add the key constant after `LogoContentType`:

```csharp
    public const string LogoContentType = "LogoContentType";
    public const string DemoModeActive = "DemoModusAktiv";
```

Add the record field as the last positional parameter:

```csharp
public sealed record SystemConfiguration(
    bool MaintenanceModeActive,
    string? MaintenanceModeText,
    string? BannerText,
    string BannerLevel,
    string? ThemePrimary,
    string? ThemeSecondary,
    string? ThemeTertiary,
    string? LogoFileName,
    string? LogoContentType,
    bool DemoModeActive)
{
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoFileName);
}
```

Add the input field:

```csharp
public class SystemConfigurationInput
{
    public bool MaintenanceModeActive { get; set; }
    public string? MaintenanceModeText { get; set; }
    public string? BannerText { get; set; }
    public string BannerLevel { get; set; } = BannerLevels.Info;
    public string? ThemePrimary { get; set; }
    public string? ThemeSecondary { get; set; }
    public string? ThemeTertiary { get; set; }
    public bool DemoModeActive { get; set; }
}
```

- [ ] **Step 2: Read, write, and default the setting in `SystemSettingService.cs`**

In `GetAsync`, add to the `new SystemConfiguration(...)` initializer (after `LogoContentType:`):

```csharp
                LogoContentType: Empty(values.GetValueOrDefault(SystemSettingKeys.LogoContentType)),
                DemoModeActive: string.Equals(values.GetValueOrDefault(SystemSettingKeys.DemoModeActive), "true", StringComparison.OrdinalIgnoreCase));
```

In `SaveAsync`, add after the `ThemeTertiary` line:

```csharp
        await SetAsync(db, SystemSettingKeys.ThemeTertiary, Empty(input.ThemeTertiary)?.Trim(), cancellationToken);
        await SetAsync(db, SystemSettingKeys.DemoModeActive, input.DemoModeActive ? "true" : "false", cancellationToken);
```

Update `Default()`:

```csharp
    private static SystemConfiguration Default()
        => new(false, null, null, BannerLevels.Info, null, null, null, null, null, false);
```

- [ ] **Step 3: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add NOOSE-Website/Models/Common/SystemConfiguration.cs NOOSE-Website/Services/SystemSettingService.cs
git commit -m "feat(demo): add DemoModeActive system setting"
```

---

### Task 2: `IsDemo` claim and write-gate

**Files:**
- Modify: `NOOSE-Website/Authorization/AgentClaimTypes.cs`
- Modify: `NOOSE-Website/Authorization/AgentPrincipalExtensions.cs`

**Interfaces:**
- Produces: `AgentClaimTypes.IsDemo` (string const), `ClaimsPrincipal.IsDemo()` (bool ext). `MayWrite()` now also false for demo. Consumed by Tasks 3, 4, 6, 7.

- [ ] **Step 1: Add the claim type constant**

In `AgentClaimTypes.cs`, add after `IsTeamLead`:

```csharp
    public const string IsTeamLead = "noose:teamleitung";
    public const string IsDemo = "noose:demo";
```

- [ ] **Step 2: Add `IsDemo()` and update `MayWrite()`**

In `AgentPrincipalExtensions.cs`, add after `IsTeamLead(...)` (before `IsOnlyReader`):

```csharp
    /// <summary>Read-only public demo visitor (synthetic principal); reads everything, writes nothing.</summary>
    public static bool IsDemo(this ClaimsPrincipal user)
        => string.Equals(user.FindFirstValue(AgentClaimTypes.IsDemo), "true", StringComparison.OrdinalIgnoreCase);
```

Update `MayWrite()`:

```csharp
    /// <summary>May write at all; false for read-only supervisors, partners and demo visitors. Sole source for write-control visibility.</summary>
    public static bool MayWrite(this ClaimsPrincipal user) => !user.IsOnlyReader() && !user.IsPartner() && !user.IsDemo();
```

- [ ] **Step 3: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add NOOSE-Website/Authorization/AgentClaimTypes.cs NOOSE-Website/Authorization/AgentPrincipalExtensions.cs
git commit -m "feat(demo): add IsDemo claim and exclude demo from MayWrite"
```

---

### Task 3: Demo write-veto at the DB layer

**Files:**
- Modify: `NOOSE-Website/Infrastructure/CurrentUser/ICurrentUserService.cs`
- Modify: `NOOSE-Website/Infrastructure/CurrentUser/CurrentUserService.cs`
- Modify: `NOOSE-Website/Infrastructure/Authorization/ReadOnlyBarrierInterceptor.cs`

**Interfaces:**
- Consumes: `ClaimsPrincipal.IsDemo()` (Task 2).
- Produces: `CurrentUserInfo.IsDemo` (bool). The interceptor now blocks writes when `IsDemo`.

- [ ] **Step 1: Add `IsDemo` to `CurrentUserInfo`**

In `ICurrentUserService.cs`:

```csharp
public readonly record struct CurrentUserInfo(string? Id, string? Name, bool IsOnlyReader, bool IsPartner, bool IsDemo)
{
    public static readonly CurrentUserInfo System = new(null, "System", false, false, false);
}
```

- [ ] **Step 2: Populate `IsDemo` in `CurrentUserService.Build`**

```csharp
    private static CurrentUserInfo Build(ClaimsPrincipal user)
        => new(user.GetAgentId(), user.GetCodename() ?? user.Identity?.Name, user.IsOnlyReader(), user.IsPartner(), user.IsDemo());
```

- [ ] **Step 3: Veto demo writes in `ReadOnlyBarrierInterceptor.Require`**

```csharp
    private static void Require(DbContext? context, CurrentUserInfo user)
    {
        if (context is null || (!user.IsOnlyReader && !user.IsPartner && !user.IsDemo))
        {
            return;
        }
```

- [ ] **Step 4: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add NOOSE-Website/Infrastructure/CurrentUser/ICurrentUserService.cs NOOSE-Website/Infrastructure/CurrentUser/CurrentUserService.cs NOOSE-Website/Infrastructure/Authorization/ReadOnlyBarrierInterceptor.cs
git commit -m "feat(demo): block demo-visitor writes in the read-only barrier"
```

---

### Task 4: Synthetic demo principal

**Files:**
- Create: `NOOSE-Website/Infrastructure/DemoIdentity.cs`

**Interfaces:**
- Consumes: `AgentClaimTypes.*` (Task 2), `Rank`, `AgentStatus`.
- Produces: `DemoIdentity.AgentId` (const "demo-agent"), `DemoIdentity.Codename`, `DemoIdentity.BuildPrincipal()` → `ClaimsPrincipal`. Consumed by Tasks 5, 6, 9.

- [ ] **Step 1: Create `DemoIdentity.cs`**

```csharp
using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Infrastructure;

/// <summary>The synthetic read-only visitor presented to anonymous users while demo mode is active.</summary>
public static class DemoIdentity
{
    /// <summary>Stable id of the seeded demo agent row; also the principal's NameIdentifier.</summary>
    public const string AgentId = "demo-agent";

    /// <summary>Auth type marker so the identity counts as authenticated.</summary>
    public const string AuthenticationType = "Demo";

    public const string Codename = "Demo";

    /// <summary>Director + TRU + HRB for full read access; not admin; marked read-only via IsDemo.</summary>
    public static ClaimsPrincipal BuildPrincipal()
    {
        var identity = new ClaimsIdentity(AuthenticationType);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, AgentId));
        identity.AddClaim(new Claim(AgentClaimTypes.Codename, Codename));
        identity.AddClaim(new Claim(AgentClaimTypes.Status, AgentStatus.Active.ToString()));
        identity.AddClaim(new Claim(AgentClaimTypes.Rank, ((int)Rank.Director).ToString()));
        identity.AddClaim(new Claim(AgentClaimTypes.IsTRU, "true"));
        identity.AddClaim(new Claim(AgentClaimTypes.IsHRB, "true"));
        identity.AddClaim(new Claim(AgentClaimTypes.IsAdmin, "false"));
        identity.AddClaim(new Claim(AgentClaimTypes.IsTeamLead, "false"));
        identity.AddClaim(new Claim(AgentClaimTypes.IsDemo, "true"));
        return new ClaimsPrincipal(identity);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add NOOSE-Website/Infrastructure/DemoIdentity.cs
git commit -m "feat(demo): add synthetic demo principal builder"
```

---

### Task 5: Demo-mode middleware (SSR / authorization)

**Files:**
- Create: `NOOSE-Website/Infrastructure/DemoModeMiddleware.cs`
- Modify: `NOOSE-Website/Program.cs`

**Interfaces:**
- Consumes: `ISystemSettingService.GetAsync()` (Task 1), `DemoIdentity.BuildPrincipal()` (Task 4).
- Produces: anonymous non-excluded requests carry the demo principal when demo mode is on.

- [ ] **Step 1: Create `DemoModeMiddleware.cs`**

```csharp
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure;

/// <summary>While demo mode is on, presents anonymous visitors as the read-only demo agent so the whole app is browsable without login. Login and framework paths stay anonymous.</summary>
public sealed class DemoModeMiddleware(RequestDelegate next)
{
    // login + framework + asset paths must not be hijacked
    private static readonly string[] ExcludedPrefixes =
    [
        "/Account", "/signin-discord", "/health", "/_blazor", "/_framework", "/system/logo",
    ];

    public async Task InvokeAsync(HttpContext context, ISystemSettingService settings)
    {
        if (context.User.Identity?.IsAuthenticated != true && !IsExcluded(context.Request.Path))
        {
            var config = await settings.GetAsync(context.RequestAborted);
            if (config.DemoModeActive)
            {
                context.User = DemoIdentity.BuildPrincipal();
            }
        }

        await next(context);
    }

    private static bool IsExcluded(PathString path)
    {
        foreach (var prefix in ExcludedPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
```

- [ ] **Step 2: Register the middleware between authentication and authorization**

In `Program.cs`, change:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

to:

```csharp
app.UseAuthentication();
app.UseMiddleware<NOOSE_Website.Infrastructure.DemoModeMiddleware>();
app.UseAuthorization();
```

- [ ] **Step 3: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add NOOSE-Website/Infrastructure/DemoModeMiddleware.cs NOOSE-Website/Program.cs
git commit -m "feat(demo): apply demo principal to anonymous requests via middleware"
```

---

### Task 6: Demo-aware auth-state provider (circuit)

**Files:**
- Create: `NOOSE-Website/Components/Account/DemoAwareAuthenticationStateProvider.cs`
- Modify: `NOOSE-Website/Program.cs`
- Delete: `NOOSE-Website/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs`

**Interfaces:**
- Consumes: `ISystemSettingService.GetAsync()` (Task 1), `ClaimsPrincipal.IsDemo()` (Task 2), `DemoIdentity.BuildPrincipal()` (Task 4).
- Produces: the registered `AuthenticationStateProvider` returns the demo principal in anonymous circuits when demo mode is on, and keeps the 30s revalidation kill-switch for real users.

- [ ] **Step 1: Create `DemoAwareAuthenticationStateProvider.cs`**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Components.Account;

/// <summary>Carries auth state into interactive components and revalidates it (kill-switch). In demo mode it presents anonymous circuits as the demo agent and never revalidates that synthetic principal away.</summary>
internal sealed class DemoAwareAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    // keep identical to the SecurityStampValidator interval in Program.cs
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var state = await base.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated == true)
        {
            return state;
        }

        // anonymous circuit + demo mode → present the demo agent
        if (await DemoActiveAsync())
        {
            var demo = new AuthenticationState(DemoIdentity.BuildPrincipal());
            SetAuthenticationState(Task.FromResult(demo));
            return demo;
        }

        return state;
    }

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // the synthetic demo principal is static and always valid
        if (authenticationState.User.IsDemo())
        {
            return true;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Agent>>();
        return await ValidateAsync(userManager, authenticationState.User);
    }

    private async Task<bool> DemoActiveAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingService>();
        return (await settings.GetAsync()).DemoModeActive;
    }

    private async Task<bool> ValidateAsync(UserManager<Agent> userManager, ClaimsPrincipal principal)
    {
        var agent = await userManager.GetUserAsync(principal);
        if (agent is null || agent.Status != AgentStatus.Active)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var stampInCookie = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        var currentStamp = await userManager.GetSecurityStampAsync(agent);
        return stampInCookie == currentStamp;
    }
}
```

- [ ] **Step 2: Swap the registration in `Program.cs`**

Change:

```csharp
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
```

to:

```csharp
builder.Services.AddScoped<AuthenticationStateProvider, DemoAwareAuthenticationStateProvider>();
```

- [ ] **Step 3: Delete the superseded provider**

```bash
git rm NOOSE-Website/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs
```

- [ ] **Step 4: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors. (If "type or namespace IdentityRevalidatingAuthenticationStateProvider not found" appears, a reference was missed — only Program.cs should reference it.)

- [ ] **Step 5: Commit**

```bash
git add NOOSE-Website/Components/Account/DemoAwareAuthenticationStateProvider.cs NOOSE-Website/Program.cs
git commit -m "feat(demo): demo-aware auth-state provider for interactive circuits"
```

---

### Task 7: Demo banner in the layout

**Files:**
- Modify: `NOOSE-Website/Components/Layout/MainLayout.razor`

**Interfaces:**
- Consumes: `ClaimsPrincipal.IsDemo()` (Task 2).
- Produces: a persistent read-only demo banner for demo visitors.

- [ ] **Step 1: Add the `_isDemo` field**

In the `@code` block, after `private bool _isPartner;`:

```csharp
    private bool _isPartner;
    private bool _isDemo;
```

- [ ] **Step 2: Set it in `OnInitializedAsync`**

After `_isAdmin = user.IsAdmin();`:

```csharp
            _isAdmin = user.IsAdmin();
            _isDemo = user.IsDemo();
```

- [ ] **Step 3: Render the banner**

Immediately after the closing `</AuthorizeView>` of the `OnlyReadMode` block (after line `</AuthorizeView>` that follows the "Nur-Lese-Modus (Aufsicht)" alert), add:

```razor
            @if (_isDemo)
            {
                <MudAlert Severity="Severity.Info" Dense="true" Class="mb-4 no-print">
                    Demo-Modus: schreibgeschützte Vorschau mit Beispieldaten. Änderungen sind nicht möglich.
                </MudAlert>
            }
```

- [ ] **Step 4: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add NOOSE-Website/Components/Layout/MainLayout.razor
git commit -m "feat(demo): show read-only demo banner in the layout"
```

---

### Task 8: Admin toggle + 3-stage confirmation wizard

**Files:**
- Create: `NOOSE-Website/Components/Pages/Admin/DemoModeConfirmDialog.razor`
- Modify: `NOOSE-Website/Components/Pages/Admin/SystemManagement.razor`

**Interfaces:**
- Consumes: `SystemConfigurationInput.DemoModeActive` (Task 1), `IDemoDataService` (Task 9 — wired in this task's seed button; Task 9 provides the type).
- Produces: enabling demo mode requires the full 3-stage dialog; disabling is one click.

> Note: the seed button calls `IDemoDataService` from Task 9. Implement Task 9 before this task, or temporarily comment the seed button until Task 9 lands. Recommended order: Task 9, then Task 8.

- [ ] **Step 1: Create `DemoModeConfirmDialog.razor`**

```razor
@implements IDisposable

@* Three-stage confirmation before enabling public demo mode (anonymous read access). *@
<MudDialog>
    <DialogContent>
        @if (_step == 1)
        {
            <MudStack Spacing="3">
                <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
                    <MudIcon Icon="@Icons.Material.Filled.Warning" Color="Color.Error" Size="Size.Large" />
                    <MudText Typo="Typo.h6">Demo-Modus aktivieren?</MudText>
                </MudStack>
                <MudText Typo="Typo.body1">Willst du das wirklich?</MudText>
            </MudStack>
        }
        else if (_step == 2)
        {
            <MudStack Spacing="3">
                <MudText Typo="Typo.h6" Color="Color.Error">Auswirkungen</MudText>
                <MudAlert Severity="Severity.Error" Variant="Variant.Outlined">
                    Anonyme Besucher sehen <b>ALLE Daten dieser Instanz</b> – inklusive Verschlusssachen
                    und Klarnamen – <b>ohne Login</b>. Nur auf der Demo-Instanz mit Beispieldaten aktivieren,
                    niemals auf der Produktiv-Instanz mit echten Akten.
                </MudAlert>
                <MudText Typo="Typo.body2" Class="mud-text-secondary">
                    Der „Weiter"-Button ist gesperrt, bis du die Auswirkungen gelesen hast.
                </MudText>
            </MudStack>
        }
        else
        {
            <MudStack Spacing="3">
                <MudText Typo="Typo.h6">Bestätigung</MudText>
                <MudText Typo="Typo.body2" Class="mud-text-secondary">
                    Tippe das folgende Wort ab (es lässt sich nicht kopieren):
                </MudText>
                <div class="demo-word">
                    @foreach (var ch in _challenge)
                    {
                        <span class="demo-letter" data-c="@ch"></span>
                    }
                </div>
                <MudTextField @bind-Value="_typed" Immediate="true" Label="Wort eingeben"
                              Variant="Variant.Outlined" autocomplete="off" />
            </MudStack>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Abbrechen</MudButton>
        @if (_step == 1)
        {
            <MudButton Color="Color.Error" Variant="Variant.Filled" OnClick="GoToStep2">Ja, fortfahren</MudButton>
        }
        else if (_step == 2)
        {
            <MudButton Color="Color.Error" Variant="Variant.Filled" Disabled="_lockSeconds > 0" OnClick="GoToStep3">
                @(_lockSeconds > 0 ? $"Weiter ({_lockSeconds})" : "Weiter")
            </MudButton>
        }
        else
        {
            <MudButton Color="Color.Error" Variant="Variant.Filled"
                       Disabled="@(!string.Equals(_typed?.Trim(), _challenge, StringComparison.OrdinalIgnoreCase))"
                       OnClick="Confirm">Demo-Modus aktivieren</MudButton>
        }
    </DialogActions>
</MudDialog>

<style>
    .demo-word { display:flex; gap:.4rem; font-size:2rem; font-weight:700; letter-spacing:.25rem;
                 background:rgba(255,255,255,.05); padding:.75rem 1rem; border-radius:.5rem;
                 user-select:none; -webkit-user-select:none; }
    .demo-letter { pointer-events:none; }
    .demo-letter::before { content:attr(data-c); }
</style>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    private int _step = 1;
    private int _lockSeconds = 10;
    private string _typed = string.Empty;
    private readonly string _challenge = GenerateWord();
    private System.Timers.Timer? _timer;

    private void GoToStep2()
    {
        _step = 2;
        _lockSeconds = 10;
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += async (_, _) =>
        {
            await InvokeAsync(() =>
            {
                if (_lockSeconds > 0) _lockSeconds--;
                if (_lockSeconds <= 0) { _timer?.Stop(); _timer?.Dispose(); _timer = null; }
                StateHasChanged();
            });
        };
        _timer.Start();
    }

    private void GoToStep3()
    {
        _timer?.Dispose();
        _timer = null;
        _step = 3;
    }

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
    private void Cancel() => MudDialog.Cancel();

    public void Dispose() => _timer?.Dispose();

    // non-confusable uppercase letters (no I/O/Q)
    private static string GenerateWord()
    {
        const string letters = "ABCDEFGHJKLMNPRSTUVWXYZ";
        return string.Concat(Enumerable.Range(0, 6).Select(_ => letters[Random.Shared.Next(letters.Length)]));
    }
}
```

- [ ] **Step 2: Add injects to `SystemManagement.razor`**

After `@inject ISnackbar Snackbar`:

```razor
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@inject IDemoDataService DemoData
```

- [ ] **Step 3: Add the Demo-Mode section**

Insert a new `MudPaper` after the Wartungsmodus `MudPaper` (after its closing `</MudPaper>`, before the Ankündigungsbanner paper):

```razor
        <MudPaper Class="pa-4" Elevation="2">
            <MudText Typo="Typo.h6" Class="mb-2">Demo-Modus</MudText>
            <MudText Typo="Typo.body2" Class="mud-text-secondary mb-3">
                Macht die gesamte Website ohne Login als schreibgeschützte Vorschau mit Beispieldaten sichtbar
                (Portfolio/Showcase). <b>Nur auf der Demo-Instanz aktivieren</b> – niemals mit echten Daten.
            </MudText>
            <AuthorizeView Policy="@Policies.Admin" Context="admin">
                <MudSwitch T="bool" Value="_input.DemoModeActive" Color="Color.Error"
                           Label="Demo-Modus aktiv (öffentlich, ohne Login)"
                           ValueChanged="DemoModeToggleAsync" />
                <MudButton Variant="Variant.Outlined" Color="Color.Primary" Class="mt-2"
                           StartIcon="@Icons.Material.Filled.DataObject" OnClick="SeedDemoDataAsync"
                           Disabled="_seeding">Demo-Daten einspielen</MudButton>
            </AuthorizeView>
        </MudPaper>
```

- [ ] **Step 4: Add handlers and the `_seeding` field**

In the `@code` block, after `private bool _busy;`:

```csharp
    private bool _busy;
    private bool _seeding;
```

Add the methods after `SaveAsync`:

```csharp
    private async Task DemoModeToggleAsync(bool value)
    {
        if (value)
        {
            var dialog = await DialogService.ShowAsync<DemoModeConfirmDialog>("Demo-Modus aktivieren");
            var result = await dialog.Result;
            if (result is null || result.Canceled || result.Data is not true)
            {
                return; // not confirmed → stays off
            }
            _input.DemoModeActive = true;
        }
        else
        {
            _input.DemoModeActive = false;
        }
        await SaveAsync();
    }

    private async Task SeedDemoDataAsync()
    {
        _seeding = true;
        try
        {
            var count = await DemoData.SeedAsync(_user);
            Snackbar.Add($"Demo-Daten eingespielt ({count} neue Datensätze).", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _seeding = false;
        }
    }
```

- [ ] **Step 5: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add NOOSE-Website/Components/Pages/Admin/DemoModeConfirmDialog.razor NOOSE-Website/Components/Pages/Admin/SystemManagement.razor
git commit -m "feat(demo): admin toggle with 3-stage confirmation wizard"
```

---

### Task 9: Demo-data seeder (framework + starter dataset)

**Files:**
- Create: `NOOSE-Website/Services/IDemoDataService.cs`
- Create: `NOOSE-Website/Services/DemoDataService.cs`
- Modify: `NOOSE-Website/Program.cs`

**Interfaces:**
- Consumes: `Permission.RequireAdmin` , `ICaseNumberService.NextAsync`, `DemoIdentity.AgentId` (Task 4), `IDbContextFactory<AppDbContext>`, `UserManager<Agent>`.
- Produces: `IDemoDataService.SeedAsync(ClaimsPrincipal actor, CancellationToken)` → `int` (rows added). Consumed by Task 8.

- [ ] **Step 1: Create `IDemoDataService.cs`**

```csharp
using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Idempotently seeds the demo agent and example records for the public demo instance. Admin only.</summary>
public interface IDemoDataService
{
    /// <summary>Seeds missing demo data; returns the number of newly added rows.</summary>
    Task<int> SeedAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create `DemoDataService.cs`**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Seeds the demo agent and example records; idempotent (skips by name) and admin-gated.</summary>
public class DemoDataService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICaseNumberService caseNumbers,
    UserManager<Agent> userManager) : IDemoDataService
{
    public async Task<int> SeedAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAdmin(actor);

        var added = await EnsureDemoAgentAsync();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // case numbers need an enclosing transaction (see CaseNumberService)
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        added += await SeedFactionsAsync(db, cancellationToken);
        added += await SeedPeopleAsync(db, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return added;
    }

    private async Task<int> EnsureDemoAgentAsync()
    {
        if (await userManager.FindByIdAsync(DemoIdentity.AgentId) is not null)
        {
            return 0;
        }
        var agent = new Agent
        {
            Id = DemoIdentity.AgentId,
            UserName = "demo-agent",
            Codename = DemoIdentity.Codename,
            DiscordId = "demo",
            Status = AgentStatus.Active,
            Rank = Rank.Director,
            IsTRU = true,
            IsHRB = true,
            RegisteredAt = DateTime.UtcNow,
        };
        await userManager.CreateAsync(agent);
        return 1;
    }

    private async Task<int> SeedFactionsAsync(AppDbContext db, CancellationToken ct)
    {
        var have = new HashSet<string>(
            await db.Factions.IgnoreQueryFilters().Select(f => f.Name).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var spec in FactionSpecs)
        {
            if (have.Contains(spec.Name))
            {
                continue;
            }
            db.Factions.Add(new Faction
            {
                CaseNumber = await caseNumbers.NextAsync(db, "F", ct),
                Name = spec.Name,
                Kind = spec.Kind,
                Description = spec.Description,
                RecognitionColor = spec.Color,
                Classification = spec.Classification,
                ThreatScore = spec.ThreatScore,
                ThreatConfidence = 70,
                ScoreCalculatedAt = DateTime.UtcNow,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    private async Task<int> SeedPeopleAsync(AppDbContext db, CancellationToken ct)
    {
        var have = new HashSet<string>(
            await db.People.IgnoreQueryFilters().Select(p => p.Name).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var spec in PeopleSpecs)
        {
            if (have.Contains(spec.Name))
            {
                continue;
            }
            db.People.Add(new Person
            {
                CaseNumber = await caseNumbers.NextAsync(db, "P", ct),
                Name = spec.Name,
                Description = spec.Description,
                Classification = spec.Classification,
                ThreatScore = spec.ThreatScore,
                ThreatConfidence = 65,
                ScoreCalculatedAt = DateTime.UtcNow,
                CreatedById = DemoIdentity.AgentId,
            });
            added++;
        }
        return added;
    }

    private static readonly (string Name, string Kind, string Description, string Color, Classification Classification, int ThreatScore)[] FactionSpecs =
    [
        ("Vagos Demo-Clan", "Streetgang", "Beispiel-Fraktion für die Demo-Instanz.", "#F9A825", Classification.SuspicionCase, 58),
        ("Marabunta Demo", "Streetgang", "Beispiel-Fraktion für die Demo-Instanz.", "#2E7D32", Classification.ReviewCase, 34),
        ("Demo-Syndikat", "Organisierte Kriminalität", "Beispiel-Fraktion mit hoher Einstufung.", "#C62828", Classification.SecuredStateThreatening, 82),
    ];

    private static readonly (string Name, string Description, Classification Classification, int ThreatScore)[] PeopleSpecs =
    [
        ("Max Demo", "Beispiel-Person für die Demo-Instanz.", Classification.ReviewCase, 22),
        ("Erika Beispiel", "Beispiel-Person mit Verdachtsfall.", Classification.SuspicionCase, 49),
        ("John Showcase", "Beispiel-Person, gesichert staatsgefährdend.", Classification.SecuredStateThreatening, 88),
        ("Lara Muster", "Beispiel-Person für die Demo-Instanz.", Classification.Unknown, 5),
    ];
}
```

- [ ] **Step 3: Register the service in `Program.cs`**

After `builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();`:

```csharp
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddScoped<IDemoDataService, DemoDataService>();
```

- [ ] **Step 4: Build**

Run: `dotnet build NOOSE-Website.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add NOOSE-Website/Services/IDemoDataService.cs NOOSE-Website/Services/DemoDataService.cs NOOSE-Website/Program.cs
git commit -m "feat(demo): idempotent demo-data seeder (agent + starter records)"
```

- [ ] **Step 6 (follow-up, optional): Enrich the dataset**

The starter dataset proves the pipeline (records appear in lists, search, graph, statistics). To reach the full "rich dataset" from the spec, extend `DemoDataService` with private `Seed*` methods for: person aliases/phones/vehicles/weapons/photos, person docs and observations, faction ranks/members/activities, parties, person groups, operations, taskforces, cases, documents (various VS levels), laws, and personnel/promotion history — each guarded by the same name/marker idempotency check. This requires reading the respective entity classes. Track as its own plan if it grows large. Log what is seeded vs skipped in the returned count.

---

## Manual Verification (after all tasks; spec §13)

Run the app: `dotnet run --project NOOSE-Website/NOOSE-Website.csproj` → http://localhost:5174

1. **Demo off, anonymous** → visiting `/personen` redirects to login (unchanged).
2. **Seed**: log in as admin → `/admin/system` → "Demo-Daten einspielen" → snackbar reports added rows. Click again → reports 0 added (idempotent).
3. **Wizard**: toggle "Demo-Modus aktiv" → dialog appears; "Weiter" locked ~10s; wrong word keeps "Aktivieren" disabled; correct word enables it; confirm → saved.
4. **Demo on, anonymous** (log out / private window) → entire site browsable; demo banner shown; classified content + real names visible; **no** create/edit/delete buttons; `/admin/*` not reachable.
5. **Write attempt** via a direct mutate path → `UnauthorizedAccessException` (interceptor backstop).
6. **Circuit longevity**: leave a demo page open >30s → still authenticated as Demo (not kicked by revalidation).
7. **Interactive** features (search, Ctrl+K, graph) work read-only.
8. **Owner login** still works while demo mode is on (Discord login path not hijacked); real admin gets the full principal.
9. **Disable** demo mode (one click) → anonymous visitors locked out again.

## Self-Review Notes

- Spec coverage: §4 setting→T1; §5 identity→T4/T9; §6 wizard→T8; §7 enforcement→T2/T3/T5/T6; §8 seeder→T9; §9 banner→T7. All covered.
- Type consistency: `CurrentUserInfo` 5-arg ctor (T3) matches `System` initializer; `DemoIdentity.AgentId`/`BuildPrincipal` used consistently (T4→T5/T6/T9); `IDemoDataService.SeedAsync(ClaimsPrincipal,CancellationToken)` matches T8 call `DemoData.SeedAsync(_user)`.
- DbSets verified: `db.People`, `db.Factions`. Dialog type verified: `IMudDialogInstance`. Classification values verified.

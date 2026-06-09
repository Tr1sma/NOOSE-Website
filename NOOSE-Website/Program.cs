using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using NOOSE_Website.Authorization;
using NOOSE_Website.Components;
using NOOSE_Website.Components.Account;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Services;

var builder = WebApplication.CreateBuilder(args);

// MudBlazor UI-Dienste.
builder.Services.AddMudServices();

// HttpContext-Zugriff (vom CurrentUserService / Audit genutzt).
builder.Services.AddHttpContextAccessor();

// Datenbank (MySQL 8.0 / MariaDB via Pomelo / EF Core) inkl. Audit-Interceptor.
// Verbindungs-String kommt aus den User Secrets (lokal) bzw. Umgebungsvariablen (Server),
// niemals aus appsettings.json/Code.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection-String 'DefaultConnection' fehlt. Bitte per 'dotnet user-secrets set' hinterlegen.");

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// AutoDetect ermittelt die passende Server-Variante automatisch (lokal MariaDB/XAMPP,
// Produktion MySQL 8.0). Setzt voraus, dass die DB beim Start erreichbar ist.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

// Health-Checks: prueft die DB-Erreichbarkeit (genutzt von /health und der Status-Seite).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// ---- ASP.NET Core Identity (Nutzer = Agent) ----
builder.Services.AddIdentityCore<Agent>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// NOOSE-Claims (Dienstgrad/Status/TRU/Admin) ins Cookie schreiben.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<Agent>, AgentClaimsPrincipalFactory>();

// Kill-Switch: SecurityStamp wird oft revalidiert → Sperre greift praktisch sofort.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromSeconds(30));

// ---- Authentifizierung: Identity-Cookies + Discord-OAuth ----
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});
authentication.AddDiscord(options =>
{
    options.ClientId = builder.Configuration["Authentication:Discord:ClientId"] ?? string.Empty;
    options.ClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"] ?? string.Empty;
    // Damit GetExternalLoginInfoAsync funktioniert, meldet der OAuth-Handler am External-Cookie an.
    options.SignInScheme = IdentityConstants.ExternalScheme;
    options.Scope.Add("email");
    options.SaveTokens = true;
});
authentication.AddIdentityCookies();

// ---- Autorisierung: NOOSE-Policies (Rechte-Matrix Plan.md §6) ----
builder.Services.AddNooseAuthorization();

// ---- Auth-State in interaktive Komponenten + Kill-Switch-Revalidierung ----
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Fachliche Dienste.
builder.Services.AddScoped<IAgentVerwaltungService, AgentVerwaltungService>();
builder.Services.AddScoped<IZugriffsLogService, ZugriffsLogService>();

// Rate-Limit auf den Login-Start (Brute-Force-/Spam-Schutz).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(IdentityComponentsEndpointRouteBuilderExtensions.LoginRateLimitPolicy, limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// Blazor (Interactive Server).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapNooseAccountEndpoints();

// Seeding: technische "Admin"-Rolle sicherstellen (fuer spaetere Nutzung; Admin-Rechte laufen
// aktuell ueber das IstAdmin-Flag des Agents).
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
}

app.Run();

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MudBlazor.Services;
using NOOSE_Website.Authorization;
using NOOSE_Website.Components;
using NOOSE_Website.Components.Account;
using NOOSE_Website.Components.Personen;
using NOOSE_Website.Components.Querschnitt;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Chat;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Infrastructure.Notifications;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

var builder = WebApplication.CreateBuilder(args);

// MudBlazor UI-Dienste.
builder.Services.AddMudServices();

// HttpContext-Zugriff (vom CurrentUserService / Audit genutzt).
builder.Services.AddHttpContextAccessor();

// Datei-Upload-Konfiguration (Foto-Galerie der Personen-Akten).
builder.Services.Configure<FileUploadOptions>(builder.Configuration.GetSection("FileUpload"));

// Datenbank (MySQL 8.0 / MariaDB via Pomelo / EF Core) inkl. Audit-Interceptor.
// Verbindungs-String kommt aus den User Secrets (lokal) bzw. Umgebungsvariablen (Server),
// niemals aus appsettings.json/Code.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection-String 'DefaultConnection' fehlt. Bitte per 'dotnet user-secrets set' hinterlegen.");

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
// Phase 6: zweiter, unabhängiger Interceptor für die Watchlist (gefolgte Akte geändert → Glocke).
builder.Services.AddScoped<WatchlistAenderungInterceptor>();

// AutoDetect ermittelt die passende Server-Variante automatisch (lokal MariaDB/XAMPP,
// Produktion MySQL 8.0). Setzt voraus, dass die DB beim Start erreichbar ist.
//
// Factory statt scoped DbContext: In Blazor Server lebt ein scoped Context den gesamten Circuit lang
// und wird von Seite, Kind-Komponenten und Diensten geteilt. Gleichzeitige Zugriffe (parallele
// OnInitializedAsync-Aufrufe mehrerer Komponenten, der 30-Sekunden-Status-Timer) lösen dann
// „A second operation was started on this context instance" aus. Mit der Factory bekommt jede
// Arbeitseinheit ihren eigenen, kurzlebigen Context. Als Scoped registriert, damit der (scoped)
// Audit-Interceptor mit dem aktuellen Agent aufgelöst wird; AddDbContextFactory registriert
// AppDbContext zusätzlich als scoped Service – das deckt Identity (AddEntityFrameworkStores),
// den Health-Check und das Seeding ab.
builder.Services.AddDbContextFactory<AppDbContext>((sp, options) =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .AddInterceptors(
               sp.GetRequiredService<AuditSaveChangesInterceptor>(),
               sp.GetRequiredService<WatchlistAenderungInterceptor>())
           // Steckbrief-Kinder (Alias/Telefon/…) werden ausschließlich über die – bereits
           // soft-delete-gefilterte – Person geladen. Die EF-Warnung zum Zusammenspiel von
           // Query-Filter und Pflichtnavigation ist daher für unsere Zugriffsmuster unkritisch.
           .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)),
    ServiceLifetime.Scoped);

// Health-Checks: prüft die DB-Erreichbarkeit (genutzt von /health und der Status-Seite).
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

// Kill-Switch: SecurityStamp wird oft revalidiert → Sperre greift praktisch sofort. Identisch zum
// RevalidationInterval des IdentityRevalidatingAuthenticationStateProvider (30 s) → Worst-Case-Latenz ~30 s.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromSeconds(30));

// ---- Authentifizierung: Identity-Cookies + Discord-OAuth ----
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

// Discord nur registrieren, wenn konfiguriert. Der OAuth-Handler ist ein Request-Handler und
// validiert seine ClientId bei JEDER Anfrage – mit leerer ClientId würde die App sonst bei jedem
// Request abstürzen. So läuft die App auch ohne Secrets (Login-Seite zeigt dann einen Hinweis).
var discordClientId = builder.Configuration["Authentication:Discord:ClientId"];
var discordClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"];
if (!string.IsNullOrWhiteSpace(discordClientId) && !string.IsNullOrWhiteSpace(discordClientSecret))
{
    authentication.AddDiscord(options =>
    {
        options.ClientId = discordClientId;
        options.ClientSecret = discordClientSecret;
        // Damit GetExternalLoginInfoAsync funktioniert, meldet der OAuth-Handler am External-Cookie an.
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Scope.Add("email");
        options.SaveTokens = true;
    });
}

authentication.AddIdentityCookies();

// ---- Autorisierung: NOOSE-Policies (Rechte-Matrix Plan.md §6) ----
builder.Services.AddNooseAuthorization();

// ---- Auth-State in interaktive Komponenten + Kill-Switch-Revalidierung ----
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Fachliche Dienste.
builder.Services.AddScoped<IAgentVerwaltungService, AgentVerwaltungService>();
builder.Services.AddScoped<IZugriffsLogService, ZugriffsLogService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IQuellenStorageService, QuellenStorageService>();
// Gemeinsame Aktenzeichen-Vergabe (Person/Fraktion/Gruppe).
builder.Services.AddScoped<IAktenzeichenService, AktenzeichenService>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IPersonDokService, PersonDokService>();
builder.Services.AddScoped<ISteckbriefVorschlagService, SteckbriefVorschlagService>();
// Phase 7: admin-definierte Dok-Vorlagen (Erfassungsmasken).
builder.Services.AddScoped<IDokVorlageService, DokVorlageService>();
// Phase 3a: generische Querschnitts-Dienste (Tags, Kommentare, Quellen).
builder.Services.AddScoped<IQuelleService, QuelleService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IKommentarService, KommentarService>();
// Phase 7: konfigurierbare Custom-Felder je Aktentyp (Admin).
builder.Services.AddScoped<ICustomFeldDefinitionService, CustomFeldDefinitionService>();
builder.Services.AddScoped<ICustomFeldWertService, CustomFeldWertService>();
// Phase 3b: Verknüpfungs-Engine + Person-Beziehungen.
builder.Services.AddScoped<IVerknuepfungService, VerknuepfungService>();
builder.Services.AddScoped<IBeziehungService, BeziehungService>();
// Phase 3c: globale Suche + gespeicherte Suchen.
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IGespeicherteSucheService, GespeicherteSucheService>();
// Phase 4a: Fraktionen.
builder.Services.AddScoped<IFraktionService, FraktionService>();
// Phase 4b: Personengruppen.
builder.Services.AddScoped<IPersonengruppeService, PersonengruppeService>();
// Phase 5a: Parteien.
builder.Services.AddScoped<IParteiService, ParteiService>();
// Phase 5b: Operationen.
builder.Services.AddScoped<IOperationService, OperationService>();
// Phase 5: Vorgangs-/Fallakten (bündeln Personen/Operationen/Observationen/Doks).
builder.Services.AddScoped<IVorgangService, VorgangService>();
// Phase 5c: Taskforces.
builder.Services.AddScoped<ITaskforceService, TaskforceService>();
// Phase 5d: Taskforce-Chat mit @-Verlinkung (Mentions) + Live-Broadcaster.
builder.Services.AddScoped<ITaskforceChatService, TaskforceChatService>();
builder.Services.AddScoped<IMentionService, MentionService>();
builder.Services.AddSingleton<TaskforceChatBroadcaster>();
// Phase 5: Observationen (Überwachungseinträge an Personen).
builder.Services.AddScoped<IObservationService, ObservationService>();
// Phase 5e: Personalakte je Agent (Verlauf, Vermerke, Beförderungsanträge).
builder.Services.AddScoped<IPersonalakteService, PersonalakteService>();
// Phase 5: generischer Antrags-/Posteingang-Workflow (Hochstufungs-Anträge).
builder.Services.AddScoped<IAntragService, AntragService>();
// Lagezentrum (Startseite): Kennzahlen + Aktivitäts-Feed.
builder.Services.AddScoped<IDashboardService, DashboardService>();
// Phase 6: In-App-Benachrichtigungen (Glocke) + Live-Broadcaster.
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<NotificationBroadcaster>();
// Phase 6: Watchlist (Akten folgen → „gefolgte Akte geändert"). Fan-out entkoppelt über den Singleton-Dispatcher.
builder.Services.AddScoped<IWatchlistService, WatchlistService>();
builder.Services.AddScoped<WatchlistFanout>();
builder.Services.AddSingleton<WatchlistDispatcher>();
// Phase 6: Aufgaben/To-Dos & Zuweisungen (Team-Board; Zuweisung/Erledigung → Glocke).
builder.Services.AddScoped<IAufgabeService, AufgabeService>();
// Phase 6: News/Schwarzes Brett + Behörden-Broadcast (Brett für alle; Broadcast/Quittierung nur Führung → Glocke).
builder.Services.AddScoped<IAnkuendigungService, AnkuendigungService>();

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
app.MapNoosePersonenDateiEndpoints();
app.MapNooseQuellenDateiEndpoints();

// Seeding: technische "Admin"-Rolle sicherstellen (für spätere Nutzung; Admin-Rechte laufen
// aktuell über das IstAdmin-Flag des Agents).
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
}

app.Run();

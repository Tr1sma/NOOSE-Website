using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MudBlazor.Services;
using NOOSE_Website.Authorization;
using NOOSE_Website.Components;
using NOOSE_Website.Components.Account;
using NOOSE_Website.Components.Factions;
using NOOSE_Website.Components.People;
using NOOSE_Website.Components.Common;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Infrastructure.Announcements;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Authorization;
using NOOSE_Website.Infrastructure.Threat;
using NOOSE_Website.Infrastructure.Chat;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Infrastructure.Shares;
using NOOSE_Website.Infrastructure.Notifications;
using NOOSE_Website.Infrastructure.Statistics;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Infrastructure.Followups;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Statistics;

// german culture
var germanCulture = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = germanCulture;
CultureInfo.DefaultThreadCurrentUICulture = germanCulture;

var builder = WebApplication.CreateBuilder(args);

// MudBlazor
builder.Services.AddMudServices();

// http context
builder.Services.AddHttpContextAccessor();

// reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// persist keys
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("NOOSE-Website");

// file upload
builder.Services.Configure<FileUploadOptions>(builder.Configuration.GetSection("FileUpload"));

// database setup
using var startupLoggerFactory = LoggerFactory.Create(lb =>
    lb.AddConfiguration(builder.Configuration.GetSection("Logging")).AddConsole());
var (connectionString, serverVersion) = DatabaseConnectionResolver.Resolve(
    builder.Configuration, startupLoggerFactory.CreateLogger("NOOSE.Datenbank"));

// Macht den Circuit-Scope (für den AuthenticationStateProvider) app-weiten Singletons zugänglich.
builder.Services.AddCircuitServicesAccessor();
// Singleton, damit es in die singleton-registrierten SaveChanges-Interceptors passt (s. CurrentUserService).
builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
// write barrier — Singleton: die Interceptors müssen aus dem Root-Provider der (jetzt) Singleton-DbContext-Factory
// aufgelöst werden. Per-Context-Zustand liegt in ConditionalWeakTable, Nutzer kommt aus ICurrentUserService.
builder.Services.AddSingleton<ReadOnlyBarrierInterceptor>();
builder.Services.AddSingleton<AuditSaveChangesInterceptor>();
// watchlist interceptor
builder.Services.AddSingleton<WatchlistChangeInterceptor>();

// db factory — Singleton (Default): die erzeugten Contexts hängen NICHT mehr am Circuit-Scope, der beim
// Dialog-Öffnen/NavMenu-Refresh abgebaut wird (sonst "ObjectDisposedException: IServiceProvider").
// AddDbContextFactory registriert AppDbContext weiterhin zusätzlich als scoped Service (für AgentManagementService,
// Startup-Migration, Health-Check). Die Interceptors sind jetzt Singleton und werden aus dem Root-sp aufgelöst.
builder.Services.AddDbContextFactory<AppDbContext>((sp, options) =>
    options.UseMySql(connectionString, serverVersion)
           .AddInterceptors(
               sp.GetRequiredService<ReadOnlyBarrierInterceptor>(),
               sp.GetRequiredService<AuditSaveChangesInterceptor>(),
               sp.GetRequiredService<WatchlistChangeInterceptor>())
           // ignore nav filter warning
           .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

// health check
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// ---- Identity ----
builder.Services.AddIdentityCore<Agent>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// custom claims
builder.Services.AddScoped<IUserClaimsPrincipalFactory<Agent>, AgentClaimsPrincipalFactory>();

// kill switch
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromSeconds(30));

// ---- Auth ----
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

// optional discord
var discordClientId = builder.Configuration["Authentication:Discord:ClientId"];
var discordClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"];
if (!string.IsNullOrWhiteSpace(discordClientId) && !string.IsNullOrWhiteSpace(discordClientSecret))
{
    authentication.AddDiscord(options =>
    {
        options.ClientId = discordClientId;
        options.ClientSecret = discordClientSecret;
        // external cookie
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Scope.Add("email");
        options.SaveTokens = true;
    });
}

authentication.AddIdentityCookies();

// ---- Authorization ----
builder.Services.AddNooseAuthorization();

// ---- Auth state ----
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// services
builder.Services.AddScoped<IAgentManagementService, AgentManagementService>();
builder.Services.AddScoped<IAccessLogService, AccessLogService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISourcesStorageService, SourcesStorageService>();
builder.Services.AddScoped<IFactionPhotoStorageService, FactionPhotoStorageService>();
// case numbers
builder.Services.AddScoped<ICaseNumberService, CaseNumberService>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IPersonDocService, PersonDocService>();
builder.Services.AddScoped<IProfileSuggestionService, ProfileSuggestionService>();
// doc templates
builder.Services.AddScoped<IDocTemplateService, DocTemplateService>();
// documents
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
builder.Services.AddScoped<IPlaceholderService, PlaceholderService>();
// recency
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IRecencyService, RecencyService>();
builder.Services.AddScoped<IFollowupService, FollowupService>();
// followup worker
builder.Services.AddHostedService<FollowupDueWorker>();
// cross-cutting
builder.Services.AddScoped<ISourceService, SourceService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<ICommentService, CommentService>();
// custom fields
builder.Services.AddScoped<ICustomFieldDefinitionService, CustomFieldDefinitionService>();
builder.Services.AddScoped<ICustomFieldValueService, CustomFieldValueService>();
// links
builder.Services.AddScoped<ILinkService, LinkService>();
builder.Services.AddScoped<IRelationService, RelationService>();
// graph
builder.Services.AddScoped<IGraphService, GraphService>();
builder.Services.AddScoped<ILinkSuggestionService, LinkSuggestionService>();
// timeline
builder.Services.AddScoped<ITimelineService, TimelineService>();
builder.Services.AddScoped<IOrgChartService, OrgChartService>();
// calendar
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
// threat score
builder.Services.AddScoped<IThreatScoreConfigService, ThreatScoreConfigService>();
builder.Services.AddScoped<IThreatScoreService, ThreatScoreService>();
// score worker
builder.Services.AddHostedService<ThreatScoreSweepWorker>();
// search
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<ISavedSearchService, SavedSearchService>();
// factions
builder.Services.AddScoped<IFactionService, FactionService>();
// groups
builder.Services.AddScoped<IPersonGroupService, PersonGroupService>();
// parties
builder.Services.AddScoped<IPartyService, PartyService>();
// operations
builder.Services.AddScoped<IOperationService, OperationService>();
// cases
builder.Services.AddScoped<ICaseService, CaseService>();
// taskforces
builder.Services.AddScoped<ITaskforceService, TaskforceService>();
// chat
builder.Services.AddScoped<ITaskforceChatService, TaskforceChatService>();
builder.Services.AddScoped<IMentionService, MentionService>();
builder.Services.AddSingleton<TaskforceChatBroadcaster>();
// observations
builder.Services.AddScoped<IObservationService, ObservationService>();
// personnel files
builder.Services.AddScoped<IPersonnelFileService, PersonnelFileService>();
// training
builder.Services.AddScoped<ITrainingModuleService, TrainingModuleService>();
// requests
builder.Services.AddScoped<IRequestService, RequestService>();
// dashboard
builder.Services.AddScoped<IDashboardService, DashboardService>();
// statistics
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
// situation reports
builder.Services.AddScoped<ISituationReportService, SituationReportService>();
builder.Services.AddHostedService<SituationReportWorker>();
// notifications
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<NotificationBroadcaster>();
// shares badge
builder.Services.AddSingleton<SharesBroadcaster>();
// board badge
builder.Services.AddSingleton<AcknowledgmentBroadcaster>();
// watchlist
builder.Services.AddScoped<IWatchlistService, WatchlistService>();
builder.Services.AddScoped<WatchlistFanout>();
builder.Services.AddSingleton<WatchlistDispatcher>();
// jobs
builder.Services.AddScoped<IJobService, JobService>();
// announcements
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
// system settings
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
// per-user navigation preferences
builder.Services.AddScoped<INavPreferencesService, NavPreferencesService>();
// route → section/record label resolution (breadcrumbs, recents)
builder.Services.AddScoped<INavLabelService, NavLabelService>();
// per partner-rank visibility config
builder.Services.AddScoped<IPartnerVisibilityPolicyService, PartnerVisibilityPolicyService>();
builder.Services.AddScoped<ILawService, LawService>();
builder.Services.AddScoped<ILibraryStorageService, LibraryStorageService>();
builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IPersonMergeService, PersonMergeService>();
builder.Services.AddScoped<IPartnerShareService, PartnerShareService>();

// rate limit
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

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // large message limit
        options.MaximumReceiveMessageSize = 5 * 1024 * 1024;
    });

var app = builder.Build();

// forwarded headers first
app.UseForwardedHeaders();

// force de-DE
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("de-DE")
    .AddSupportedCultures("de-DE")
    .AddSupportedUICultures("de-DE"));

// pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // hsts
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
app.MapNoosePeopleFileEndpoints();
app.MapNooseSourcesFileEndpoints();
app.MapNooseFactionsFileEndpoints();
app.MapNooseLibraryFileEndpoints();
app.MapNooseSystemEndpoints();
app.MapNooseStatisticsExportEndpoints();

// startup
using (var scope = app.Services.CreateScope())
{
    // auto migrate
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // ensure admin role
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
}

app.Run();

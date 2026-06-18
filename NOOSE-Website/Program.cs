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

var germanCulture = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = germanCulture;
CultureInfo.DefaultThreadCurrentUICulture = germanCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMudServices();

builder.Services.AddHttpContextAccessor();

// trust only the loopback reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// persist data-protection keys to App_Data so a restart doesn't sign everyone out
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("NOOSE-Website");

builder.Services.Configure<FileUploadOptions>(builder.Configuration.GetSection("FileUpload"));

using var startupLoggerFactory = LoggerFactory.Create(lb =>
    lb.AddConfiguration(builder.Configuration.GetSection("Logging")).AddConsole());
var (connectionString, serverVersion) = DatabaseConnectionResolver.Resolve(
    builder.Configuration, startupLoggerFactory.CreateLogger("NOOSE.Datenbank"));

// exposes the circuit scope (for AuthenticationStateProvider) to app-wide singletons
builder.Services.AddCircuitServicesAccessor();
// Singleton so it fits the singleton-registered SaveChanges interceptors (see CurrentUserService)
builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
// interceptors must be Singleton: resolved from the root provider of the singleton DbContext factory; per-context state lives in a ConditionalWeakTable
builder.Services.AddSingleton<ReadOnlyBarrierInterceptor>();
builder.Services.AddSingleton<AuditSaveChangesInterceptor>();
builder.Services.AddSingleton<WatchlistChangeInterceptor>();

// Singleton factory so created contexts don't hang off the circuit scope (avoids ObjectDisposedException on dialog/nav refresh)
builder.Services.AddDbContextFactory<AppDbContext>((sp, options) =>
    options.UseMySql(connectionString, serverVersion)
           .AddInterceptors(
               sp.GetRequiredService<ReadOnlyBarrierInterceptor>(),
               sp.GetRequiredService<AuditSaveChangesInterceptor>(),
               sp.GetRequiredService<WatchlistChangeInterceptor>())
           .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

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

builder.Services.AddScoped<IUserClaimsPrincipalFactory<Agent>, AgentClaimsPrincipalFactory>();

// kill switch: revalidate the security stamp every 30s
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromSeconds(30));

// ---- Auth ----
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

// Discord is optional: only wired when client id+secret are configured
var discordClientId = builder.Configuration["Authentication:Discord:ClientId"];
var discordClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"];
if (!string.IsNullOrWhiteSpace(discordClientId) && !string.IsNullOrWhiteSpace(discordClientSecret))
{
    authentication.AddDiscord(options =>
    {
        options.ClientId = discordClientId;
        options.ClientSecret = discordClientSecret;
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

// ---- Services ----
builder.Services.AddScoped<IAgentManagementService, AgentManagementService>();
builder.Services.AddScoped<IAccessLogService, AccessLogService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISourcesStorageService, SourcesStorageService>();
builder.Services.AddScoped<IFactionPhotoStorageService, FactionPhotoStorageService>();
builder.Services.AddScoped<ICaseNumberService, CaseNumberService>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IPersonDocService, PersonDocService>();
builder.Services.AddScoped<IProfileSuggestionService, ProfileSuggestionService>();
builder.Services.AddScoped<IDocTemplateService, DocTemplateService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
builder.Services.AddScoped<IPlaceholderService, PlaceholderService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IRecencyService, RecencyService>();
builder.Services.AddScoped<IFollowupService, FollowupService>();
builder.Services.AddHostedService<FollowupDueWorker>();
builder.Services.AddScoped<ISourceService, SourceService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ICustomFieldDefinitionService, CustomFieldDefinitionService>();
builder.Services.AddScoped<ICustomFieldValueService, CustomFieldValueService>();
builder.Services.AddScoped<ILinkService, LinkService>();
builder.Services.AddScoped<IRelationService, RelationService>();
builder.Services.AddScoped<IGraphService, GraphService>();
builder.Services.AddScoped<ILinkSuggestionService, LinkSuggestionService>();
builder.Services.AddScoped<ITimelineService, TimelineService>();
builder.Services.AddScoped<IOrgChartService, OrgChartService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IThreatScoreConfigService, ThreatScoreConfigService>();
builder.Services.AddScoped<IThreatScoreService, ThreatScoreService>();
builder.Services.AddHostedService<ThreatScoreSweepWorker>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<ISavedSearchService, SavedSearchService>();
builder.Services.AddScoped<IFactionService, FactionService>();
builder.Services.AddScoped<IPersonGroupService, PersonGroupService>();
builder.Services.AddScoped<IPartyService, PartyService>();
builder.Services.AddScoped<IOperationService, OperationService>();
builder.Services.AddScoped<ICaseService, CaseService>();
builder.Services.AddScoped<ITaskforceService, TaskforceService>();
builder.Services.AddScoped<ITaskforceChatService, TaskforceChatService>();
builder.Services.AddScoped<IMentionService, MentionService>();
builder.Services.AddSingleton<TaskforceChatBroadcaster>();
builder.Services.AddScoped<IObservationService, ObservationService>();
builder.Services.AddScoped<IPersonnelFileService, PersonnelFileService>();
builder.Services.AddScoped<ITrainingModuleService, TrainingModuleService>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<ISituationReportService, SituationReportService>();
builder.Services.AddHostedService<SituationReportWorker>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<NotificationBroadcaster>();
builder.Services.AddSingleton<SharesBroadcaster>();
builder.Services.AddSingleton<AcknowledgmentBroadcaster>();
builder.Services.AddScoped<IWatchlistService, WatchlistService>();
builder.Services.AddScoped<WatchlistFanout>();
builder.Services.AddSingleton<WatchlistDispatcher>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddScoped<INavPreferencesService, NavPreferencesService>();
builder.Services.AddScoped<INavLabelService, NavLabelService>();
builder.Services.AddScoped<IPartnerVisibilityPolicyService, PartnerVisibilityPolicyService>();
builder.Services.AddScoped<ILawService, LawService>();
builder.Services.AddScoped<ILibraryStorageService, LibraryStorageService>();
builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IPersonMergeService, PersonMergeService>();
builder.Services.AddScoped<IPartnerShareService, PartnerShareService>();

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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // 5 MB: the RichTextEditor streams full HTML over SignalR — do not lower
        options.MaximumReceiveMessageSize = 5 * 1024 * 1024;
    });

var app = builder.Build();

// forwarded headers must run first
app.UseForwardedHeaders();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("de-DE")
    .AddSupportedCultures("de-DE")
    .AddSupportedUICultures("de-DE"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
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

// apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
}

app.Run();

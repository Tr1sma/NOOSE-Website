using Microsoft.AspNetCore.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>
/// Registriert alle NOOSE-Authorization-Policies und -Handler an einem Ort (von <c>Program.cs</c>
/// aufgerufen). Spiegelt die Rechte-Matrix aus <c>Plan.md</c> §6 wider.
/// </summary>
public static class AuthorizationRegistration
{
    public static IServiceCollection AddNooseAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, DienstgradAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, VerschlusssacheAuthorizationHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AktiverAgent, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.GetStatus() == AgentStatus.Aktiv))
            .AddPolicy(Policies.Fuehrung, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new DienstgradRequirement(Dienstgrad.SupervisorySpecialAgent)))
            .AddPolicy(Policies.Admin, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IstAdmin()))
            // Schreibrecht / Nur-Lese-Aufsicht: steuern Mutations-Controls bzw. den Nur-Lese-Banner.
            .AddPolicy(Policies.Schreibrecht, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.DarfSchreiben()))
            .AddPolicy(Policies.NurLeseModus, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IstNurLeser()))
            // Seiten-Zugang: lassen zusätzlich die Nur-Lese-Aufsicht zum reinen Lesen zu. Bewusst als
            // RequireAssertion (kein DienstgradRequirement), damit der DienstgradAuthorizationHandler – und
            // damit die an die Rang-Policies gebundenen Button-AuthorizeViews – unberührt bleibt.
            .AddPolicy(Policies.FuehrungSeite, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IstFuehrung() || ctx.User.IstNurLeser()))
            .AddPolicy(Policies.HoechsteEinstufungSeite, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.DarfHoechsteEinstufung() || ctx.User.IstNurLeser()))
            .AddPolicy(Policies.AdminSeite, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IstAdmin() || ctx.User.IstNurLeser()))
            .AddPolicy(Policies.HoechsteEinstufung, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new DienstgradRequirement(Dienstgrad.SeniorSpecialAgent)))
            .AddPolicy(Policies.BefoerderungEntscheiden, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new DienstgradRequirement(Dienstgrad.DeputyDirector)))
            .AddPolicy(Policies.Verschlusssache, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new VerschlusssacheRequirement()));

        return services;
    }
}

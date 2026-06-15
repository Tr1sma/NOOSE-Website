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
        services.AddScoped<IAuthorizationHandler, RankAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ClassifiedAuthorizationHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.ActiveAgent, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.GetStatus() == AgentStatus.Active))
            .AddPolicy(Policies.Leadership, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new RankRequirement(Rank.SupervisorySpecialAgent)))
            .AddPolicy(Policies.Admin, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IsAdmin()))
            // Schreibrecht / Nur-Lese-Aufsicht: steuern Mutations-Controls bzw. den Nur-Lese-Banner.
            .AddPolicy(Policies.WriteAccess, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.MayWrite()))
            .AddPolicy(Policies.OnlyReadMode, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IsOnlyReader()))
            // Seiten-Zugang: lassen zusätzlich die Nur-Lese-Aufsicht zum reinen Lesen zu. Bewusst als
            // RequireAssertion (kein DienstgradRequirement), damit der DienstgradAuthorizationHandler – und
            // damit die an die Rang-Policies gebundenen Button-AuthorizeViews – unberührt bleibt.
            .AddPolicy(Policies.LeadershipPage, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IsLeadership() || ctx.User.IsOnlyReader()))
            .AddPolicy(Policies.HighestClassificationPage, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.MayHighestClassification() || ctx.User.IsOnlyReader()))
            .AddPolicy(Policies.AdminPage, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IsAdmin() || ctx.User.IsOnlyReader()))
            .AddPolicy(Policies.HighestClassification, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new RankRequirement(Rank.SeniorSpecialAgent)))
            .AddPolicy(Policies.PromotionDecide, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new RankRequirement(Rank.DeputyDirector)))
            .AddPolicy(Policies.Classified, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new ClassifiedRequirement()));

        return services;
    }
}

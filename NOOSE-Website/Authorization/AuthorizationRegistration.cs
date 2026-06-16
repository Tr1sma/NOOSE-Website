using Microsoft.AspNetCore.Authorization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Authorization;

/// <summary>Registers all authorization policies.</summary>
public static class AuthorizationRegistration
{
    public static IServiceCollection AddNooseAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, RankAuthorizationHandler>();

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
            // write guards
            .AddPolicy(Policies.WriteAccess, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.MayWrite()))
            .AddPolicy(Policies.OnlyReadMode, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.IsOnlyReader()))
            // page access
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
                .AddRequirements(new RankRequirement(Rank.DeputyDirector)));

        return services;
    }
}

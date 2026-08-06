using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Cases;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IApplicationCaseService" />
public class ApplicationCaseService(
    IDbContextFactory<AppDbContext> dbFactory,
    IRecruitingAutomationService automation,
    ICaseService cases,
    IDocumentService documents,
    IDocumentTemplateService templates,
    ISourceService sources,
    IPlaceholderService placeholders,
    ILogger<ApplicationCaseService> logger) : IApplicationCaseService
{
    public async Task EnsureSecurityCheckCaseAsync(string bewerbungId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // read-only supervisors never provision records
        if (!actor.MayWrite())
        {
            return;
        }

        var config = await automation.GetAsync(cancellationToken);
        if (!config.AutoCaseEnabled)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bewerbung = await db.Bewerbungen.FirstOrDefaultAsync(b => b.Id == bewerbungId, cancellationToken);
        if (bewerbung is null || bewerbung.LinkedCaseId is not null)
        {
            return; // idempotent: already provisioned
        }

        var name = bewerbung.Name;

        // 1) case first, then commit the link so a re-assignment never duplicates the Vorgang
        var @case = await cases.CreateAsync(new CaseInput
        {
            Title = $"Bewerbungsverfahren | {name}",
            Status = CaseStatus.Open,
            Classification = Classification.Unknown,
            SecrecyLevel = DocumentClassification.None,
        }, actor, cancellationToken);
        bewerbung.LinkedCaseId = @case.Id;
        await db.SaveChangesAsync(cancellationToken);

        // 2) document from the configured template (built-in body as fallback if it was deleted)
        var template = await templates.GetAsync(config.TemplateId, cancellationToken);
        var body = template?.ContentHtml ?? ApplicationTemplates.SecurityCheckBody;
        var html = await placeholders.ApplyAsync(body, nameof(Case), @case.Id, actor, cancellationToken);
        var docTitle = $"Sicherheitsüberprüfung | {name}";
        var document = await documents.CreateAsync(new DocumentInput
        {
            Title = docTitle,
            ContentHtml = html,
            Classification = DocumentClassification.None,
        }, actor, cancellationToken);

        // 3) file the document under the case
        await sources.CreateAsync(nameof(Case), @case.Id, new SourceInput
        {
            Type = SourceType.Document,
            Title = docTitle,
            TargetType = nameof(Document),
            TargetId = document.Id,
        }, actor, cancellationToken);

        logger.LogInformation("Auto-provisioned case {CaseId} + document {DocId} for application {AppId}.",
            @case.Id, document.Id, bewerbungId);
    }
}

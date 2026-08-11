using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.CounterIntel;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Evidence;
using NOOSE_Website.Models.Kasse;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Llm.Tools;
using NSubstitute;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>Builds the wide NOOSEI tools without spelling out every collaborator at each call site.</summary>
/// <remarks>Each of them fans out over one list service per record type, so the constructors grow with every type
/// that becomes enumerable. Named optional parameters keep a test to the services it actually asserts on, and a
/// new type adds one line here instead of breaking every test that ever built the tool.</remarks>
public static class NooseiToolHost
{
    public static FilterRecordsTool Filter(
        IPersonService? people = null,
        IFactionService? factions = null,
        IPersonGroupService? groups = null,
        IPartyService? parties = null,
        ICaseService? cases = null,
        IOperationService? operations = null,
        ILawService? laws = null,
        ITaskforceService? taskforces = null,
        IDocumentService? documents = null,
        IMeetingService? meetings = null,
        IJobService? jobs = null,
        IInformantService? informants = null,
        IEvidenceService? evidence = null,
        ILibraryService? library = null,
        IAbsenceService? absences = null,
        IAnnouncementService? announcements = null,
        IFinancingService? financing = null,
        IBewerbungService? applications = null)
        => new(
            people ?? Substitute.For<IPersonService>(),
            factions ?? Substitute.For<IFactionService>(),
            groups ?? Substitute.For<IPersonGroupService>(),
            parties ?? Substitute.For<IPartyService>(),
            cases ?? Substitute.For<ICaseService>(),
            operations ?? Substitute.For<IOperationService>(),
            laws ?? Substitute.For<ILawService>(),
            taskforces ?? Substitute.For<ITaskforceService>(),
            documents ?? Substitute.For<IDocumentService>(),
            meetings ?? Substitute.For<IMeetingService>(),
            jobs ?? Substitute.For<IJobService>(),
            informants ?? Substitute.For<IInformantService>(),
            evidence ?? Substitute.For<IEvidenceService>(),
            library ?? Substitute.For<ILibraryService>(),
            absences ?? Substitute.For<IAbsenceService>(),
            announcements ?? Substitute.For<IAnnouncementService>(),
            financing ?? Substitute.For<IFinancingService>(),
            applications ?? Substitute.For<IBewerbungService>());

    /// <summary>The area tool with every service the caller did not supply answering nothing.</summary>
    /// <remarks>Empty rather than a bare substitute: an unconfigured NSubstitute hands back a null list, which no
    /// real service ever does, and a tool written against that null would be guarding against a bug it cannot have.</remarks>
    public static ReadAreaTool Area(
        SqliteTestContext ctx,
        IKassenService? treasury = null,
        IEvidenceService? evidence = null,
        IAnnouncementService? announcements = null,
        ICounterIntelService? counterIntel = null,
        IFollowupService? followups = null,
        ITrainingModuleService? training = null)
        => new(
            ctx.Factory,
            treasury ?? QuietTreasury(),
            evidence ?? QuietEvidence(),
            announcements ?? QuietBoard(),
            counterIntel ?? QuietCounterIntel(),
            followups ?? QuietFollowups(),
            training ?? QuietTraining());

    private static IKassenService QuietTreasury()
    {
        var treasury = Substitute.For<IKassenService>();
        treasury.GetSummariesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KassenKontoSummary>>([]));
        treasury.GetLedgerAsync(Arg.Any<KassenKonto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<KassenBuchungDisplay>>([]));
        return treasury;
    }

    private static IEvidenceService QuietEvidence()
    {
        var evidence = Substitute.For<IEvidenceService>();
        evidence.GetItemsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EvidenceItemDisplay>()));
        return evidence;
    }

    private static IAnnouncementService QuietBoard()
    {
        var announcements = Substitute.For<IAnnouncementService>();
        announcements.GetBoardAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<AnnouncementRow>()));
        return announcements;
    }

    private static ICounterIntelService QuietCounterIntel()
    {
        var counterIntel = Substitute.For<ICounterIntelService>();
        counterIntel.GetOverviewAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CounterIntelOverview(0, 0, 0, 0, 30)));
        counterIntel.GetFlagsAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<InsiderFlag>>([]));
        return counterIntel;
    }

    private static IFollowupService QuietFollowups()
    {
        var followups = Substitute.For<IFollowupService>();
        followups.GetMyDueAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<FollowupDashboardItem>()));
        return followups;
    }

    private static ITrainingModuleService QuietTraining()
    {
        var training = Substitute.For<ITrainingModuleService>();
        training.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<TrainingModule>()));
        return training;
    }
}

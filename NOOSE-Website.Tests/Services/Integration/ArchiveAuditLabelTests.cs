using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The archive actions carry their own label, or the log reads as an ordinary edit.</summary>
public sealed class ArchiveAuditLabelTests
{
    [Fact]
    public void Archive_actions_have_german_labels()
    {
        Assert.Equal("Archiviert", AuditActionDisplay.Name(AuditAction.Archived));
        Assert.Equal("Aus dem Archiv geholt", AuditActionDisplay.Name(AuditAction.Unarchived));
    }

    [Fact]
    public void Archive_actions_are_offered_in_the_log_filter()
    {
        Assert.Contains(AuditAction.Archived, AuditActionDisplay.All);
        Assert.Contains(AuditAction.Unarchived, AuditActionDisplay.All);
    }

    [Fact]
    public void Timeline_titles_say_archive_not_changed()
    {
        var archived = TimelineDisplay.MapAudit(nameof(Person), AuditAction.Archived);
        var back = TimelineDisplay.MapAudit(nameof(Person), AuditAction.Unarchived);
        Assert.Contains("archiviert", archived.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Archiv", back.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("geändert", archived.Title, StringComparison.OrdinalIgnoreCase);
    }
}

using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure.Audit;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>What the change protocol may never carry, held by a registry instead of by care during review.</summary>
/// <remarks>
/// The integration halves live next to the services that write these rows: <c>TipServiceTests</c> and
/// <c>TicketServiceTests</c> each attach the real interceptor and assert that an edit leaves no wording behind.
/// </remarks>
public class AuditRedactionTests
{
    [Fact]
    public void Citizen_correspondence_keeps_its_wording_out_of_the_protocol()
    {
        Assert.True(AuditRedaction.Hides(nameof(HinweisNachricht), nameof(HinweisNachricht.Text)));
        Assert.True(AuditRedaction.Hides(nameof(TicketNachricht), nameof(TicketNachricht.Text)));
    }

    [Fact]
    public void Everything_else_stays_visible()
    {
        // a note on a file is supposed to show its before and after; CommentServiceTests holds that line
        Assert.False(AuditRedaction.Hides(nameof(Comment), nameof(Comment.Text)));
        // the rule is scoped to the field, not to the whole row
        Assert.False(AuditRedaction.Hides(nameof(TicketNachricht), nameof(TicketNachricht.Audience)));
        Assert.False(AuditRedaction.Hides(nameof(Hinweis), nameof(Hinweis.Text)));
        Assert.False(AuditRedaction.Hides("Person", "Name"));
    }

    [Fact]
    public void The_lookup_is_case_sensitive_on_the_clr_names_the_interceptor_stamps()
    {
        Assert.False(AuditRedaction.Hides("ticketnachricht", "Text"));
        Assert.False(AuditRedaction.Hides(nameof(TicketNachricht), "text"));
    }
}

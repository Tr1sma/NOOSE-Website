using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The limits and the transition matrix of a citizen ticket.</summary>
public class TicketRulesTests
{
    [Fact]
    public void A_message_is_as_long_as_a_tip_message()
    {
        // two numbers over the same thing drift; the constant is derived rather than repeated
        Assert.Equal(TipRules.MaxMessageLength, TicketRules.MaxMessageLength);
    }

    [Fact]
    public void The_agency_sender_is_a_constant_and_names_no_agent()
    {
        Assert.Equal("NOOSE – Führungsebene", TicketRules.AgencySender);
    }

    [Theory]
    [InlineData(TicketStatus.Offen, true)]
    [InlineData(TicketStatus.InBearbeitung, true)]
    [InlineData(TicketStatus.WartetAufBuerger, true)]
    [InlineData(TicketStatus.Geschlossen, false)]
    public void Open_covers_everything_but_the_closed_status(TicketStatus status, bool expected)
    {
        Assert.Equal(expected, TicketRules.IsOpen(status));
    }

    [Theory]
    [InlineData(TicketStatus.Offen, TicketStatus.InBearbeitung)]
    [InlineData(TicketStatus.Offen, TicketStatus.WartetAufBuerger)]
    [InlineData(TicketStatus.Offen, TicketStatus.Geschlossen)]
    [InlineData(TicketStatus.InBearbeitung, TicketStatus.WartetAufBuerger)]
    [InlineData(TicketStatus.InBearbeitung, TicketStatus.Geschlossen)]
    [InlineData(TicketStatus.WartetAufBuerger, TicketStatus.InBearbeitung)]
    [InlineData(TicketStatus.WartetAufBuerger, TicketStatus.Geschlossen)]
    [InlineData(TicketStatus.Geschlossen, TicketStatus.InBearbeitung)]
    public void Allowed_transitions(TicketStatus from, TicketStatus to)
    {
        Assert.True(TicketRules.IsTransitionAllowed(from, to));
    }

    [Theory]
    [InlineData(TicketStatus.Offen, TicketStatus.Offen)]
    [InlineData(TicketStatus.Geschlossen, TicketStatus.Geschlossen)]
    // reopening lands in handling, never back at "untouched": someone has read it by now
    [InlineData(TicketStatus.Geschlossen, TicketStatus.Offen)]
    [InlineData(TicketStatus.Geschlossen, TicketStatus.WartetAufBuerger)]
    [InlineData(TicketStatus.InBearbeitung, TicketStatus.Offen)]
    [InlineData(TicketStatus.WartetAufBuerger, TicketStatus.Offen)]
    public void Refused_transitions(TicketStatus from, TicketStatus to)
    {
        Assert.False(TicketRules.IsTransitionAllowed(from, to));
    }

    [Fact]
    public void Allowed_targets_never_contain_the_current_status()
    {
        foreach (var status in TicketStatusDisplay.All)
        {
            Assert.DoesNotContain(status, TicketRules.AllowedTargets(status));
        }
    }

    [Fact]
    public void A_closed_ticket_offers_exactly_one_way_out()
    {
        var targets = TicketRules.AllowedTargets(TicketStatus.Geschlossen);
        Assert.Equal(new[] { TicketStatus.InBearbeitung }, targets);
    }

    [Fact]
    public void Both_caps_are_positive_and_the_open_one_is_the_tighter()
    {
        Assert.True(TicketRules.MaxOpen > 0);
        Assert.True(TicketRules.PerDay > 0);
        Assert.True(TicketRules.MaxOpen <= TicketRules.PerDay);
        Assert.Equal(TimeSpan.FromHours(24), TicketRules.QuotaWindow);
    }

    [Fact]
    public void Every_status_display_has_a_citizen_wording()
    {
        foreach (var status in TicketStatusDisplay.All)
        {
            Assert.NotEqual("—", TicketStatusDisplay.Name(status));
            Assert.NotEqual("—", TicketStatusDisplay.CitizenName(status));
        }
    }
}

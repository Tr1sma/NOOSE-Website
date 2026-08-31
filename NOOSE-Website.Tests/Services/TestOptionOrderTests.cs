using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Unit tests for <see cref="TestOptionOrder"/>.</summary>
public class TestOptionOrderTests
{
    private static List<BewerbungTestOption> Options(int count)
        => Enumerable.Range(1, count)
            .Select(i => new BewerbungTestOption { Id = $"o{i}", Label = $"Option {i}", Sorting = i })
            .ToList();

    [Fact]
    public void For_KeepOrder_ReturnsAuthoringOrder()
    {
        var result = TestOptionOrder.For("as1", keepOrder: true, Options(5).OrderByDescending(o => o.Sorting));

        Assert.Equal(new[] { "o1", "o2", "o3", "o4", "o5" }, result.Select(o => o.Id).ToArray());
    }

    [Fact]
    public void For_SameAssignment_IsDeterministic()
    {
        var first = TestOptionOrder.For("as1", keepOrder: false, Options(8));
        var second = TestOptionOrder.For("as1", keepOrder: false, Options(8));

        Assert.Equal(first.Select(o => o.Id), second.Select(o => o.Id));
    }

    [Fact]
    public void For_Shuffled_DiffersFromAuthoringOrder()
    {
        var result = TestOptionOrder.For("as1", keepOrder: false, Options(8));

        Assert.NotEqual(new[] { "o1", "o2", "o3", "o4", "o5", "o6", "o7", "o8" }, result.Select(o => o.Id).ToArray());
    }

    [Fact]
    public void For_DifferentAssignments_ProduceDifferentOrders()
    {
        var a = TestOptionOrder.For("as1", keepOrder: false, Options(10)).Select(o => o.Id).ToArray();
        var b = TestOptionOrder.For("as2", keepOrder: false, Options(10)).Select(o => o.Id).ToArray();

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void For_Shuffled_KeepsEveryOption()
    {
        var result = TestOptionOrder.For("as1", keepOrder: false, Options(6));

        Assert.Equal(6, result.Count);
        Assert.Equal(new[] { "o1", "o2", "o3", "o4", "o5", "o6" }, result.Select(o => o.Id).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void For_Empty_ReturnsEmpty()
    {
        Assert.Empty(TestOptionOrder.For("as1", keepOrder: false, []));
    }
}

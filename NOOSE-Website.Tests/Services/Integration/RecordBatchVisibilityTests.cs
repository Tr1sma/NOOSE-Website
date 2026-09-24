using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Every type a batch may write to has a real gate: a record that is not there is never "visible".</summary>
/// <remarks><see cref="Visibility.IsRecordVisibleAsync"/> answers an unknown type as visible and a personnel file by
/// rank alone; a type in one of the batch sets that lands in either gap would let a forged id through.</remarks>
public sealed class RecordBatchVisibilityTests
{
    public static TheoryData<string> BatchTypes()
    {
        var data = new TheoryData<string>();
        foreach (var t in RecordBatch.Linkable.Concat(RecordBatch.Taggable).Concat(RecordBatch.Followable).Distinct().Order())
        {
            data.Add(t);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(BatchTypes))]
    public async Task A_missing_record_is_refused_even_to_the_director(string type)
    {
        using var ctx = new SqliteTestContext();
        using var db = ctx.NewContext();
        var scope = ViewerScope.From(ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).AsAdmin().AsHrb().Build());

        Assert.False(await RecordBatch.ExistsAndVisibleAsync(db, type, "gibt-es-nicht", scope));
    }
}

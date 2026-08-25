using NOOSE_Website.Data.Entities.Recruiting;
using System.Text;

namespace NOOSE_Website.Services;

/// <summary>Decides the order answer options are served to an applicant in.</summary>
/// <remarks>Creation order is the answer key: an author who types the correct option first makes
/// position 1 the key for every applicant. Shuffling per assignment breaks that correlation while
/// staying stable across reloads, so the page does not reshuffle under the applicant.</remarks>
public static class TestOptionOrder
{
    /// <summary>Order options for one assignment; authoring order only when the question opts out.</summary>
    public static List<BewerbungTestOption> For(string assignmentId, bool keepOrder, IEnumerable<BewerbungTestOption> options)
        => keepOrder
            ? options.OrderBy(o => o.Sorting).ToList()
            : options.OrderBy(o => Seed(assignmentId, o.Id)).ThenBy(o => o.Id, StringComparer.Ordinal).ToList();

    /// <summary>Deterministic FNV-1a over assignment + option id.</summary>
    /// <remarks>string.GetHashCode is randomised per process, which would reshuffle on every app
    /// restart; this must survive restarts so one applicant always sees one order.</remarks>
    private static uint Seed(string assignmentId, string optionId)
    {
        var bytes = Encoding.UTF8.GetBytes($"{assignmentId}:{optionId}");
        var hash = 2166136261u;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= 16777619u;
        }
        return hash;
    }
}

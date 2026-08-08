using System.Runtime.CompilerServices;

namespace NOOSE_Website.Tests.Services;

/// <summary>Structural guard: the meter cannot be bypassed by adding a new caller.</summary>
public class NooseiGatewayCoverageTests
{
    /// <summary>The transport, the gateway that meters it, and the composition root that wires them.
    /// Nothing else may reach the model — a fourth entry here means a feature is spending untracked tokens.</summary>
    private static readonly string[] Allowed = ["LlmService.cs", "NooseiGateway.cs", "Program.cs"];

    private static string SourceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website"));

    [Fact]
    public void OnlyTheGatewayAndTheTransportUseILlmService()
    {
        var root = SourceRoot();
        Assert.True(Directory.Exists(root), $"Quellordner nicht gefunden: {root}");

        var offenders = Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !Allowed.Contains(Path.GetFileName(f)))
            .Where(f => File.ReadAllText(f).Contains("ILlmService", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(root, f))
            .Order()
            .ToArray();

        Assert.Empty(offenders);
    }
}

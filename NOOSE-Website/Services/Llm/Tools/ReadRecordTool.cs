using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Reads one record as a German dossier, assembled at the asking agent's own scope.</summary>
public sealed class ReadRecordTool(IDbContextFactory<AppDbContext> dbFactory, IAccessLogService accessLog) : INooseiTool
{
    public string Name => "lies_akte";

    public string Description =>
        "Liest eine Akte vollständig: Stammdaten, Mitglieder, Doks, Quellen, Kommentare und Verknüpfungen. "
        + "Die Id kommt aus suche_akten.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["typ", "id"],
          "properties": {
            "typ": { "type": "string", "enum": {{NooseiRecordTypes.EnumJson}} },
            "id": { "type": "string", "description": "Id der Akte, wie von suche_akten geliefert." },
            "umfang": { "type": "string", "enum": ["kompakt", "voll"],
                        "description": "kompakt = Stammdaten, voll = vollständiges Dossier." }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var type = NooseiRecordTypes.Clr(NooseiLimits.Text(arguments, "typ"));
        var id = NooseiLimits.Text(arguments, "id");
        if (type is null || id is null)
        {
            return NooseiToolResult.NotFound();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // gate first, build second: the builder masks links but does not decide whether the record itself is visible
        if (!await Visibility.IsRecordVisibleAsync(db, type, id, context.Scope, cancellationToken))
        {
            return NooseiToolResult.NotFound();
        }

        var dossier = await DossierContextBuilder.BuildAsync(db, type, id, context.Scope, cancellationToken);
        if (dossier is null)
        {
            return NooseiToolResult.NotFound();
        }

        // a read through NOOSEI is still a read: it belongs in the access log like any other
        try
        {
            await accessLog.LogViewAsync(type, id, cancellationToken);
        }
        catch (Exception) { /* best effort */ }

        var budget = string.Equals(NooseiLimits.Text(arguments, "umfang"), "kompakt", StringComparison.OrdinalIgnoreCase)
            ? NooseiLimits.MaxCompactRecordChars
            : NooseiLimits.MaxToolResultChars;

        return new NooseiToolResult(
            NooseiLimits.Clip(dossier.Value.Text, budget),
            [new LlmContextRef(type, id, dossier.Value.Title)]);
    }
}

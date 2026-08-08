using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <summary>Serializes a saved graph view (node positions + workbench state) into one JSON blob.</summary>
public static class GraphViewPayload
{
    private const int Version = 2;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Wraps raw positions JSON plus workbench state into the stored envelope.</summary>
    public static string Wrap(string? positionsJson, GraphViewState view)
    {
        JsonNode? positions = null;
        try
        {
            positions = string.IsNullOrWhiteSpace(positionsJson) ? null : JsonNode.Parse(positionsJson);
        }
        catch (JsonException) { /* ignore */ }

        var root = new JsonObject
        {
            ["v"] = Version,
            ["positions"] = positions ?? new JsonObject(),
            ["view"] = JsonSerializer.SerializeToNode(view, Options),
        };
        return root.ToJsonString();
    }

    /// <summary>Splits a stored blob back apart; views saved before the envelope existed are plain position maps.</summary>
    public static (string PositionsJson, GraphViewState? View) Unwrap(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson))
        {
            return ("{}", null);
        }
        try
        {
            if (JsonNode.Parse(layoutJson) is not JsonObject root)
            {
                return ("{}", null);
            }
            if (root["positions"] is not { } positions)
            {
                return (layoutJson, null); // legacy: the whole blob is the position map
            }
            var view = root["view"]?.Deserialize<GraphViewState>(Options);
            return (positions.ToJsonString(), view?.Sanitized());
        }
        catch (JsonException)
        {
            return ("{}", null);
        }
    }
}

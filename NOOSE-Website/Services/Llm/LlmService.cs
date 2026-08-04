using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>Thin OpenAI-compatible chat client (OpenRouter). All calls are gated by <see cref="Permission.RequireLlmUse"/>.</summary>
public interface ILlmService
{
    bool IsConfigured { get; }
    string Model { get; }
    Task<string> ChatAsync(string systemPrompt, string userPrompt, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ILlmService" />
public class LlmService(IHttpClientFactory httpFactory, IOptions<LlmOptions> options) : ILlmService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LlmOptions _o = options.Value;

    public bool IsConfigured => _o.IsConfigured;
    public string Model => _o.Model;

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLlmUse(actor);
        if (!_o.IsConfigured)
        {
            throw new InvalidOperationException("Der KI-Assistent ist nicht konfiguriert.");
        }

        var payload = new
        {
            model = _o.Model,
            temperature = 0.3,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        };

        var client = httpFactory.CreateClient("llm");
        using var response = await client.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = ExtractError(body);
            throw new InvalidOperationException(
                $"KI-Endpunkt antwortete mit {(int)response.StatusCode}{(detail is null ? "" : ": " + detail)}.");
        }

        var doc = await response.Content.ReadFromJsonAsync<ChatResponse>(Json, cancellationToken);
        var content = doc?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        return string.IsNullOrWhiteSpace(content) ? "(Keine Antwort erhalten.)" : content;
    }

    /// <summary>Pull a human-readable message out of an OpenAI-style error body; falls back to the raw body.</summary>
    private static string? ExtractError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var msg))
                {
                    return Trim(msg.GetString());
                }
                return Trim(err.ToString());
            }
        }
        catch (JsonException) { /* not json — fall through */ }
        return Trim(body);
    }

    private static string? Trim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        text = text.Trim();
        return text.Length > 300 ? text[..300] : text;
    }

    private sealed record ChatResponse(List<Choice>? Choices);
    private sealed record Choice(Message? Message);
    private sealed record Message(string? Content);
}

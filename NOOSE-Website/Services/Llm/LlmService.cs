using System.Diagnostics;
using System.Net;
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
public class LlmService(IHttpClientFactory httpFactory, IOptions<LlmOptions> options, ILogger<LlmService> logger) : ILlmService
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

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _o.Model,
            ["temperature"] = 0.3,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        };
        if (Routing() is { } routing)
        {
            payload["provider"] = routing;
        }

        var client = httpFactory.CreateClient("llm");
        var attempts = Math.Max(0, _o.Retries) + 1;
        var watch = Stopwatch.StartNew();

        for (var attempt = 1; ; attempt++)
        {
            var last = attempt == attempts;
            try
            {
                // Own budget per attempt: a provider that never answers is dropped instead of burning the whole request.
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _o.AttemptTimeoutSeconds)));

                using var response = await client.PostAsJsonAsync("chat/completions", payload, attemptCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(attemptCts.Token);
                    var detail = ExtractError(body);
                    var status = (int)response.StatusCode;
                    if (!last && IsTransient(response.StatusCode))
                    {
                        logger.LogWarning("KI-Aufruf {Attempt}/{Attempts} verworfen: HTTP {Status} {Detail}",
                            attempt, attempts, status, detail);
                        await DelayAsync(cancellationToken);
                        continue;
                    }
                    logger.LogError("KI-Aufruf endgültig fehlgeschlagen: HTTP {Status} {Detail} (Modell {Model}, {Elapsed} ms)",
                        status, detail, _o.Model, watch.ElapsedMilliseconds);
                    throw new InvalidOperationException(
                        $"KI-Endpunkt antwortete mit {status}{(detail is null ? "" : ": " + detail)}.");
                }

                var doc = await response.Content.ReadFromJsonAsync<ChatResponse>(Json, attemptCts.Token);
                logger.LogInformation("KI-Antwort ok: Modell {Model}, Provider {Provider}, Versuch {Attempt}/{Attempts}, {Elapsed} ms",
                    _o.Model, doc?.Provider ?? "?", attempt, attempts, watch.ElapsedMilliseconds);
                var content = doc?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
                return string.IsNullOrWhiteSpace(content) ? "(Keine Antwort erhalten.)" : content;
            }
            // caller cancelled (circuit gone, navigation) — never retry that
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // attempt budget or HttpClient ceiling elapsed
            catch (OperationCanceledException)
            {
                if (!last)
                {
                    logger.LogWarning("KI-Aufruf {Attempt}/{Attempts} nach {Seconds} s ohne Antwort abgebrochen",
                        attempt, attempts, _o.AttemptTimeoutSeconds);
                    await DelayAsync(cancellationToken);
                    continue;
                }
                logger.LogError("KI-Endpunkt hat in {Attempts} Versuchen nicht geantwortet (Modell {Model}, {Elapsed} ms)",
                    attempts, _o.Model, watch.ElapsedMilliseconds);
                throw new InvalidOperationException(
                    $"Der KI-Endpunkt hat nicht rechtzeitig geantwortet ({attempts} Versuche à {_o.AttemptTimeoutSeconds} s). Bitte später erneut versuchen.");
            }
            catch (HttpRequestException ex)
            {
                if (!last)
                {
                    logger.LogWarning(ex, "KI-Aufruf {Attempt}/{Attempts} auf Transportebene fehlgeschlagen", attempt, attempts);
                    await DelayAsync(cancellationToken);
                    continue;
                }
                logger.LogError(ex, "KI-Endpunkt nicht erreichbar (Modell {Model})", _o.Model);
                throw new InvalidOperationException($"KI-Endpunkt nicht erreichbar: {ex.Message}");
            }
        }
    }

    private Task DelayAsync(CancellationToken cancellationToken)
        => _o.RetryDelayMs <= 0 ? Task.CompletedTask : Task.Delay(_o.RetryDelayMs, cancellationToken);

    /// <summary>Provider routing sent with every request; null when nothing is constrained.</summary>
    private object? Routing()
    {
        var routing = new Dictionary<string, object?>();
        if (_o.Providers.Count > 0)
        {
            routing["order"] = _o.Providers;
            routing["allow_fallbacks"] = _o.AllowProviderFallbacks;
        }
        else if (!string.IsNullOrWhiteSpace(_o.ProviderSort))
        {
            routing["sort"] = _o.ProviderSort;
        }
        if (_o.IgnoreProviders.Count > 0)
        {
            routing["ignore"] = _o.IgnoreProviders;
        }
        return routing.Count == 0 ? null : routing;
    }

    /// <summary>Upstream failures worth another attempt. 404 belongs here: OpenRouter answers it when no provider can currently serve the request, not only for a wrong model.</summary>
    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.NotFound
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

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

    private sealed record ChatResponse(List<Choice>? Choices, string? Provider);
    private sealed record Choice(Message? Message);
    private sealed record Message(string? Content);
}

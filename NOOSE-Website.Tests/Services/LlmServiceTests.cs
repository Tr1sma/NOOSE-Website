using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services;

/// <summary>Tests the LLM boundary: provider routing in the payload, retries on transient upstream failures, and no retry on permanent ones.</summary>
public class LlmServiceTests
{
    private static ClaimsPrincipal Agent()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static LlmOptions Options(Action<LlmOptions>? configure = null)
    {
        var o = new LlmOptions
        {
            Enabled = true,
            BaseUrl = "https://openrouter.test/api/v1",
            ApiKey = "test-key",
            Model = "vendor/model",
            Retries = 2,
            RetryDelayMs = 0,
            AttemptTimeoutSeconds = 5,
        };
        configure?.Invoke(o);
        return o;
    }

    private static (LlmService Svc, StubHandler Handler) Build(LlmOptions options, params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var client = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("llm").Returns(client);
        return (new LlmService(factory, new OptionsWrapper<LlmOptions>(options), NullLogger<LlmService>.Instance), handler);
    }

    private static HttpResponseMessage Ok(string content = "Kurzbrief", string provider = "Baidu")
        => Json(HttpStatusCode.OK, $"{{\"provider\":\"{provider}\",\"choices\":[{{\"message\":{{\"content\":\"{content}\"}}}}]}}");

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // ---------------------------------------------------------------- provider routing

    [Fact]
    public async Task ChatAsync_SortsProvidersByLatency_WhenNoExplicitOrder()
    {
        var (svc, handler) = Build(Options(o => o.ProviderSort = "latency"), Ok());

        await svc.ChatAsync("sys", "user", Agent());

        var provider = handler.LastBody!.RootElement.GetProperty("provider");
        Assert.Equal("latency", provider.GetProperty("sort").GetString());
    }

    [Fact]
    public async Task ChatAsync_SendsProviderOrderAndFallbackFlag_WhenConfigured()
    {
        var (svc, handler) = Build(Options(o =>
        {
            o.Providers = new List<string> { "Baidu", "Cloudflare" };
            o.AllowProviderFallbacks = true;
        }), Ok());

        await svc.ChatAsync("sys", "user", Agent());

        var provider = handler.LastBody!.RootElement.GetProperty("provider");
        Assert.Equal(new[] { "Baidu", "Cloudflare" },
            provider.GetProperty("order").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.True(provider.GetProperty("allow_fallbacks").GetBoolean());
    }

    [Fact]
    public async Task ChatAsync_ExcludesIncompatibleProviders_WhenIgnoreListConfigured()
    {
        // Morph rejects the system+user shape with HTTP 400 "Multi-turn conversations are not supported"
        var (svc, handler) = Build(Options(o => o.IgnoreProviders = new List<string> { "Morph" }), Ok());

        await svc.ChatAsync("sys", "user", Agent());

        var ignore = handler.LastBody!.RootElement.GetProperty("provider").GetProperty("ignore");
        Assert.Equal(new[] { "Morph" }, ignore.EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    // ---------------------------------------------------------------- transient failures

    [Fact]
    public async Task ChatAsync_RetriesOn429_AndSucceeds()
    {
        var (svc, handler) = Build(Options(),
            Json(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"Provider returned error\"}}"),
            Ok("Zweiter Versuch"));

        var answer = await svc.ChatAsync("sys", "user", Agent());

        Assert.Equal("Zweiter Versuch", answer);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ChatAsync_RetriesOn404_BecauseOpenRouterUsesItForNoAvailableProvider()
    {
        var (svc, handler) = Build(Options(),
            Json(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"No endpoints found that support your request\"}}"),
            Ok());

        var answer = await svc.ChatAsync("sys", "user", Agent());

        Assert.Equal("Kurzbrief", answer);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ChatAsync_RetriesOnServerError_ThenGivesUpWithUpstreamDetail()
    {
        var (svc, handler) = Build(Options(o => o.Retries = 1),
            Json(HttpStatusCode.BadGateway, "{\"error\":{\"message\":\"upstream kaputt\"}}"),
            Json(HttpStatusCode.BadGateway, "{\"error\":{\"message\":\"upstream kaputt\"}}"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ChatAsync("sys", "user", Agent()));

        Assert.Contains("502", ex.Message);
        Assert.Contains("upstream kaputt", ex.Message);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ChatAsync_RetriesAfterAttemptTimeout_AndSucceeds()
    {
        var (svc, handler) = Build(Options(o => o.AttemptTimeoutSeconds = 1),
            StubHandler.Hang, Ok("nach Timeout"));

        var answer = await svc.ChatAsync("sys", "user", Agent());

        Assert.Equal("nach Timeout", answer);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ChatAsync_ThrowsFriendlyMessage_WhenEveryAttemptTimesOut()
    {
        var (svc, _) = Build(Options(o => { o.AttemptTimeoutSeconds = 1; o.Retries = 1; }),
            StubHandler.Hang, StubHandler.Hang);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ChatAsync("sys", "user", Agent()));

        Assert.Contains("nicht rechtzeitig", ex.Message);
        // no raw HttpClient wording leaking into the UI
        Assert.DoesNotContain("HttpClient", ex.Message);
    }

    // ---------------------------------------------------------------- permanent failures

    [Fact]
    public async Task ChatAsync_DoesNotRetryOnBadRequest()
    {
        var (svc, handler) = Build(Options(),
            Json(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Multi-turn conversations are not supported\"}}"),
            Ok());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ChatAsync("sys", "user", Agent()));

        Assert.Contains("400", ex.Message);
        Assert.Contains("Multi-turn", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ChatAsync_DoesNotRetry_WhenCallerCancels()
    {
        var (svc, handler) = Build(Options(), StubHandler.Hang);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.ChatAsync("sys", "user", Agent(), cts.Token));

        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Replays queued responses in order; <see cref="Hang"/> blocks until the attempt's token fires.</summary>
    private sealed class StubHandler(params HttpResponseMessage?[] responses) : HttpMessageHandler
    {
        /// <summary>Marker for "server never answers".</summary>
        internal static readonly HttpResponseMessage? Hang = null;

        private int _index;

        public int Calls { get; private set; }

        public JsonDocument? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));

            var response = _index < responses.Length ? responses[_index] : Ok();
            _index++;
            if (response is null)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return response!;
        }
    }
}

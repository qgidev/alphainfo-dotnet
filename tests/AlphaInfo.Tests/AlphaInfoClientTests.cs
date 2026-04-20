using System.Net;
using System.Net.Http;
using AlphaInfo;
using RichardSzalay.MockHttp;
using Xunit;

namespace AlphaInfo.Tests;

public class AlphaInfoClientTests
{
    private const string BaseUrl = "http://localhost:9999";

    private (AlphaInfoClient client, MockHttpMessageHandler http) NewClient()
    {
        var mock = new MockHttpMessageHandler();
        var http = new HttpClient(mock) { BaseAddress = new Uri(BaseUrl) };
        var client = new AlphaInfoClient("ai_test", BaseUrl, http);
        return (client, mock);
    }

    private static List<double> Zeros(int n) => Enumerable.Repeat(0.0, n).ToList();

    [Fact]
    public void Constants_match_server()
    {
        Assert.Equal(192, AlphaInfoConstants.MinFingerprintSamples);
        Assert.Equal(50, AlphaInfoConstants.MinFingerprintSamplesWithBaseline);
    }

    [Fact]
    public void Empty_api_key_throws_validation()
    {
        var ex = Assert.Throws<ValidationException>(() => new AlphaInfoClient(""));
        Assert.Contains("alphainfo.io/register", ex.Message);
    }

    [Fact]
    public async Task Fingerprint_complete_populates_vector()
    {
        var (client, http) = NewClient();
        http.When(HttpMethod.Post, BaseUrl + "/v1/analyze/stream")
            .Respond("application/json", """
                {
                    "analysis_id": "abc",
                    "structural_score": 0.9,
                    "change_detected": false,
                    "change_score": 0.1,
                    "confidence_band": "stable",
                    "engine_version": "t",
                    "metrics": {
                        "sim_local": 0.9, "sim_spectral": 0.85,
                        "sim_fractal": 0.8, "sim_transition": 0.91, "sim_trend": 0.88,
                        "fingerprint_available": true, "fingerprint_reason": null
                    }
                }
                """);
        using (client)
        {
            var fp = await client.FingerprintAsync(new AnalyzeRequest
            {
                Signal = Zeros(AlphaInfoConstants.MinFingerprintSamples),
                SamplingRate = 1,
            });
            Assert.True(fp.IsComplete);
            var vec = fp.GetVector();
            Assert.NotNull(vec);
            Assert.Equal(5, vec!.Length);
            Assert.Equal(0.9, vec[0]);
        }
    }

    [Fact]
    public async Task Fingerprint_incomplete_returns_null_vector()
    {
        var (client, http) = NewClient();
        http.When(HttpMethod.Post, BaseUrl + "/v1/analyze/stream")
            .Respond("application/json", """
                {
                    "analysis_id": "abc",
                    "structural_score": 0.5,
                    "change_detected": false,
                    "change_score": 0.5,
                    "confidence_band": "transition",
                    "engine_version": "t",
                    "metrics": {
                        "sim_local": null, "sim_spectral": null,
                        "sim_fractal": null, "sim_transition": null, "sim_trend": null,
                        "fingerprint_available": false, "fingerprint_reason": "signal_too_short"
                    }
                }
                """);
        using (client)
        {
            var fp = await client.FingerprintAsync(new AnalyzeRequest
            {
                Signal = Zeros(20),
                SamplingRate = 1,
            });
            Assert.False(fp.IsComplete);
            Assert.Null(fp.GetVector());
            Assert.Null(fp.SimLocal);
            Assert.Equal("signal_too_short", fp.FingerprintReason);
        }
    }

    [Fact]
    public async Task Auth_401_maps_to_auth_exception()
    {
        var (client, http) = NewClient();
        http.When(HttpMethod.Post, BaseUrl + "/v1/analyze/stream")
            .Respond(HttpStatusCode.Unauthorized, "application/json", "{\"detail\":\"Invalid API key\"}");
        using (client)
        {
            await Assert.ThrowsAsync<AuthException>(() => client.AnalyzeAsync(new AnalyzeRequest
            {
                Signal = Zeros(10), SamplingRate = 1,
            }));
        }
    }

    [Fact]
    public async Task Rate_429_maps_with_retry_after()
    {
        var (client, http) = NewClient();
        http.When(HttpMethod.Post, BaseUrl + "/v1/analyze/stream")
            .Respond(req =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                resp.Headers.Add("Retry-After", "42");
                resp.Content = new StringContent("{\"detail\":\"Rate limit exceeded\"}", System.Text.Encoding.UTF8, "application/json");
                return resp;
            });
        using (client)
        {
            var ex = await Assert.ThrowsAsync<RateLimitException>(() => client.AnalyzeAsync(new AnalyzeRequest
            {
                Signal = Zeros(10), SamplingRate = 1,
            }));
            Assert.Equal(42, ex.RetryAfterSeconds);
        }
    }

    [Fact]
    public async Task Rate_limit_headers_captured()
    {
        var (client, http) = NewClient();
        http.When(HttpMethod.Post, BaseUrl + "/v1/analyze/stream")
            .Respond(req =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Headers.Add("X-RateLimit-Limit", "100");
                resp.Headers.Add("X-RateLimit-Remaining", "73");
                resp.Headers.Add("X-RateLimit-Reset", "1234567890");
                resp.Content = new StringContent("""
                    {"analysis_id":"a","structural_score":0.9,"change_detected":false,
                     "change_score":0.1,"confidence_band":"stable","engine_version":"t"}
                    """, System.Text.Encoding.UTF8, "application/json");
                return resp;
            });
        using (client)
        {
            Assert.Null(client.RateLimitInfo);
            await client.AnalyzeAsync(new AnalyzeRequest { Signal = Zeros(200), SamplingRate = 1 });
            Assert.NotNull(client.RateLimitInfo);
            Assert.Equal(100, client.RateLimitInfo!.Limit);
            Assert.Equal(73, client.RateLimitInfo.Remaining);
        }
    }

    [Fact]
    public async Task Audit_replay_empty_id_fails_locally()
    {
        var (client, _) = NewClient();
        using (client)
        {
            await Assert.ThrowsAsync<ValidationException>(() => client.AuditReplayAsync(""));
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AlphaInfo;

/// <summary>
/// snake_case naming policy. .NET 8 ships
/// <c>JsonNamingPolicy.SnakeCaseLower</c>, but we also target .NET 6,
/// which does not. Implementing our own keeps the package on a single
/// codepath across target frameworks.
/// </summary>
internal sealed class SnakeCaseLowerPolicy : JsonNamingPolicy
{
    public static readonly SnakeCaseLowerPolicy Instance = new();

    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                bool prevIsLower = i > 0 && char.IsLower(name[i - 1]);
                bool nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (i > 0 && (prevIsLower || nextIsLower))
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}

/// <summary>
/// Async client for the alphainfo.io Structural Intelligence API.
///
/// <code>
/// using var client = new AlphaInfoClient("ai_...");
/// var result = await client.AnalyzeAsync(new AnalyzeRequest {
///     Signal = signal, SamplingRate = 250
/// });
/// </code>
///
/// The underlying <see cref="HttpClient"/> is owned by the client instance
/// by default; dispose the <see cref="AlphaInfoClient"/> to release it, or
/// pass your own via the <paramref name="httpClient"/> constructor arg if
/// you want to share a pool across the application.
/// </summary>
public sealed class AlphaInfoClient : IDisposable, IAsyncDisposable
{
    private const string DefaultBaseUrl = "https://www.alphainfo.io";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(150);

    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public RateLimitInfo? RateLimitInfo { get; private set; }

    public AlphaInfoClient(string apiKey, string? baseUrl = null, HttpClient? httpClient = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ValidationException(
                "apiKey is required. Get one at https://alphainfo.io/register (format: 'ai_...')");
        }
        _apiKey = apiKey;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _http = httpClient ?? new HttpClient { Timeout = DefaultTimeout };
        _ownsHttpClient = httpClient is null;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = SnakeCaseLowerPolicy.Instance,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>
    /// Release the owned <see cref="HttpClient"/> (if any). Idempotent;
    /// safe to call from <c>using</c> or <c>finally</c> blocks. When the
    /// caller passed their own <see cref="HttpClient"/> via the
    /// constructor, this is a no-op — ownership stays with the caller.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsHttpClient)
        {
            try { _http.Dispose(); } catch { /* defensive — never throw from Dispose */ }
        }
    }

    /// <summary>
    /// Async-disposable entry point for C# 8.0+ <c>await using</c>. Calls
    /// <see cref="Dispose"/> synchronously (HttpClient has no async
    /// disposal on the .NET target this SDK supports).
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    // ------------------------------------------------------------------
    // Module-level helpers — no API key required
    // ------------------------------------------------------------------

    /// <summary>Fetch <c>/v1/guide</c> without authentication.</summary>
    public static async Task<Dictionary<string, object?>> GuideAsync(
        string? baseUrl = null, CancellationToken ct = default)
    {
        var url = (baseUrl ?? DefaultBaseUrl).TrimEnd('/') + "/v1/guide";
        return await FetchNoAuthAsync<Dictionary<string, object?>>(url, ct);
    }

    /// <summary>Fetch <c>/health</c> without authentication.</summary>
    public static async Task<HealthStatus> HealthAsync(
        string? baseUrl = null, CancellationToken ct = default)
    {
        var url = (baseUrl ?? DefaultBaseUrl).TrimEnd('/') + "/health";
        return await FetchNoAuthAsync<HealthStatus>(url, ct);
    }

    private static async Task<T> FetchNoAuthAsync<T>(string url, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("alphainfo-dotnet/" + AlphaInfoConstants.SdkVersion);
        try
        {
            using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) throw MapError(resp.StatusCode, resp.Headers, body);
            return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new ApiException("empty body", 0);
        }
        catch (HttpRequestException e) { throw new NetworkException("Network error: " + e.Message, e); }
        catch (TaskCanceledException e) { throw new NetworkException("Request timed out: " + url, e); }
    }

    // ------------------------------------------------------------------
    // analyze / fingerprint
    // ------------------------------------------------------------------

    /// <summary>
    /// Run a full structural analysis on a single signal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>req.Domain</c> is optional (defaults to <c>"generic"</c>). Pass
    /// <c>"auto"</c> to have the server infer the calibration from the signal
    /// — then read <see cref="AnalysisResult.DomainApplied"/> and
    /// <see cref="AnalysisResult.DomainInference"/>. Or pass a specific name
    /// (<c>"biomedical"</c>, <c>"finance"</c>, …). Aliases like <c>"fintech"</c>
    /// and <c>"biomed"</c> resolve server-side; real typos receive HTTP 400
    /// with a "Did you mean …?" suggestion.
    /// </para>
    /// </remarks>
    public async Task<AnalysisResult> AnalyzeAsync(AnalyzeRequest req, CancellationToken ct = default)
    {
        var body = BuildAnalyzeBody(req);
        var json = await PostAsync("/v1/analyze/stream", body, ct);
        return JsonSerializer.Deserialize<AnalysisResult>(json, _jsonOptions)
               ?? throw new ApiException("failed to parse analysis", 0);
    }

    /// <summary>
    /// Syntactic sugar for <see cref="AnalyzeAsync"/> with
    /// <c>req.Domain = "auto"</c>. The server picks the best calibration from
    /// cheap signal statistics; inspect
    /// <see cref="AnalysisResult.DomainInference"/> for confidence + reasoning.
    /// </summary>
    public Task<AnalysisResult> AnalyzeAutoAsync(AnalyzeRequest req, CancellationToken ct = default)
    {
        req.Domain = "auto";
        return AnalyzeAsync(req, ct);
    }

    public async Task<FingerprintResult> FingerprintAsync(AnalyzeRequest req, CancellationToken ct = default)
    {
        WarnIfTooShortForFingerprint(req.Signal, req.Baseline);
        var body = BuildAnalyzeBody(req);
        body["include_semantic"] = false;
        body["use_multiscale"] = false;
        var json = await PostAsync("/v1/analyze/stream", body, ct);
        using var doc = JsonDocument.Parse(json);
        return ParseFingerprintResponse(doc.RootElement);
    }

    private static void WarnIfTooShortForFingerprint(List<double>? signal, List<double>? baseline)
    {
        if (signal is null) return;
        var threshold = baseline is { Count: > 0 }
            ? AlphaInfoConstants.MinFingerprintSamplesWithBaseline
            : AlphaInfoConstants.MinFingerprintSamples;
        if (signal.Count >= threshold) return;
        var qualifier = baseline is { Count: > 0 } ? "with baseline" : "without baseline";
        Console.Error.WriteLine(
            $"[alphainfo] Signal has {signal.Count} samples; the 5D fingerprint needs >={threshold} {qualifier}. " +
            $"Response will likely come back with fingerprint_available=false (reason=\"signal_too_short\"). " +
            $"Use AnalyzeAsync for shorter signals.");
    }

    private static FingerprintResult ParseFingerprintResponse(JsonElement root)
    {
        var r = new FingerprintResult
        {
            AnalysisId = root.TryGetProperty("analysis_id", out var aid) ? aid.GetString() ?? "" : "",
            StructuralScore = root.TryGetProperty("structural_score", out var s) ? s.GetDouble() : 0,
            ConfidenceBand = root.TryGetProperty("confidence_band", out var b) ? b.GetString() ?? "" : "",
        };
        if (!root.TryGetProperty("metrics", out var m) || m.ValueKind != JsonValueKind.Object)
        {
            r.FingerprintAvailable = false;
            r.FingerprintReason = "internal_error";
            return r;
        }
        r.SimLocal = OptDouble(m, "sim_local");
        r.SimSpectral = OptDouble(m, "sim_spectral");
        r.SimFractal = OptDouble(m, "sim_fractal");
        r.SimTransition = OptDouble(m, "sim_transition");
        r.SimTrend = OptDouble(m, "sim_trend");

        if (m.TryGetProperty("fingerprint_available", out var fa) && fa.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            r.FingerprintAvailable = fa.GetBoolean();
            r.FingerprintReason = m.TryGetProperty("fingerprint_reason", out var fr) && fr.ValueKind == JsonValueKind.String
                ? fr.GetString()
                : null;
        }
        else
        {
            r.FingerprintAvailable = r.SimLocal.HasValue && r.SimSpectral.HasValue && r.SimFractal.HasValue
                                     && r.SimTransition.HasValue && r.SimTrend.HasValue;
            r.FingerprintReason = r.FingerprintAvailable ? null : "internal_error";
        }
        return r;
    }

    private static double? OptDouble(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el)) return null;
        return el.ValueKind == JsonValueKind.Number ? el.GetDouble() : null;
    }

    // ------------------------------------------------------------------
    // Batch / matrix / vector
    // ------------------------------------------------------------------

    public async Task<BatchResult> AnalyzeBatchAsync(BatchRequest req, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["signals"] = req.Signals,
            ["sampling_rate"] = req.SamplingRate,
            ["domain"] = req.Domain,
        };
        if (req.Baselines is not null) body["baselines"] = req.Baselines;
        if (req.IncludeSemantic.HasValue) body["include_semantic"] = req.IncludeSemantic.Value;
        if (req.UseMultiscale.HasValue) body["use_multiscale"] = req.UseMultiscale.Value;
        var json = await PostAsync("/v1/analyze/batch", body, ct);
        return JsonSerializer.Deserialize<BatchResult>(json, _jsonOptions)
               ?? throw new ApiException("failed to parse batch", 0);
    }

    public async Task<MatrixResult> AnalyzeMatrixAsync(MatrixRequest req, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["signals"] = req.Signals,
            ["sampling_rate"] = req.SamplingRate,
            ["domain"] = req.Domain,
        };
        if (req.UseMultiscale.HasValue) body["use_multiscale"] = req.UseMultiscale.Value;
        var json = await PostAsync("/v1/analyze/matrix", body, ct);
        return JsonSerializer.Deserialize<MatrixResult>(json, _jsonOptions)
               ?? throw new ApiException("failed to parse matrix", 0);
    }

    public async Task<VectorResult> AnalyzeVectorAsync(VectorRequest req, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["channels"] = req.Channels,
            ["sampling_rate"] = req.SamplingRate,
            ["domain"] = req.Domain,
        };
        if (req.Baselines is not null) body["baselines"] = req.Baselines;
        if (req.IncludeSemantic.HasValue) body["include_semantic"] = req.IncludeSemantic.Value;
        if (req.UseMultiscale.HasValue) body["use_multiscale"] = req.UseMultiscale.Value;
        var json = await PostAsync("/v1/analyze/vector", body, ct);
        return JsonSerializer.Deserialize<VectorResult>(json, _jsonOptions)
               ?? throw new ApiException("failed to parse vector", 0);
    }

    // ------------------------------------------------------------------
    // audit / meta
    // ------------------------------------------------------------------

    public async Task<Dictionary<string, object?>> AuditReplayAsync(string analysisId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(analysisId))
            throw new ValidationException("analysisId cannot be empty");
        var json = await GetAsync("/v1/audit/replay/" + Uri.EscapeDataString(analysisId), ct);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, _jsonOptions) ?? new();
    }

    public async Task<object?> AuditListAsync(int limit = 100, CancellationToken ct = default)
    {
        var json = await GetAsync($"/v1/audit/list?limit={limit}", ct);
        return JsonSerializer.Deserialize<object>(json, _jsonOptions);
    }

    public async Task<HealthStatus> HealthAsync(CancellationToken ct = default) =>
        await HealthAsync(_baseUrl, ct);

    public async Task<Dictionary<string, object?>> GuideAsync(CancellationToken ct = default) =>
        await GuideAsync(_baseUrl, ct);

    public async Task<object?> PlansAsync(CancellationToken ct = default)
    {
        var json = await GetAsync("/api/plans", ct);
        return JsonSerializer.Deserialize<object>(json, _jsonOptions);
    }

    public async Task<object?> VersionAsync(CancellationToken ct = default)
    {
        var json = await GetAsync("/v1/version", ct);
        return JsonSerializer.Deserialize<object>(json, _jsonOptions);
    }

    // ------------------------------------------------------------------
    // HTTP plumbing
    // ------------------------------------------------------------------

    private Dictionary<string, object?> BuildAnalyzeBody(AnalyzeRequest req)
    {
        var body = new Dictionary<string, object?>
        {
            ["signal"] = req.Signal,
            ["sampling_rate"] = req.SamplingRate,
            ["domain"] = req.Domain,
        };
        if (req.Baseline is not null) body["baseline"] = req.Baseline;
        if (req.Metadata is not null) body["metadata"] = req.Metadata;
        if (req.IncludeSemantic.HasValue) body["include_semantic"] = req.IncludeSemantic.Value;
        if (req.UseMultiscale.HasValue) body["use_multiscale"] = req.UseMultiscale.Value;
        return body;
    }

    private async Task<string> PostAsync(string path, object body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path)
        {
            Content = JsonContent.Create(body, options: _jsonOptions),
        };
        req.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        req.Headers.UserAgent.ParseAdd("alphainfo-dotnet/" + AlphaInfoConstants.SdkVersion);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await ExecuteAsync(req, ct);
    }

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path);
        req.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        req.Headers.UserAgent.ParseAdd("alphainfo-dotnet/" + AlphaInfoConstants.SdkVersion);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await ExecuteAsync(req, ct);
    }

    private async Task<string> ExecuteAsync(HttpRequestMessage req, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            CaptureRateLimit(resp.Headers);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) throw MapError(resp.StatusCode, resp.Headers, body);
            return body;
        }
        catch (HttpRequestException e) { throw new NetworkException("Network error: " + e.Message, e); }
        catch (TaskCanceledException e) { throw new NetworkException("Request timed out or cancelled: " + req.RequestUri, e); }
    }

    private void CaptureRateLimit(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("X-RateLimit-Limit", out var limitVals)) return;
        if (!int.TryParse(limitVals.FirstOrDefault(), out var limit) || limit <= 0) return;
        int remaining = 0;
        long reset = 0;
        if (headers.TryGetValues("X-RateLimit-Remaining", out var rem))
            int.TryParse(rem.FirstOrDefault(), out remaining);
        if (headers.TryGetValues("X-RateLimit-Reset", out var rs))
            long.TryParse(rs.FirstOrDefault(), out reset);
        RateLimitInfo = new RateLimitInfo { Limit = limit, Remaining = remaining, Reset = reset };
    }

    private static AlphaInfoException MapError(HttpStatusCode status, HttpResponseHeaders headers, string body)
    {
        Dictionary<string, object?>? parsed = null;
        try { parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(body); } catch { /* non-JSON */ }
        var detailMsg = ExtractDetail(parsed);

        return status switch
        {
            HttpStatusCode.Unauthorized => new AuthException(detailMsg, (int)status, parsed),
            HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnprocessableEntity
                => new ValidationException(detailMsg ?? "Validation failed", (int)status, parsed),
            HttpStatusCode.NotFound
                => new NotFoundException(detailMsg ?? "Not found", (int)status, parsed),
            HttpStatusCode.TooManyRequests => BuildRateLimit(headers, detailMsg ?? "Rate limit exceeded", parsed),
            _ when (int)status >= 500
                => new ApiException(detailMsg ?? $"Server error (HTTP {(int)status})", (int)status, parsed),
            _ => new ApiException(detailMsg ?? $"HTTP {(int)status}", (int)status, parsed),
        };
    }

    private static RateLimitException BuildRateLimit(HttpResponseHeaders headers, string msg, Dictionary<string, object?>? parsed)
    {
        int retryAfter = 0;
        if (headers.TryGetValues("Retry-After", out var values))
            int.TryParse(values.FirstOrDefault(), out retryAfter);
        return new RateLimitException(msg, retryAfter, 429, parsed);
    }

    private static string? ExtractDetail(Dictionary<string, object?>? parsed)
    {
        if (parsed is null) return null;
        if (parsed.TryGetValue("detail", out var detail))
        {
            return detail switch
            {
                string s => s,
                JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
                JsonElement je when je.ValueKind == JsonValueKind.Object && je.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String => m.GetString(),
                _ => null,
            };
        }
        return null;
    }
}

using System.Text.Json.Serialization;

namespace AlphaInfo;

public enum ConfidenceBand { Stable, Transition, Unstable, Unknown }

public sealed class SemanticResult
{
    [JsonPropertyName("summary")] public string Summary { get; set; } = "";
    [JsonPropertyName("alert_level")] public string AlertLevel { get; set; } = "normal";
    [JsonPropertyName("recommended_action")] public string? RecommendedAction { get; set; }
    [JsonPropertyName("trend")] public string? Trend { get; set; }
    [JsonPropertyName("severity")] public string? Severity { get; set; }
    [JsonPropertyName("severity_score")] public double? SeverityScore { get; set; }
}

public sealed class AnalysisResult
{
    [JsonPropertyName("structural_score")] public double StructuralScore { get; set; }
    [JsonPropertyName("change_detected")] public bool ChangeDetected { get; set; }
    [JsonPropertyName("change_score")] public double ChangeScore { get; set; }
    [JsonPropertyName("confidence_band")] public string ConfidenceBand { get; set; } = "";
    [JsonPropertyName("engine_version")] public string EngineVersion { get; set; } = "";
    [JsonPropertyName("analysis_id")] public string AnalysisId { get; set; } = "";
    [JsonPropertyName("metrics")] public Dictionary<string, object?>? Metrics { get; set; }
    [JsonPropertyName("provenance")] public Dictionary<string, object?>? Provenance { get; set; }
    [JsonPropertyName("semantic")] public SemanticResult? Semantic { get; set; }
    [JsonPropertyName("warning")] public string? Warning { get; set; }

    /// <summary>
    /// Always populated by server 1.5.12+. The calibration actually applied
    /// (even when the caller omitted the <c>domain</c> field or passed
    /// <c>"auto"</c>).
    /// </summary>
    [JsonPropertyName("domain_applied")] public string? DomainApplied { get; set; }

    /// <summary>
    /// Populated only when the caller passed <c>domain="auto"</c>. Carries
    /// the inferred calibration name, a [0,1] confidence, a fallback flag
    /// and a machine-readable reasoning tag.
    /// </summary>
    [JsonPropertyName("domain_inference")] public DomainInference? DomainInference { get; set; }
}

/// <summary>
/// Inference block returned by <c>/v1/analyze/stream</c> when the caller
/// passed <c>domain="auto"</c>. Null on the <see cref="AnalysisResult"/>
/// for any other <c>domain</c> value.
/// </summary>
public sealed class DomainInference
{
    [JsonPropertyName("inferred")] public string Inferred { get; set; } = "";
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
    [JsonPropertyName("fallback_used")] public bool FallbackUsed { get; set; }
    [JsonPropertyName("reasoning")] public string Reasoning { get; set; } = "";
}

/// <summary>
/// 5-dimensional structural fingerprint. Every <c>Sim*</c> is <c>double?</c>
/// (nullable): <c>null</c> when the engine could not compute that dimension,
/// never 0.0 as a silent fallback.
/// </summary>
public sealed class FingerprintResult
{
    public string AnalysisId { get; set; } = "";
    public double StructuralScore { get; set; }
    public string ConfidenceBand { get; set; } = "";
    public double? SimLocal { get; set; }
    public double? SimSpectral { get; set; }
    public double? SimFractal { get; set; }
    public double? SimTransition { get; set; }
    public double? SimTrend { get; set; }
    public bool FingerprintAvailable { get; set; }
    public string? FingerprintReason { get; set; }

    /// <summary>Convenience alias for <see cref="FingerprintAvailable"/>.</summary>
    public bool IsComplete => FingerprintAvailable;

    /// <summary>
    /// The 5D fingerprint ready for ANN indexing (pgvector, Qdrant, Faiss).
    /// Returns <c>null</c> when the fingerprint is not available — callers
    /// that embed vectors MUST skip on null instead of substituting zeros.
    /// </summary>
    public double[]? GetVector()
    {
        if (!FingerprintAvailable) return null;
        if (SimLocal is null || SimSpectral is null || SimFractal is null
            || SimTransition is null || SimTrend is null) return null;
        return new[] { SimLocal.Value, SimSpectral.Value, SimFractal.Value, SimTransition.Value, SimTrend.Value };
    }
}

public sealed class BatchItemResult
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("structural_score")] public double? StructuralScore { get; set; }
    [JsonPropertyName("change_detected")] public bool? ChangeDetected { get; set; }
    [JsonPropertyName("change_score")] public double? ChangeScore { get; set; }
    [JsonPropertyName("confidence_band")] public string? ConfidenceBand { get; set; }
    [JsonPropertyName("engine_version")] public string? EngineVersion { get; set; }
    [JsonPropertyName("analysis_id")] public string? AnalysisId { get; set; }
    [JsonPropertyName("metrics")] public Dictionary<string, object?>? Metrics { get; set; }
    [JsonPropertyName("semantic")] public SemanticResult? Semantic { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

public sealed class BatchResult
{
    [JsonPropertyName("results")] public List<BatchItemResult> Results { get; set; } = new();
    [JsonPropertyName("analyses_consumed")] public int AnalysesConsumed { get; set; }
    [JsonPropertyName("total_signals")] public int TotalSignals { get; set; }
}

public sealed class ChannelResult
{
    [JsonPropertyName("structural_score")] public double? StructuralScore { get; set; }
    [JsonPropertyName("change_detected")] public bool? ChangeDetected { get; set; }
    [JsonPropertyName("change_score")] public double? ChangeScore { get; set; }
    [JsonPropertyName("confidence_band")] public string? ConfidenceBand { get; set; }
    [JsonPropertyName("engine_version")] public string? EngineVersion { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

public sealed class VectorResult
{
    [JsonPropertyName("structural_score")] public double StructuralScore { get; set; }
    [JsonPropertyName("change_score")] public double ChangeScore { get; set; }
    [JsonPropertyName("change_detected")] public bool ChangeDetected { get; set; }
    [JsonPropertyName("confidence_band")] public string ConfidenceBand { get; set; } = "";
    [JsonPropertyName("analysis_id")] public string AnalysisId { get; set; } = "";
    [JsonPropertyName("engine_version")] public string EngineVersion { get; set; } = "";
    [JsonPropertyName("channels")] public Dictionary<string, ChannelResult> Channels { get; set; } = new();
    [JsonPropertyName("warning")] public string? Warning { get; set; }
}

public sealed class MatrixResult
{
    [JsonPropertyName("matrix")] public List<List<double>> Matrix { get; set; } = new();
    [JsonPropertyName("labels")] public List<string> Labels { get; set; } = new();
    [JsonPropertyName("n_signals")] public int NSignals { get; set; }
    [JsonPropertyName("n_pairs")] public int NPairs { get; set; }
    [JsonPropertyName("analyses_consumed")] public int AnalysesConsumed { get; set; }
}

public sealed class HealthStatus
{
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("uptime_seconds")] public double? UptimeSeconds { get; set; }
    [JsonPropertyName("services")] public Dictionary<string, string>? Services { get; set; }
}

public sealed class RateLimitInfo
{
    public int Limit { get; init; }
    public int Remaining { get; init; }
    public long Reset { get; init; }
}

public sealed class AnalyzeRequest
{
    public List<double> Signal { get; set; } = new();
    public double SamplingRate { get; set; }
    public string Domain { get; set; } = "generic";
    public List<double>? Baseline { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
    public bool? IncludeSemantic { get; set; }
    public bool? UseMultiscale { get; set; }
}

public sealed class BatchRequest
{
    public List<List<double>> Signals { get; set; } = new();
    public double SamplingRate { get; set; }
    public string Domain { get; set; } = "generic";
    public List<List<double>?>? Baselines { get; set; }
    public bool? IncludeSemantic { get; set; }
    public bool? UseMultiscale { get; set; }
}

public sealed class MatrixRequest
{
    public List<List<double>> Signals { get; set; } = new();
    public double SamplingRate { get; set; }
    public string Domain { get; set; } = "generic";
    public bool? UseMultiscale { get; set; }
}

public sealed class VectorRequest
{
    public Dictionary<string, List<double>> Channels { get; set; } = new();
    public double SamplingRate { get; set; }
    public string Domain { get; set; } = "generic";
    public Dictionary<string, List<double>>? Baselines { get; set; }
    public bool? IncludeSemantic { get; set; }
    public bool? UseMultiscale { get; set; }
}

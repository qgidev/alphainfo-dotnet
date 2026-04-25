namespace AlphaInfo;

/// <summary>
/// Public constants for the alphainfo .NET client.
/// Values are kept in sync with <c>signal_requirements.fingerprint_minimum_samples</c>
/// in <c>/v1/guide</c>.
/// </summary>
public static class AlphaInfoConstants
{
    /// <summary>
    /// Minimum signal length for a full 5-dimensional fingerprint when no
    /// baseline is provided. Below this the server returns
    /// <c>fingerprint_available=false</c> with
    /// <c>fingerprint_reason="signal_too_short"</c>.
    /// </summary>
    public const int MinFingerprintSamples = 192;

    /// <summary>
    /// Minimum length with a comparable baseline — the baseline provides
    /// the reference window so the engine can decompose shorter signals.
    /// </summary>
    public const int MinFingerprintSamplesWithBaseline = 50;

    /// <summary>
    /// SDK version string used in the <c>User-Agent</c> header.
    /// </summary>
    public const string SdkVersion = "1.5.25";
}

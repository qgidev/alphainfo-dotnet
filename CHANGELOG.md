# Changelog

## [1.5.10] - 2026-04-20

Initial release — parity with Python SDK 1.5.10.

- `AlphaInfoClient` with `AnalyzeAsync`, `FingerprintAsync`, `AnalyzeBatchAsync`,
  `AnalyzeMatrixAsync`, `AnalyzeVectorAsync`, `AuditReplayAsync`, `AuditListAsync`,
  `HealthAsync`, `PlansAsync`, `GuideAsync`, `VersionAsync`. All methods accept
  `CancellationToken`.
- Static `AlphaInfoClient.GuideAsync()` / `HealthAsync()` — no key.
- `AlphaInfoConstants.MinFingerprintSamples` (192) and
  `MinFingerprintSamplesWithBaseline` (50).
- Honest fingerprint contract — `FingerprintResult.SimLocal` etc. are
  `double?`; `GetVector()` returns `null` when incomplete.
- Typed exceptions (`AuthException`, `RateLimitException` with
  `RetryAfterSeconds`, `ValidationException`, `NotFoundException`,
  `ApiException`, `NetworkException`) all inheriting from
  `AlphaInfoException`.
- Uses `HttpClient` + `System.Text.Json` — no external deps.
- Targets .NET 6+.

# Changelog

## [1.5.12] - 2026-04-20

Added automatic domain inference; `AnalyzeRequest.Domain` now optional
with sensible default.

- New `DomainInference` class (Inferred, Confidence, FallbackUsed,
  Reasoning).
- `AnalysisResult.DomainApplied` — populated by server 1.5.12+.
- `AnalysisResult.DomainInference` — populated only when the caller
  set `req.Domain = "auto"`.
- New `client.AnalyzeAutoAsync(req)` helper — sugar for
  `AnalyzeAsync(req)` with `req.Domain = "auto"`.
- XML doc on `AnalyzeAsync` explains "auto", aliases (`"fintech"`,
  `"biomed"`, …) and the server's "Did you mean …?" typo path.

Backwards-compatible.

## [1.5.11] - 2026-04-20

Connection cleanup improvements.

- `AlphaInfoClient` now implements `IAsyncDisposable` in addition to
  `IDisposable`, enabling the C# 8.0+ `await using` statement.
- `Dispose()` is now fully idempotent and defensive — a second call is
  a no-op and it never rethrows from the owned `HttpClient.Dispose`.
- When the caller injects their own `HttpClient`, the SDK explicitly
  does **not** dispose it (documented + tested).

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

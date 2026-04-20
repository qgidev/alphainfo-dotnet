# AlphaInfo.Client

[![NuGet](https://img.shields.io/nuget/v/AlphaInfo.Client.svg)](https://www.nuget.org/packages/AlphaInfo.Client)
[![.NET 6+](https://img.shields.io/badge/.NET-6.0+-blue.svg)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**.NET client for the [alphainfo.io](https://alphainfo.io) Structural Intelligence API.**

Detect structural regime changes in any time series. One API, no training, no per-domain tuning.

## Install

```bash
dotnet add package AlphaInfo.Client
```

## 30-second try

**Step 1 — [get a free API key](https://alphainfo.io/register)**.

**Step 2**:

```csharp
using AlphaInfo;

var signal = new List<double>();
for (int i = 0; i < 200; i++) signal.Add(Math.Sin(i / 10.0));
for (int i = 0; i < 200; i++) signal.Add(Math.Sin(i / 10.0) * 3);

using var client = new AlphaInfoClient("ai_...");
var result = await client.AnalyzeAsync(new AnalyzeRequest
{
    Signal = signal,
    SamplingRate = 100,
});
Console.WriteLine(result.ConfidenceBand);   // stable | transition | unstable
Console.WriteLine(result.StructuralScore);  // 0 → 1
```

## Structural fingerprint

```csharp
var fp = await client.FingerprintAsync(new AnalyzeRequest { Signal = signal, SamplingRate = 250 });
if (fp.IsComplete)
{
    double[]? vector = fp.GetVector();  // 5D for pgvector / Qdrant / Faiss
}
else
{
    Console.WriteLine($"unavailable: {fp.FingerprintReason}");
}
```

**Minimum signal length:**

| Case | Minimum | Constant |
|---|---|---|
| No baseline | 192 | `AlphaInfoConstants.MinFingerprintSamples` |
| With baseline | 50 | `AlphaInfoConstants.MinFingerprintSamplesWithBaseline` |

Below the threshold, `GetVector()` returns `null` (never fills with zeros) and a warning is written to `Console.Error` at call time.

## Error handling

```csharp
try {
    await client.AnalyzeAsync(req);
} catch (AuthException)            { /* Get key at https://alphainfo.io/register */ }
catch (RateLimitException e)       { await Task.Delay(TimeSpan.FromSeconds(e.RetryAfterSeconds)); }
catch (ValidationException e)      { /* bad input */ }
catch (NotFoundException)          { /* id not found */ }
catch (ApiException)               { /* 5xx */ }
catch (NetworkException)           { /* transport */ }
```

Everything inherits from `AlphaInfoException`.

## Zero-auth exploration

```csharp
var g = await AlphaInfoClient.GuideAsync();
var h = await AlphaInfoClient.HealthAsync();
```

## Cancellation

All async methods accept `CancellationToken`:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await client.AnalyzeAsync(req, cts.Token);
```

## Links

- [Web](https://alphainfo.io)
- [Python SDK](https://pypi.org/project/alphainfo/)
- [JS/TS SDK](https://www.npmjs.com/package/alphainfo)
- [Go SDK](https://pkg.go.dev/github.com/qgidev/alphainfo-go)
- [Java SDK](https://central.sonatype.com/artifact/io.alphainfo/client)

## About

Built by **QGI Quantum Systems LTDA** — São Paulo, Brazil.
Contact: contato@alphainfo.io · api@alphainfo.io

## License

MIT

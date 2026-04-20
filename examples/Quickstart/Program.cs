using AlphaInfo;

var apiKey = Environment.GetEnvironmentVariable("ALPHAINFO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("Set ALPHAINFO_API_KEY first: https://alphainfo.io/register");
    return 1;
}

var signal = new List<double>();
for (int i = 0; i < 200; i++) signal.Add(Math.Sin(i / 10.0));
for (int i = 0; i < 200; i++) signal.Add(Math.Sin(i / 10.0) * 3);

using var client = new AlphaInfoClient(apiKey);
var result = await client.AnalyzeAsync(new AnalyzeRequest
{
    Signal = signal,
    SamplingRate = 100,
});

Console.WriteLine($"structural_score: {result.StructuralScore:F3}");
Console.WriteLine($"confidence_band:  {result.ConfidenceBand}");
Console.WriteLine($"change_detected:  {result.ChangeDetected}");
Console.WriteLine($"analysis_id:      {result.AnalysisId}");
return 0;

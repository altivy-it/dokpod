using System.Diagnostics;
using Dokpod.Licensing.Tests.Vectors;
using Xunit;

namespace Dokpod.Licensing.Tests;

/// <summary>
/// Spec 031 T019: arquivo de 64 KiB (o máximo aceito) verificado em menos de 50 ms. Usa a mediana de várias
/// execuções após aquecimento, para o teste não falhar por oscilação do runner.
/// </summary>
public sealed class LicenseVerifierPerformanceTests : IDisposable
{
    private const int Runs = 21;
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(50);
    private readonly LicenseVectors vectors = new();

    public void Dispose() => vectors.Dispose();

    [Fact]
    public void MaximumSizeDocument_IsVerifiedWithinBudget()
    {
        var valid = vectors.Valid();
        var maximum = vectors.WithPayload(p => p["plan"] = new string('p', 64));
        var padded = new byte[JwsCompactReader.MaxDocumentBytes];
        Array.Fill(padded, (byte)'A');
        maximum.CopyTo(padded, 0);

        Assert.True(Median(valid) < Budget, "licença válida acima de 50 ms");
        Assert.True(Median(padded) < Budget, "arquivo de 64 KiB acima de 50 ms");
        Assert.Equal(LicenseFailure.Malformed, LicenseVerifier.Verify(padded, vectors.TrustStore, LicenseVectors.Instant).Failure);
    }

    private TimeSpan Median(byte[] document)
    {
        for (var warmup = 0; warmup < 5; warmup++) LicenseVerifier.Verify(document, vectors.TrustStore, LicenseVectors.Instant);
        var samples = new TimeSpan[Runs];
        for (var run = 0; run < Runs; run++)
        {
            var started = Stopwatch.GetTimestamp();
            LicenseVerifier.Verify(document, vectors.TrustStore, LicenseVectors.Instant);
            samples[run] = Stopwatch.GetElapsedTime(started);
        }

        Array.Sort(samples);
        return samples[Runs / 2];
    }
}

using System.Threading.RateLimiting;
using Dokpod.ControlPlane.Application.AuditExport;

namespace Dokpod.ControlPlane.Api.AuditExport;

/// <summary>Até 5 envios de teste por minuto por instalação (threat model L8), em memória da réplica.</summary>
public sealed class AuditExportTestLimiter : IAuditExportTestLimiter, IDisposable
{
    private readonly FixedWindowRateLimiter limiter = new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = AuditExportLimits.MaxTestsPerMinute,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    });

    public bool TryAcquire()
    {
        using var lease = limiter.AttemptAcquire();
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}

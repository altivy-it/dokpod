using Dokpod.ControlPlane.Application.ChangeWindows;

namespace Dokpod.ControlPlane.Api.Endpoints;

/// <summary>Estado da janela no formato do contrato (<c>ChangeWindowStatus</c>), nas duas edições.</summary>
public static class ChangeWindowJson
{
    public static object Status(ChangeWindowStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new
        {
            state = status.State switch
            {
                ChangeWindowState.Open => "open",
                ChangeWindowState.Closed => "closed",
                _ => "none",
            },
            closesAt = status.ClosesAt,
            nextOpensAt = status.NextOpensAt,
            timeZone = status.TimeZone,
        };
    }
}

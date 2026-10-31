using Dokpod.Agent.Contracts.V1;

namespace Dokpod.Agent.Application.Engines;

/// <summary>
/// Contagem de CPU do <c>AgentHello</c> e sua origem (spec 031 T036, D-004): o valor do engine é confiável; o do
/// processo do agente é só fallback, porque pode refletir o limite do container, e nunca vira dado comercial.
/// </summary>
public static class CpuCountResolver
{
    public static (uint CpuCount, CpuCountSource Source) Resolve(EngineHostInfo? hostInfo, int processorCount) =>
        hostInfo?.CpuCount is > 0
            ? ((uint)hostInfo.CpuCount.Value, CpuCountSource.Engine)
            : ((uint)Math.Max(processorCount, 0), CpuCountSource.ProcessFallback);
}

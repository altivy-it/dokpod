using Dokpod.Agent.Contracts.V1;
using Dokpod.ControlPlane.Api.Agents;
using Google.Protobuf;
using Xunit;

namespace Dokpod.ControlPlane.Api.Tests.Agents;

/// <summary>Compatibilidade N/N-1 do protocolo do agente (spec 008 T026, D-009).</summary>
public sealed class ProtocolCompatibilityTests
{
    [Fact]
    public void ServerNMinus1_SessionEstablishedWithoutAcceptedCapabilities_DisablesNewPayloads()
    {
        // Servidor N-1 só conhece os campos 1-5 de SessionEstablished.
        var legacy = new MemoryStream();
        var output = new CodedOutputStream(legacy);
        output.WriteTag(1, WireFormat.WireType.LengthDelimited);
        output.WriteString("1.0");
        output.WriteTag(2, WireFormat.WireType.LengthDelimited);
        output.WriteString(Guid.NewGuid().ToString("D"));
        output.WriteTag(3, WireFormat.WireType.Varint);
        output.WriteUInt64(7);
        output.WriteTag(4, WireFormat.WireType.Varint);
        output.WriteUInt32(15);
        output.WriteTag(5, WireFormat.WireType.Varint);
        output.WriteUInt32(1_048_576);
        output.Flush();

        var parsed = SessionEstablished.Parser.ParseFrom(legacy.ToArray());

        Assert.Equal(7UL, parsed.FencingToken);
        Assert.Empty(parsed.AcceptedCapabilities);
        Assert.DoesNotContain(Capability.EngineEvents, parsed.AcceptedCapabilities);
    }

    [Fact]
    public void AgentNMinus1_HelloWithoutDescriptorFields_IsAcceptedWithKnownCapabilitiesOnly()
    {
        var legacy = new AgentHello
        {
            AgentVersion = "0.8.2",
            Engine = EngineKind.Docker,
            EngineVersion = "27.3.1",
            OperatingSystem = Dokpod.Agent.Contracts.V1.OperatingSystem.Linux,
            Architecture = Architecture.Amd64,
            Capabilities = { Capability.Inventory, Capability.ContainerStart, Capability.ContainerStop },
            SupportedProtocolVersions = { "1.0" },
        };

        var roundTrip = AgentHello.Parser.ParseFrom(legacy.ToByteArray());
        var accepted = AgentHostActivity.Accept(roundTrip).ToArray();

        Assert.Equal([Capability.Inventory, Capability.ContainerStart, Capability.ContainerStop], accepted);
        Assert.DoesNotContain(Capability.EngineEvents, accepted);
        Assert.Equal(0U, roundTrip.CpuCount);
        Assert.False(roundTrip.EngineRootless);
    }

    [Fact]
    public void FutureCapabilities_AreIgnoredByAcceptAndPreservedOnTheWire()
    {
        var hello = new AgentHello
        {
            AgentVersion = "1.1.0",
            Capabilities = { Capability.Inventory, (Capability)99, Capability.EngineEvents, Capability.EngineEvents },
        };

        var parsed = AgentHello.Parser.ParseFrom(hello.ToByteArray());
        var accepted = AgentHostActivity.Accept(parsed).ToArray();

        Assert.Contains((Capability)99, parsed.Capabilities);
        Assert.DoesNotContain((Capability)99, accepted);
        Assert.Single(accepted, c => c == Capability.EngineEvents);
    }

    [Fact]
    public void AgentN_UnknownFieldsFromNewerServerAreIgnored()
    {
        var established = new SessionEstablished { SessionId = "s", FencingToken = 3 };
        var bytes = established.ToByteArray().Concat(UnknownField(42, "futuro")).ToArray();

        var parsed = SessionEstablished.Parser.ParseFrom(bytes);

        Assert.Equal(3UL, parsed.FencingToken);
    }

    private static byte[] UnknownField(int number, string value)
    {
        var stream = new MemoryStream();
        var output = new CodedOutputStream(stream);
        output.WriteTag(number, WireFormat.WireType.LengthDelimited);
        output.WriteString(value);
        output.Flush();
        return stream.ToArray();
    }

    [Fact]
    public void Spec012_ServerAcceptsImageCapabilitiesAndAgentNMinus1WithoutThemGetsNone()
    {
        var current = new AgentHello { Capabilities = { Capability.Inventory, Capability.ImageInventory, Capability.ImagePull } };
        var legacy = new AgentHello { Capabilities = { Capability.Inventory, Capability.ContainerLogs } };

        Assert.Contains(Capability.ImagePull, AgentHostActivity.Accept(current));
        Assert.Contains(Capability.ImageInventory, AgentHostActivity.Accept(current));
        Assert.DoesNotContain(Capability.ImagePull, AgentHostActivity.Accept(legacy));
    }

    [Fact]
    public void Spec012_NewFieldsAreIgnoredByOlderParsersAndDefaultWhenAbsent()
    {
        // Heartbeat/ContainerState/SnapshotRequest de versões anteriores: campos novos assumem o padrão (0/vazio = N-1).
        var heartbeat = Heartbeat.Parser.ParseFrom(new Heartbeat { InventoryRevision = 3 }.ToByteArray());
        var request = SnapshotRequest.Parser.ParseFrom(new SnapshotRequest { ReasonCode = "x" }.ToByteArray());
        Assert.Equal(0UL, heartbeat.ImageRevision);
        Assert.Equal(SnapshotScope.Unspecified, request.Scope);

        // Um parser que conhece só os campos 1-6 de AgentCommand preserva os parâmetros como campo desconhecido.
        var command = new AgentCommand { CommandId = Guid.NewGuid().ToString("D"), Kind = CommandKind.PullImage, PullImage = new PullImageParameters { Reference = "nginx" } };
        var legacyView = Google.Protobuf.WellKnownTypes.Empty.Parser.ParseFrom(command.ToByteArray());
        Assert.Equal(command.ToByteArray(), legacyView.ToByteArray());
    }
}

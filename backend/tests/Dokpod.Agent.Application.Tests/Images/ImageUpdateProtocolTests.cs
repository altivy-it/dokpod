using Dokpod.Agent.Contracts.V1;
using Google.Protobuf;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Images;

/// <summary>Spec 025 T003: tags novas e aditivas no protocolo (N/N-1); nenhum número existente muda.</summary>
public sealed class ImageUpdateProtocolTests
{
    [Fact]
    public void Tags_AreTheOnesOfTheContract()
    {
        Assert.Equal(23, (int)Capability.ImageUpdateCheck);
        Assert.Equal(17, ControlPlaneMessage.ImageUpdateCheckFieldNumber);
        Assert.Equal(27, AgentMessage.ImageUpdateCheckResultFieldNumber);
        Assert.Equal(16, ControlPlaneMessage.ExecOpenFieldNumber);
        Assert.Equal(26, AgentMessage.PodSnapshotPageFieldNumber);
        Assert.Equal([0, 1, 2, 3, 4], Enum.GetValues<ImageUpdateState>().Select(state => (int)state));
    }

    [Fact]
    public void RequestAndResult_RoundTrip()
    {
        var request = new ControlPlaneMessage
        {
            ImageUpdateCheck = new ImageUpdateCheckRequest
            {
                RequestId = Guid.NewGuid().ToString("D"),
                Items = { new ImageUpdateCheckItem { Reference = "nginx:1.28", ImageId = "sha256:" + new string('a', 64) } },
            },
        };
        var result = new AgentMessage
        {
            ImageUpdateCheckResult = new ImageUpdateCheckResultPage
            {
                RequestId = request.ImageUpdateCheck.RequestId,
                IsLastPage = true,
                Results = { new ImageUpdateCheckResult { Reference = "nginx:1.28", Platform = "linux/amd64", State = ImageUpdateState.UpdateAvailable } },
            },
        };

        Assert.Equal(request, ControlPlaneMessage.Parser.ParseFrom(request.ToByteArray()));
        Assert.Equal(result, AgentMessage.Parser.ParseFrom(result.ToByteArray()));
    }
}

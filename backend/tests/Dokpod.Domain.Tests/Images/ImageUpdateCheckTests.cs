using Dokpod.Domain.Images;
using Xunit;

namespace Dokpod.Domain.Tests.Images;

/// <summary>Spec 025 T004: estados, motivos, plataforma e o estado derivado do container (D-008).</summary>
public sealed class ImageUpdateCheckTests
{
    private static readonly string Old = "sha256:" + new string('a', 64);
    private static readonly string New = "sha256:" + new string('b', 64);

    [Theory]
    [InlineData("up_to_date", null, true)]
    [InlineData("update_available", null, true)]
    [InlineData("up_to_date", "timeout", false)]
    [InlineData("not_checkable", "digest_reference", true)]
    [InlineData("not_checkable", "timeout", false)]
    [InlineData("not_checkable", null, false)]
    [InlineData("failed", "reference_not_found", true)]
    [InlineData("failed", "local_image", false)]
    [InlineData("container_behind", null, false)]
    [InlineData("desconhecido", null, false)]
    public void Reason_MatchesTheState(string state, string? reason, bool valid)
    {
        Assert.Equal(valid, ImageUpdateReasons.IsValidFor(state, reason));
    }

    [Theory]
    [InlineData("linux/amd64", true)]
    [InlineData("linux/arm64/v8", true)]
    [InlineData("windows/amd64", true)]
    [InlineData("linux", false)]
    [InlineData("Linux/AMD64", false)]
    [InlineData("linux/amd64/v8/extra", false)]
    [InlineData("", false)]
    public void Platform_IsOsArchitectureAndOptionalVariant(string platform, bool valid)
    {
        Assert.Equal(valid, ImagePlatform.IsValid(platform));
        Assert.Equal("linux/arm64/v8", ImagePlatform.Of("linux", "arm64", "v8"));
        Assert.Equal("linux/amd64", ImagePlatform.Of("linux", "amd64"));
    }

    [Fact]
    public void Digest_IsSha256Lowercase()
    {
        Assert.True(ContentDigest.IsValid(Old));
        Assert.False(ContentDigest.IsValid("sha256:" + new string('A', 64)));
        Assert.False(ContentDigest.IsValid("sha512:" + new string('a', 128)));
        Assert.False(ContentDigest.IsValid(null));
    }

    [Fact]
    public void Container_IsBehindOnlyWhenTheReferenceIsCurrentButItsImageIsOther()
    {
        var current = Check(ImageUpdateStates.UpToDate, localImageId: New);

        Assert.Equal(ImageUpdateStates.UpToDate, current.StateForContainer(New));
        Assert.Equal(ImageUpdateStates.ContainerBehind, current.StateForContainer(Old));
        Assert.Equal(ImageUpdateStates.UpdateAvailable, Check(ImageUpdateStates.UpdateAvailable, localImageId: New).StateForContainer(Old));
        Assert.Equal(ImageUpdateStates.Failed, Check(ImageUpdateStates.Failed, localImageId: New).StateForContainer(Old));
        // Sem imagem local registrada, não há como afirmar que o container ficou para trás.
        Assert.Equal(ImageUpdateStates.UpToDate, Check(ImageUpdateStates.UpToDate, localImageId: null).StateForContainer(Old));
    }

    private static ImageUpdateCheck Check(string state, string? localImageId) =>
        new(Guid.NewGuid(), "docker.io/library/nginx:1.28", "linux/amd64", localImageId, state, null, null, DateTimeOffset.UtcNow, null, null);
}

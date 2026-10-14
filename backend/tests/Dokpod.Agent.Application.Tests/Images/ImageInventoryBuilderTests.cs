using Dokpod.Agent.Application.Engines;
using Dokpod.Agent.Application.Images;
using Xunit;

namespace Dokpod.Agent.Application.Tests.Images;

public sealed class ImageInventoryBuilderTests
{
    private static EngineImage Image(int n, int tags = 1) => new(
        $"sha256:{n:D64}", Enumerable.Range(0, tags).Select(t => $"registry.empresa.com/app{n}:{t}").ToArray(), [], 1_000 + n, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Build_PaginatesAt200ImagesAndMarksLastPage()
    {
        var builder = new ImageInventoryBuilder(TimeProvider.System);

        var pages = builder.Build(Enumerable.Range(0, 450).Select(i => Image(i)).ToArray(), force: false);

        Assert.Equal([200, 200, 50], pages.Select(p => p.Images.Count));
        Assert.Equal([false, false, true], pages.Select(p => p.IsLastPage));
        Assert.Single(pages.Select(p => p.SnapshotId).Distinct());
        Assert.All(pages, p => Assert.True(p.CalculateSize() <= ImageInventoryBuilder.MaxPageBytes));
    }

    [Fact]
    public void Build_SkipsUnchangedInventoryAndAdvancesRevisionOnChange()
    {
        var builder = new ImageInventoryBuilder(TimeProvider.System);
        var first = builder.Build([Image(1)], force: false);
        var revision = builder.Revision;

        Assert.NotEmpty(first);
        Assert.Empty(builder.Build([Image(1)], force: false));
        Assert.Equal(revision, builder.Build([Image(1)], force: true).Single().ImageRevision);
        builder.Build([Image(1), Image(2)], force: false);
        Assert.True(builder.Revision > revision);
    }

    [Fact]
    public void Build_TruncatesNamesAbove64AndSplitsLargePages()
    {
        var builder = new ImageInventoryBuilder(TimeProvider.System);

        var pages = builder.Build(Enumerable.Range(0, 150).Select(i => Image(i, tags: 100)).ToArray(), force: false);

        var state = pages[0].Images[0];
        Assert.Equal(64, state.RepoTags.Count);
        Assert.True(state.Truncated);
        Assert.True(pages.Count > 1);
        Assert.All(pages, p => Assert.True(p.CalculateSize() <= ImageInventoryBuilder.MaxPageBytes));
    }

    [Fact]
    public void Build_EmptyInventoryStillSendsOneLastPage()
    {
        var page = new ImageInventoryBuilder(TimeProvider.System).Build([], force: true).Single();
        Assert.True(page.IsLastPage);
        Assert.Empty(page.Images);
    }
}

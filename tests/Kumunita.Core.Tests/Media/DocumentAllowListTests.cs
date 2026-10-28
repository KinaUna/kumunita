using Kumunita.Core.Media;
using Xunit;

namespace Kumunita.Core.Tests.Media;

/// <summary>ADR 0228 — the default document-lane allow-list now includes PowerPoint and
/// the four raster image types (amends ADR 0122 D3); SVG and scripts stay out.</summary>
public class DocumentAllowListTests
{
    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/vnd.ms-powerpoint")]
    [InlineData("application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    public void Default_allows(string contentType)
        => Assert.True(new MediaOptions().IsDocumentAllowed(contentType));

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    [InlineData("application/x-msdownload")]
    public void Default_still_refuses(string contentType)
        => Assert.False(new MediaOptions().IsDocumentAllowed(contentType));

    [Fact]
    public void Config_override_still_wins()
    {
        var o = new MediaOptions { DocumentAllowedContentTypes = "application/pdf" };
        Assert.False(o.IsDocumentAllowed("image/png"));
    }
}

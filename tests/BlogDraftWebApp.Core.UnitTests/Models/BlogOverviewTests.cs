using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.UnitTests.Models;

public sealed class BlogOverviewTests
{
    [Test]
    public void Validate_EmptyContent_ThrowsUnifiedMessage()
    {
        var overview = new BlogOverview(" ");

        Assert.That(
            () => overview.Validate(),
            Throws.ArgumentException.With.Message.Contains(BlogOverview.RequiredErrorMessage));
    }

    [Test]
    public void Validate_ShortContent_ThrowsUnifiedMessage()
    {
        var overview = new BlogOverview(new string('x', BlogOverview.MinimumLength - 1));

        Assert.That(
            () => overview.Validate(),
            Throws.ArgumentException.With.Message.Contains(BlogOverview.MinimumLengthErrorMessage));
    }

    [Test]
    public void Validate_LongContent_ThrowsUnifiedMessage()
    {
        var overview = new BlogOverview(new string('x', BlogOverview.MaximumLength + 1));

        Assert.That(
            () => overview.Validate(),
            Throws.ArgumentException.With.Message.Contains(BlogOverview.MaximumLengthErrorMessage));
    }
}

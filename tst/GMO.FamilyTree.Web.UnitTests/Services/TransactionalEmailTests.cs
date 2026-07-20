using GMO.FamilyTree.Web.Services;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class TransactionalEmailTests
{
    [Fact]
    public void LinkMessage_includes_brand_reason_link_and_ignore_note()
    {
        var (html, text) = TransactionalEmail.LinkMessage(
            "user@example.com",
            "You created a GOOM Family Tree account with this email.",
            "Confirm your email",
            "https://example.com/confirm");

        Assert.Contains("Confirm your email", html, StringComparison.Ordinal);
        Assert.Contains("https://example.com/confirm", html, StringComparison.Ordinal);
        Assert.Contains("user@example.com", html, StringComparison.Ordinal);
        Assert.Contains("If you did not request this", html, StringComparison.Ordinal);

        Assert.Contains("Confirm your email:", text, StringComparison.Ordinal);
        Assert.Contains("https://example.com/confirm", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Subject_prefixes_brand()
    {
        Assert.Equal("GOOM Family Tree: confirm your email", TransactionalEmail.Subject("confirm your email"));
    }
}
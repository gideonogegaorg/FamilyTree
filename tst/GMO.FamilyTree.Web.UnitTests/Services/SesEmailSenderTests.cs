using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class SesEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_calls_ses_with_from_and_destination()
    {
        var ses = new Mock<IAmazonSimpleEmailService>();
        SendEmailRequest? captured = null;
        ses.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-1" });

        var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            FromAddress = "noreply@example.com",
            FromDisplayName = "Family Tree",
            Region = "us-east-1"
        });
        var sut = new SesEmailSender(ses.Object, options, NullLogger<SesEmailSender>.Instance);

        await sut.SendEmailAsync("to@example.com", "Hello", "<p>Hi</p>");

        Assert.NotNull(captured);
        Assert.Contains("noreply@example.com", captured!.Source);
        Assert.Equal("to@example.com", Assert.Single(captured.Destination.ToAddresses));
        Assert.Equal("Hello", captured.Message.Subject.Data);
        Assert.Equal("<p>Hi</p>", captured.Message.Body.Html.Data);
    }

    [Fact]
    public async Task SendEmailAsync_throws_when_from_address_missing()
    {
        var ses = new Mock<IAmazonSimpleEmailService>();
        var sut = new SesEmailSender(
            ses.Object,
            Microsoft.Extensions.Options.Options.Create(new EmailOptions()),
            NullLogger<SesEmailSender>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SendEmailAsync("to@example.com", "S", "<p>x</p>"));
    }
}
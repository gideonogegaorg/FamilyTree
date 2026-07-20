namespace GMO.FamilyTree.Web.Services;

/// <summary>
/// Development email sender that logs the message instead of sending. Use for password reset links in dev.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage, string plainTextMessage)
    {
        _logger.LogInformation(
            "Email (not sent): To={Email}, Subject={Subject}, HtmlLength={HtmlLength}, TextLength={TextLength}",
            email,
            subject,
            htmlMessage.Length,
            plainTextMessage.Length);
        return Task.CompletedTask;
    }
}
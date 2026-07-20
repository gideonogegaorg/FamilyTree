namespace GMO.FamilyTree.Web.Services;

/// <summary>
/// Development email sender that logs the message instead of sending. Use for password reset links in dev.
/// Recipient and subject are Data Protection–encrypted so logs never hold plain mailbox identifiers.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly IEmailLogProtector _logProtector;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IEmailLogProtector logProtector)
    {
        _logger = logger;
        _logProtector = logProtector;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage, string plainTextMessage)
    {
        var toProtected = _logProtector.Protect(email);
        var subjectProtected = _logProtector.Protect(subject);
        _logger.LogInformation(
            "Email (not sent): ToProtected={ToProtected}, SubjectProtected={SubjectProtected}, HtmlLength={HtmlLength}, TextLength={TextLength}",
            toProtected,
            subjectProtected,
            htmlMessage.Length,
            plainTextMessage.Length);
        return Task.CompletedTask;
    }
}
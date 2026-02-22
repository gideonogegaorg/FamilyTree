namespace GMO.Family.Web.Services;

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

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("Email (not sent): To={Email}, Subject={Subject}. Body: {Body}", email, subject, htmlMessage);
        return Task.CompletedTask;
    }
}

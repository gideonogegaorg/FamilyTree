namespace GMO.Family.Web.Services;

/// <summary>
/// Sends email (e.g. for password reset). For development, use an implementation that logs to console/file.
/// For production, implement with SMTP or a provider (e.g. SendGrid).
/// </summary>
public interface IEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);
}

using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

using GMO.FamilyTree.Web.Options;

using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Services;

public sealed class SesEmailSender : IEmailSender
{
    private readonly IAmazonSimpleEmailService _ses;
    private readonly EmailOptions _options;
    private readonly ILogger<SesEmailSender> _logger;

    public SesEmailSender(
        IAmazonSimpleEmailService ses,
        IOptions<EmailOptions> options,
        ILogger<SesEmailSender> logger)
    {
        _ses = ses;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage, string plainTextMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.FromAddress))
            throw new InvalidOperationException("Email:FromAddress is required when using SES.");

        if (string.IsNullOrWhiteSpace(plainTextMessage))
            throw new ArgumentException("Plain text body is required.", nameof(plainTextMessage));

        var from = string.IsNullOrWhiteSpace(_options.FromDisplayName)
            ? _options.FromAddress
            : $"{_options.FromDisplayName} <{_options.FromAddress}>";

        var request = new SendEmailRequest
        {
            Source = from,
            Destination = new Destination { ToAddresses = [email] },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body
                {
                    Html = new Content(htmlMessage),
                    Text = new Content(plainTextMessage)
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(_options.ReplyToAddress))
            request.ReplyToAddresses = [_options.ReplyToAddress.Trim()];

        var response = await _ses.SendEmailAsync(request);
        _logger.LogInformation("SES email sent, MessageId={MessageId}", response.MessageId);
    }
}
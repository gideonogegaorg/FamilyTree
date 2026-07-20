using System.Net;
using System.Text.Encodings.Web;

namespace GMO.FamilyTree.Web.Services;

/// <summary>Shared subject/body helpers for transactional mail (HTML + plain text).</summary>
public static class TransactionalEmail
{
    public const string Brand = "GOOM Family Tree";

    public static string Subject(string topic) => $"{Brand}: {topic}";

    public static (string Html, string Text) LinkMessage(
        string recipientEmail,
        string openingSentence,
        string linkLabel,
        string url,
        string closingSentence = "If you did not request this, you can ignore this message.")
    {
        var encOpening = WebUtility.HtmlEncode(openingSentence);
        var encLabel = WebUtility.HtmlEncode(linkLabel);
        var encRecipient = WebUtility.HtmlEncode(recipientEmail);
        var encClosing = WebUtility.HtmlEncode(closingSentence);
        var encHref = HtmlEncoder.Default.Encode(url);

        var html = $"""
            <p>{encOpening}</p>
            <p><a href="{encHref}">{encLabel}</a></p>
            <p>This message was sent to {encRecipient}.</p>
            <p>{encClosing}</p>
            """;

        var text = $"""
            {openingSentence}

            {linkLabel}:
            {url}

            This message was sent to {recipientEmail}.

            {closingSentence}
            """;

        return (html, text);
    }

    public static (string Html, string Text) CodeMessage(
        string recipientEmail,
        string openingSentence,
        string code,
        string closingSentence = "If you did not try to sign in, you can ignore this message.")
    {
        var encOpening = WebUtility.HtmlEncode(openingSentence);
        var encCode = WebUtility.HtmlEncode(code);
        var encRecipient = WebUtility.HtmlEncode(recipientEmail);
        var encClosing = WebUtility.HtmlEncode(closingSentence);

        var html = $"""
            <p>{encOpening}</p>
            <p>Your sign-in code is: <strong>{encCode}</strong>. It expires shortly.</p>
            <p>This message was sent to {encRecipient}.</p>
            <p>{encClosing}</p>
            """;

        var text = $"""
            {openingSentence}

            Your sign-in code is: {code}
            It expires shortly.

            This message was sent to {recipientEmail}.

            {closingSentence}
            """;

        return (html, text);
    }

    public static (string Html, string Text) InviteMessage(
        string recipientEmail,
        string inviterLabel,
        string roleLabel,
        string treeName,
        string acceptUrl)
    {
        var opening =
            $"{inviterLabel} invited you to {roleLabel} the family tree \"{treeName}\" on {Brand}.";
        var encOpeningLead = WebUtility.HtmlEncode($"{inviterLabel} invited you to ");
        var encRole = WebUtility.HtmlEncode(roleLabel);
        var encTree = WebUtility.HtmlEncode(treeName);
        var encBrand = WebUtility.HtmlEncode(Brand);
        var encRecipient = WebUtility.HtmlEncode(recipientEmail);
        var encHref = HtmlEncoder.Default.Encode(acceptUrl);

        var html = $"""
            <p>{encOpeningLead}<strong>{encRole}</strong> the family tree <strong>{encTree}</strong> on {encBrand}.</p>
            <p><a href="{encHref}">Accept the invite</a></p>
            <p>You'll need to sign in or create an account. This link was sent to {encRecipient}.</p>
            """;

        var text = $"""
            {opening}

            Accept the invite:
            {acceptUrl}

            You'll need to sign in or create an account. This link was sent to {recipientEmail}.
            """;

        return (html, text);
    }
}
namespace GMO.FamilyTree.Web.Extensions;

public static class HttpContextClientIpExtensions
{
    public const string UnknownClientIp = "unknown";

    /// <summary>Client IP for rate limiting; uses a shared bucket when the address is unavailable.</summary>
    public static string GetClientIpForRateLimit(this HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? UnknownClientIp;
}

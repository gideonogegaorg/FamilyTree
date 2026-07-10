using System.Net;

using GMO.FamilyTree.Web.Extensions;

using Microsoft.AspNetCore.Http;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Extensions;

public class HttpContextClientIpExtensionsTests
{
    [Fact]
    public void GetClientIpForRateLimit_returns_remote_address_when_present()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        Assert.Equal("203.0.113.10", context.GetClientIpForRateLimit());
    }

    [Fact]
    public void GetClientIpForRateLimit_returns_unknown_when_missing()
    {
        var context = new DefaultHttpContext();
        Assert.Equal(HttpContextClientIpExtensions.UnknownClientIp, context.GetClientIpForRateLimit());
    }
}
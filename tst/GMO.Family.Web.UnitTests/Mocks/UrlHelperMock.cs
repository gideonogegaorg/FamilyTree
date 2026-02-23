using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

using Moq;

namespace GMO.Family.Web.UnitTests.Mocks;

/// <summary>
/// Mock of <see cref="IUrlHelper"/> with defaults for controller redirect tests.
/// Override in tests only when you need different URLs (e.g. specific return path).
/// </summary>
public class UrlHelperMock : Mock<IUrlHelper>
{
    public const string DefaultLoginPath = "/Account/Login";
    public const string DefaultContentRoot = "/";

    public UrlHelperMock(string? contentReturn = null)
        : base(MockBehavior.Loose)
    {
        Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns(DefaultLoginPath);
        Setup(u => u.Content(It.IsAny<string>())).Returns((string? s) => s ?? contentReturn ?? DefaultContentRoot);
    }

    /// <summary>Use this when the test expects a specific return URL (e.g. LocalRedirect("/home")).</summary>
    public static UrlHelperMock WithContentReturn(string contentReturn) => new(contentReturn);
}
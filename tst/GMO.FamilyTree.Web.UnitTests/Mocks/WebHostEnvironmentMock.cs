using Microsoft.AspNetCore.Hosting;

using Moq;

namespace GMO.FamilyTree.Web.UnitTests.Mocks;

/// <summary>
/// Mock of <see cref="IWebHostEnvironment"/> with safe defaults for controller tests.
/// Override only when the test depends on a specific path or environment name.
/// </summary>
public class WebHostEnvironmentMock : Mock<IWebHostEnvironment>
{
    public WebHostEnvironmentMock()
        : base(MockBehavior.Loose)
    {
        Setup(e => e.ContentRootPath).Returns("/app");
        Setup(e => e.WebRootPath).Returns("/app/wwwroot");
        Setup(e => e.EnvironmentName).Returns("Testing");
    }
}
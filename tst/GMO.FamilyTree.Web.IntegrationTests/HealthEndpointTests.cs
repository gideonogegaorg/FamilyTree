using System.Net;

using Xunit;

namespace GMO.FamilyTree.Web.IntegrationTests;

public class HealthEndpointTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;

    public HealthEndpointTests(WebAppFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Health_returns_200_without_authentication()
    {
        using var client = _fixture.CreateClient(signIn: false);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
using System.Net.Http;
using System.Threading.Tasks;

using Xunit;

namespace GMO.Family.Web.IntegrationTests;

public class LayoutUnauthenticatedTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;

    public LayoutUnauthenticatedTests(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Unauthenticated_request_shows_sign_in_link_in_menu()
    {
        // Arrange: page that uses _Layout (and thus UserMenu) when not authenticated
        var client = _fixture.CreateClient(signIn: false);

        // Act: GET any page with main layout to exercise UserMenu unauthenticated path
        var response = await client.GetAsync("/Account/Login");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sign in", html);
    }
}
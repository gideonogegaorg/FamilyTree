using System.Net;

using Xunit;

namespace GMO.FamilyTree.Web.IntegrationTests;

public class LandingPageTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;

    public LandingPageTests(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Landing_returns_200_for_anonymous_user_with_sign_up()
    {
        var client = _fixture.CreateClient(signIn: false);

        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("ft-landing-hero", html);
        Assert.Contains("Sign up", html);
        Assert.Contains("data-demo=\"true\"", html);
        Assert.Contains("James", html);
        Assert.DoesNotContain("data-nodes=\"[]\"", html);
    }

    [Fact]
    public async Task Landing_redirects_authenticated_user_to_Home_Index()
    {
        var client = _fixture.CreateClient(signIn: true);

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location.ToString();
        Assert.True(
            location.Contains("/Home/Index", StringComparison.OrdinalIgnoreCase) || location == "/",
            $"Expected redirect to home, got {location}");
    }
}
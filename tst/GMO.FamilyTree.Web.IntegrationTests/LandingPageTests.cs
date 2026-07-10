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
            location.Contains("/Home/Index", StringComparison.OrdinalIgnoreCase),
            $"Expected redirect to home, got {location}");
    }

    [Fact]
    public async Task Landing_includes_auth_links_with_return_url_to_home_index()
    {
        var client = _fixture.CreateClient(signIn: false);

        var html = await client.GetStringAsync("/");

        Assert.Contains("returnUrl=%2FHome%2FIndex", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ft-landing-section", html);
        Assert.Contains("ft-site-footer", html);
    }

    [Fact]
    public async Task Landing_does_not_render_authenticated_tree_toolbar()
    {
        var client = _fixture.CreateClient(signIn: false);

        var html = await client.GetStringAsync("/");

        Assert.DoesNotContain("ft-tree-picker-btn", html);
        Assert.DoesNotContain("ft-subbar", html);
    }

    [Fact]
    public async Task Login_page_shows_back_link_to_landing()
    {
        var client = _fixture.CreateClient(signIn: false);

        var html = await client.GetStringAsync("/Account/Login");

        Assert.Contains("ft-auth-back", html);
        Assert.Contains("href=\"/\"", html);
    }

    [Fact]
    public async Task Register_page_shows_back_link_to_landing()
    {
        var client = _fixture.CreateClient(signIn: false);

        var html = await client.GetStringAsync("/Account/Register");

        Assert.Contains("ft-auth-back", html);
        Assert.Contains("href=\"/\"", html);
    }

    [Fact]
    public async Task Home_Index_requires_authentication()
    {
        var client = _fixture.CreateClient(signIn: false);

        var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/Account/Login", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FamilyTree_short_url_maps_to_index_for_authenticated_user()
    {
        var client = _fixture.CreateClient(signIn: true);

        var response = await client.GetAsync("/FamilyTree");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Family trees", html);
    }
}
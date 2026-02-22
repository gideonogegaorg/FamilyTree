using System.Net;

using GMO.Family.Web.Data;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.Family.Web.IntegrationTests;

public class AccountControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;

    public AccountControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Login_GET_returns_200_and_login_form()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);

        // Act
        var response = await client.GetAsync("/Account/Login");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Login", html);
    }

    [Fact]
    public async Task Register_GET_returns_200()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);

        // Act
        var response = await client.GetAsync("/Account/Register");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Register_POST_creates_user_default_tree_and_profile_and_redirects()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);
        var email = "register-" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
        var password = "TestPassword1!";
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Register");
        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password,
            ["__RequestVerificationToken"] = token
        };

        // Act
        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using (var scope = _fixture.CreateScope())
        {
            var db = _fixture.GetDbContext(scope);
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
            Assert.NotNull(user);
            var userId = user.Id;
            var tree = await db.FamilyTrees.SingleOrDefaultAsync(t => t.OwnerId == userId && t.Name == "Default");
            Assert.NotNull(tree);
            var profile = await db.UserProfiles.FindAsync(userId);
            Assert.NotNull(profile);
            Assert.Equal(tree.Id, profile.CurrentFamilyTreeId);
        }
    }

    [Fact]
    public async Task Login_POST_with_valid_credentials_redirects()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);
        var email = "login-" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
        var password = "TestPassword1!";
        var regToken = await GetAntiforgeryTokenAsync(client, "/Account/Register");
        await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password,
            ["__RequestVerificationToken"] = regToken
        }));
        client = _fixture.CreateClient(signIn: false);
        var loginToken = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = loginToken
        };

        // Act
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string pageUrl)
    {
        var getResponse = await client.GetAsync(pageUrl);
        getResponse.EnsureSuccessStatusCode();
        var html = await getResponse.Content.ReadAsStringAsync();
        var start = html.IndexOf("name=\"__RequestVerificationToken\"", StringComparison.Ordinal);
        if (start < 0)
            start = html.IndexOf("__RequestVerificationToken", StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;
        var valueStart = html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7;
        var valueEnd = html.IndexOf('"', valueStart);
        return valueStart >= 7 && valueEnd > valueStart ? html[valueStart..valueEnd] : string.Empty;
    }
}
using System.Net;

using GMO.Family.Web.Controllers;
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

    [Fact]
    public async Task Login_POST_when_user_already_has_tree_redirects_without_setting_current_tree()
    {
        // Arrange: register then login again (user already has Default tree; EnsureDefaultFamilyTreeAsync returns null)
        var client = _fixture.CreateClient(signIn: false);
        var email = "existing-" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
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
        Assert.StartsWith("/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task SignOut_POST_signs_out_and_redirects_to_Home()
    {
        // Arrange
        var token = await GetAntiforgeryTokenAsync(_client, "/FamilyTree");
        var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };

        // Act
        var response = await _client.PostAsync("/Account/SignOut", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ForgotPassword_GET_returns_200_and_form()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);

        // Act
        var response = await client.GetAsync("/Account/ForgotPassword");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Forgot", html);
    }

    [Fact]
    public async Task ForgotPassword_POST_with_existing_email_redirects_to_confirmation()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);
        var email = "forgot-" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
        var regToken = await GetAntiforgeryTokenAsync(client, "/Account/Register");
        await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "TestPassword1!",
            ["ConfirmPassword"] = "TestPassword1!",
            ["__RequestVerificationToken"] = regToken
        }));
        client = _fixture.CreateClient(signIn: false);
        var token = await GetAntiforgeryTokenAsync(client, "/Account/ForgotPassword");
        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["__RequestVerificationToken"] = token
        };

        // Act
        var response = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("ForgotPasswordConfirmation", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ForgotPassword_POST_with_unknown_email_redirects_to_confirmation()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);
        var token = await GetAntiforgeryTokenAsync(client, "/Account/ForgotPassword");
        var form = new Dictionary<string, string>
        {
            ["Email"] = "nonexistent-" + Guid.NewGuid().ToString("N") + "@example.com",
            ["__RequestVerificationToken"] = token
        };

        // Act
        var response = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("ForgotPasswordConfirmation", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ForgotPasswordConfirmation_GET_returns_200()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);

        // Act
        var response = await client.GetAsync("/Account/ForgotPasswordConfirmation");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ResetPassword_GET_with_token_and_email_returns_200()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);
        var token = "some-token";
        var email = "user@example.com";

        // Act
        var response = await client.GetAsync($"/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Reset", html);
    }

    [Fact]
    public async Task ResetPassword_GET_without_token_returns_400()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);

        // Act
        var response = await client.GetAsync("/Account/ResetPassword?email=u@e.com");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_POST_with_invalid_token_returns_view_with_errors()
    {
        // Arrange: existing user, invalid reset code
        var client = _fixture.CreateClient(signIn: false);
        var email = "reset-" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
        var regToken = await GetAntiforgeryTokenAsync(client, "/Account/Register");
        await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "TestPassword1!",
            ["ConfirmPassword"] = "TestPassword1!",
            ["__RequestVerificationToken"] = regToken
        }));
        client = _fixture.CreateClient(signIn: false);
        var resetGet = await client.GetAsync($"/Account/ResetPassword?token=any&email={Uri.EscapeDataString(email)}");
        resetGet.EnsureSuccessStatusCode();
        var resetHtml = await resetGet.Content.ReadAsStringAsync();
        var resetPageToken = GetAntiforgeryTokenFromHtml(resetHtml);
        var form = new Dictionary<string, string>
        {
            ["Code"] = "invalid-reset-token",
            ["Email"] = email,
            ["Password"] = "NewPassword1!",
            ["ConfirmPassword"] = "NewPassword1!",
            ["__RequestVerificationToken"] = resetPageToken
        };

        // Act
        var response = await client.PostAsync("/Account/ResetPassword", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Reset", html);
    }

    [Fact]
    public async Task ResetPasswordConfirmation_GET_returns_200()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);

        // Act
        var response = await client.GetAsync("/Account/ResetPasswordConfirmation");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AccessDenied_GET_returns_200()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);

        // Act
        var response = await client.GetAsync("/Account/AccessDenied");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UploadPhoto_GET_returns_200_when_authenticated()
    {
        // Arrange
        // (_client is signed in)

        // Act
        var response = await _client.GetAsync("/Account/UploadPhoto");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Upload", html);
    }

    [Fact]
    public async Task UploadPhoto_POST_without_file_redirects_with_error()
    {
        // Arrange
        var token = await GetAntiforgeryTokenAsync(_client, "/Account/UploadPhoto");
        var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };

        // Act
        var response = await _client.PostAsync("/Account/UploadPhoto", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var redirectResponse = await _client.GetAsync(response.Headers.Location);
        var html = await redirectResponse.Content.ReadAsStringAsync();
        Assert.Contains("Please select an image file", html);
    }

    [Fact]
    public async Task UploadPhoto_POST_with_valid_image_redirects_with_success()
    {
        // Arrange: minimal PNG (1x1)
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xFF, 0xFF, 0x3F, 0x00, 0x05, 0xFE, 0x02, 0xFE, 0xDC, 0xCC, 0x59, 0xE7, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
        var token = await GetAntiforgeryTokenAsync(_client, "/Account/UploadPhoto");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(token), "__RequestVerificationToken");
        content.Add(new ByteArrayContent(pngBytes), "photo", "photo.png");

        // Act
        var response = await _client.PostAsync("/Account/UploadPhoto", content);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var redirectResponse = await _client.GetAsync(response.Headers.Location);
        var html = await redirectResponse.Content.ReadAsStringAsync();
        Assert.Contains("Profile picture updated", html);
    }

    [Fact]
    public async Task SwitchFamilyTree_POST_redirects_to_Home()
    {
        // Arrange: ensure signed-in user has a tree (create one), then switch to it
        var createToken = await GetAntiforgeryTokenAsync(_client, "/FamilyTree/Create");
        await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "SwitchTest " + Guid.NewGuid().ToString("N")[..6],
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = createToken
        }));
        long treeId;
        using (var scope = _fixture.CreateScope())
        {
            var db = _fixture.GetDbContext(scope);
            var tree = await db.FamilyTrees.FirstOrDefaultAsync();
            Assert.NotNull(tree);
            treeId = tree.Id;
        }
        var token = await GetAntiforgeryTokenAsync(_client, "/FamilyTree");
        var form = new Dictionary<string, string>
        {
            ["id"] = treeId.ToString(),
            ["__RequestVerificationToken"] = token
        };

        // Act
        var response = await _client.PostAsync("/Account/SwitchFamilyTree", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/", response.Headers.Location.ToString());
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string pageUrl)
    {
        var getResponse = await client.GetAsync(pageUrl);
        getResponse.EnsureSuccessStatusCode();
        var html = await getResponse.Content.ReadAsStringAsync();
        return GetAntiforgeryTokenFromHtml(html);
    }

    private static string GetAntiforgeryTokenFromHtml(string html)
    {
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
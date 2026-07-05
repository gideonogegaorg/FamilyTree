using System.Net;

using GMO.Family.Web.Data;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.Family.Web.IntegrationTests;

public sealed class PhotoUploadTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;

    public PhotoUploadTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task UploadPhoto_POST_with_png_icon_returns_json_success_and_photo_is_served()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "HP-Folder.png");
        Assert.True(File.Exists(iconPath), $"Test asset missing: {iconPath}");

        var token = await GetAntiforgeryTokenFromProfileFormAsync();
        await using var fileStream = File.OpenRead(iconPath);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(token), "__RequestVerificationToken");
        content.Add(new StreamContent(fileStream), "photo", "HP-Folder.png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/UploadPhoto");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Content = content;

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", json);

        var photoResponse = await _client.GetAsync("/photos/profiles/me");
        Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
        Assert.Equal("image/png", photoResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UploadMemberPhoto_POST_with_png_icon_returns_json_success_and_photo_is_served()
    {
        var memberId = await EnsureMemberIdAsync();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "HP-Explorer.png");
        Assert.True(File.Exists(iconPath), $"Test asset missing: {iconPath}");

        var token = await GetAntiforgeryTokenAsync("/");
        await using var fileStream = File.OpenRead(iconPath);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(token), "__RequestVerificationToken");
        content.Add(new StringContent(memberId.ToString()), "memberId");
        content.Add(new StreamContent(fileStream), "photo", "HP-Explorer.png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/FamilyMember/UploadMemberPhoto");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Content = content;

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", json);

        var photoResponse = await _client.GetAsync($"/photos/members/{memberId}");
        Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
        Assert.Equal("image/png", photoResponse.Content.Headers.ContentType?.MediaType);
    }

    private async Task<string> GetAntiforgeryTokenFromProfileFormAsync()
    {
        var html = await (await _client.GetAsync("/")).Content.ReadAsStringAsync();
        var formStart = html.IndexOf("id=\"profile-photo-form\"", StringComparison.Ordinal);
        Assert.True(formStart >= 0, "Profile photo form should be present on home page.");
        var slice = html[formStart..];
        var tokenStart = slice.IndexOf("name=\"__RequestVerificationToken\"", StringComparison.Ordinal);
        Assert.True(tokenStart >= 0, "Profile photo form should include one antiforgery token.");
        var valueStart = slice.IndexOf("value=\"", tokenStart, StringComparison.Ordinal) + 7;
        var valueEnd = slice.IndexOf('"', valueStart);
        return slice[valueStart..valueEnd];
    }

    private async Task<long> EnsureMemberIdAsync()
    {
        var indexResponse = await _client.GetAsync("/");
        if (indexResponse.StatusCode == HttpStatusCode.Redirect)
        {
            var createToken = await GetAntiforgeryTokenAsync("/FamilyTree/Create");
            await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = "PhotoUploadTest " + Guid.NewGuid().ToString("N")[..6],
                ["Uid"] = Guid.NewGuid().ToString(),
                ["__RequestVerificationToken"] = createToken
            }));
            indexResponse = await _client.GetAsync("/");
        }

        var html = await indexResponse.Content.ReadAsStringAsync();
        if (html.Contains("Your family tree is empty"))
        {
            var getResponse = await _client.GetAsync("/Home/AddFirstMember");
            getResponse.EnsureSuccessStatusCode();
            var addHtml = await getResponse.Content.ReadAsStringAsync();
            var token = await GetAntiforgeryTokenAsync("/Home/AddFirstMember");
            var familyTreeIdStart = addHtml.IndexOf("value=\"", addHtml.IndexOf("FamilyTreeId", StringComparison.Ordinal), StringComparison.Ordinal) + 7;
            var familyTreeIdEnd = addHtml.IndexOf('"', familyTreeIdStart);
            var familyTreeId = addHtml[familyTreeIdStart..familyTreeIdEnd];
            await _client.PostAsync("/Home/AddFirstMember", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FamilyTreeId"] = familyTreeId,
                ["Name"] = "Photo Test",
                ["IsMale"] = "true",
                ["__RequestVerificationToken"] = token
            }));
        }

        using var scope = _fixture.CreateScope();
        var db = _fixture.GetDbContext(scope);
        var member = await db.FamilyMembers.AsNoTracking().FirstAsync();
        return member.Id;
    }

    private async Task<string> GetAntiforgeryTokenAsync(string pageUrl)
    {
        var html = await (await _client.GetAsync(pageUrl)).Content.ReadAsStringAsync();
        var start = html.IndexOf("name=\"__RequestVerificationToken\"", StringComparison.Ordinal);
        var valueStart = html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.FamilyTree.Web.IntegrationTests;

public class PhotosIntegrationTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;

    public PhotosIntegrationTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Member_photo_requires_authentication()
    {
        var anonClient = _fixture.CreateClient(signIn: false);
        var response = await anonClient.GetAsync("/photos/members/1");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Expected auth challenge, got {response.StatusCode}");
    }

    [Fact]
    public async Task Upload_and_get_member_photo_succeeds_for_tree_owner()
    {
        await EnsureTreeWithOneMemberAsync();
        long memberId;
        using (var scope = _fixture.CreateScope())
        {
            var db = _fixture.GetDbContext(scope);
            memberId = await db.FamilyMembers.Select(m => m.Id).FirstAsync();
        }

        var indexHtml = await GetHomeHtmlAsync();
        var token = GetAntiforgeryTokenFromHtml(indexHtml);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(memberId.ToString()), "memberId");
        content.Add(new StringContent(token), "__RequestVerificationToken");
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        content.Add(new ByteArrayContent(png) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } }, "photo", "test.png");

        var uploadResponse = await _client.PostAsync("/FamilyMember/UploadMemberPhoto", content);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadJson = await uploadResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", uploadJson.Replace(" ", ""));

        var photoResponse = await _client.GetAsync($"/photos/members/{memberId}");
        photoResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", photoResponse.Content.Headers.ContentType?.MediaType);
        var bytes = await photoResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(png, bytes);
    }

    [Fact]
    public async Task SetTreeCardViewMode_redirects_and_applies_css_class()
    {
        await EnsureTreeWithOneMemberAsync();
        var token = await GetAntiforgeryTokenAsync(_client, "/");
        var form = new Dictionary<string, string>
        {
            ["mode"] = "2", // Large
            ["__RequestVerificationToken"] = token
        };

        var postResponse = await _client.PostAsync("/Account/SetTreeCardViewMode", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var indexResponse = await _client.GetAsync("/");
        indexResponse.EnsureSuccessStatusCode();
        var html = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("ft-view-large", html);
    }

    [Fact]
    public async Task SetTreeCardViewMode_photo_only_redirects_and_applies_css_class()
    {
        await EnsureTreeWithOneMemberAsync();
        var token = await GetAntiforgeryTokenAsync(_client, "/");
        var form = new Dictionary<string, string>
        {
            ["mode"] = "5", // PhotoMedium
            ["__RequestVerificationToken"] = token
        };

        var postResponse = await _client.PostAsync("/Account/SetTreeCardViewMode", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var indexResponse = await _client.GetAsync("/");
        var html = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("ft-view-photo-medium", html);
    }

    private async Task EnsureTreeWithOneMemberAsync()
    {
        var indexResponse = await _client.GetAsync("/");
        if (indexResponse.StatusCode == HttpStatusCode.Redirect)
        {
            await CreateTreeAndSetCurrentAsync();
            indexResponse = await _client.GetAsync("/");
        }

        var html = await indexResponse.Content.ReadAsStringAsync();
        if (html.Contains("Your family tree is empty"))
            await AddFirstMemberAsync();
    }

    private async Task CreateTreeAndSetCurrentAsync()
    {
        var createToken = await GetAntiforgeryTokenAsync(_client, "/FamilyTree/Create");
        await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "PhotoTest " + Guid.NewGuid().ToString("N")[..6],
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = createToken
        }));

        long treeId;
        using (var scope = _fixture.CreateScope())
        {
            var db = _fixture.GetDbContext(scope);
            treeId = (await db.FamilyTrees.OrderByDescending(t => t.Id).FirstAsync()).Id;
        }

        var switchToken = await GetAntiforgeryTokenAsync(_client, "/FamilyTree");
        await _client.PostAsync("/Account/SwitchFamilyTree", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = treeId.ToString(),
            ["__RequestVerificationToken"] = switchToken
        }));
    }

    private async Task AddFirstMemberAsync()
    {
        var getResponse = await _client.GetAsync("/Home/AddFirstMember");
        getResponse.EnsureSuccessStatusCode();
        var html = await getResponse.Content.ReadAsStringAsync();
        var token = GetAntiforgeryTokenFromHtml(html);
        var familyTreeIdStart = html.IndexOf("value=\"", html.IndexOf("FamilyTreeId", StringComparison.Ordinal), StringComparison.Ordinal) + 7;
        var familyTreeIdEnd = html.IndexOf('"', familyTreeIdStart);
        var familyTreeId = html[familyTreeIdStart..familyTreeIdEnd];

        await _client.PostAsync("/Home/AddFirstMember", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FamilyTreeId"] = familyTreeId,
            ["Name"] = "Photo Person",
            ["IsMale"] = "true",
            ["__RequestVerificationToken"] = token
        }));
    }

    private async Task<string> GetHomeHtmlAsync()
    {
        var response = await _client.GetAsync("/");
        for (var i = 0; i < 5 && response.StatusCode == HttpStatusCode.Redirect; i++)
        {
            var location = response.Headers.Location;
            if (location == null) break;
            var nextUrl = location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
            response = await _client.GetAsync(nextUrl);
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string pageUrl)
    {
        var getResponse = await client.GetAsync(pageUrl);
        for (var i = 0; i < 5 && getResponse.StatusCode == HttpStatusCode.Redirect; i++)
        {
            var location = getResponse.Headers.Location;
            if (location == null) break;
            var nextUrl = location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
            getResponse = await client.GetAsync(nextUrl);
        }
        getResponse.EnsureSuccessStatusCode();
        return GetAntiforgeryTokenFromHtml(await getResponse.Content.ReadAsStringAsync());
    }

    private static string GetAntiforgeryTokenFromHtml(string html)
    {
        var start = html.IndexOf("name=\"__RequestVerificationToken\"", StringComparison.Ordinal);
        if (start < 0) start = html.IndexOf("__RequestVerificationToken", StringComparison.Ordinal);
        var valueStart = html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }
}
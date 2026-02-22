using System.Net;

using Xunit;

namespace GMO.Family.Web.IntegrationTests;

public class FamilyTreeCrudTests : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client;

    public FamilyTreeCrudTests(WebAppFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Index_returns_200_and_lists_family_trees()
    {
        var response = await _client.GetAsync("/FamilyTree");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Family Trees", html);
        Assert.Contains("Create New", html);
    }

    [Fact]
    public async Task Create_GET_returns_200_and_form()
    {
        var response = await _client.GetAsync("/FamilyTree/Create");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Create", html);
        Assert.Contains("Name", html);
    }

    [Fact]
    public async Task Create_POST_creates_and_redirects_to_Index()
    {
        var name = "Test Tree " + Guid.NewGuid().ToString("N")[..8];
        var form = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync("/FamilyTree/Create")
        };
        var response = await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/FamilyTree", response.Headers.Location?.ToString());
        var indexResponse = await _client.GetAsync("/FamilyTree");
        var html = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains(name, html);
    }

    private async Task<string> GetAntiforgeryTokenAsync(string pageUrl)
    {
        var getResponse = await _client.GetAsync(pageUrl);
        getResponse.EnsureSuccessStatusCode();
        var html = await getResponse.Content.ReadAsStringAsync();
        var start = html.IndexOf("name=\"__RequestVerificationToken\"");
        if (start < 0)
            start = html.IndexOf("__RequestVerificationToken");
        if (start < 0)
            return string.Empty;
        var valueStart = html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }
}
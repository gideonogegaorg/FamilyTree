using System.Net;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.FamilyTree.Web.IntegrationTests;

/// <summary>
/// Integration tests for Home/Index (family tree graph) and orientation/lineage settings.
/// </summary>
public class HomeControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;

    public HomeControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Index_with_tree_and_member_returns_graph_with_data_orientation_and_data_lineage()
    {
        // Arrange: ensure we have a tree with at least one member so the graph is rendered
        await EnsureTreeWithOneMemberAsync();

        // Act
        var response = await _client.GetAsync("/Home/Index");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"family-tree-graph\"", html);
        // Defaults: Horizontal and Paternal (from UserProfile or session)
        Assert.True(
            html.Contains("data-orientation=\"Horizontal\"") || html.Contains("data-orientation=\"Vertical\""),
            "Page should contain data-orientation (Horizontal or Vertical)");
        Assert.True(
            html.Contains("data-lineage-mode=\"Paternal\"") || html.Contains("data-lineage-mode=\"Maternal\""),
            "Page should contain data-lineage-mode (Paternal or Maternal)");
    }

    [Fact]
    public async Task SetTreeViewOrientation_redirects_to_Home_and_next_Index_shows_vertical_orientation()
    {
        await EnsureTreeWithOneMemberAsync();

        var token = await GetAntiforgeryTokenAsync(_client, "/Home/Index");
        var form = new Dictionary<string, string>
        {
            ["orientation"] = "1", // Vertical
            ["__RequestVerificationToken"] = token
        };

        var postResponse = await _client.PostAsync("/Account/SetTreeViewOrientation", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);
        Assert.Contains("/", postResponse.Headers.Location.ToString());

        var indexResponse = await _client.GetAsync("/Home/Index");
        indexResponse.EnsureSuccessStatusCode();
        var html = await indexResponse.Content.ReadAsStringAsync();
        Assert.True(
            html.Contains("data-orientation=\"Vertical\""),
            "After setting Vertical, page should contain data-orientation Vertical");
    }

    [Fact]
    public async Task SetLineageMode_redirects_to_Home_and_next_Index_shows_maternal_lineage()
    {
        await EnsureTreeWithOneMemberAsync();

        var token = await GetAntiforgeryTokenAsync(_client, "/Home/Index");
        var form = new Dictionary<string, string>
        {
            ["mode"] = "1", // Maternal
            ["__RequestVerificationToken"] = token
        };

        var postResponse = await _client.PostAsync("/Account/SetLineageMode", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var indexResponse = await _client.GetAsync("/Home/Index");
        indexResponse.EnsureSuccessStatusCode();
        var html = await indexResponse.Content.ReadAsStringAsync();
        Assert.True(
            html.Contains("data-lineage-mode=\"Maternal\""),
            "After setting Maternal, page should contain data-lineage-mode Maternal");
    }

    private async Task EnsureTreeWithOneMemberAsync()
    {
        var indexResponse = await _client.GetAsync("/Home/Index");
        if (indexResponse.StatusCode == HttpStatusCode.Redirect && indexResponse.Headers.Location?.ToString().Contains("FamilyTree") == true)
        {
            await CreateTreeAndSetCurrentAsync();
            indexResponse = await _client.GetAsync("/Home/Index");
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
            ["Name"] = "OrientationTest " + Guid.NewGuid().ToString("N")[..6],
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = createToken
        }));

        long treeId;
        using (var scope = _fixture.CreateScope())
        {
            var db = _fixture.GetDbContext(scope);
            var tree = await db.FamilyTrees.OrderByDescending(t => t.Id).FirstOrDefaultAsync();
            Assert.NotNull(tree);
            treeId = tree.Id;
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
        var familyTreeId = familyTreeIdStart >= 7 && familyTreeIdEnd > familyTreeIdStart
            ? html[familyTreeIdStart..familyTreeIdEnd]
            : "";

        await _client.PostAsync("/Home/AddFirstMember", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FamilyTreeId"] = familyTreeId,
            ["Name"] = "Test Person",
            ["IsMale"] = "true",
            ["__RequestVerificationToken"] = token
        }));
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
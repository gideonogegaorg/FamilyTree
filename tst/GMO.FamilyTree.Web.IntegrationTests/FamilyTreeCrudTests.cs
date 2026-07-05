using System.Net;

using GMO.FamilyTree.Web.Data;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.FamilyTree.Web.IntegrationTests;

public class FamilyTreeCrudTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;

    public FamilyTreeCrudTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Index_returns_200_and_lists_family_trees()
    {
        // Arrange
        // (client from fixture)

        // Act
        var response = await _client.GetAsync("/FamilyTree");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Family trees", html);
        Assert.Contains("Create new tree", html);
    }

    [Fact]
    public async Task Create_GET_returns_200_and_form()
    {
        // Arrange
        // (client from fixture)

        // Act
        var response = await _client.GetAsync("/FamilyTree/Create");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Create", html);
        Assert.Contains("Name", html);
    }

    [Fact]
    public async Task Create_POST_creates_and_redirects_to_Home()
    {
        // Arrange
        var name = "Test Tree " + Guid.NewGuid().ToString("N")[..8];
        var form = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync("/FamilyTree/Create")
        };

        // Act
        var response = await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.True(location == "/" || location.Contains("/Home", StringComparison.OrdinalIgnoreCase),
            $"Expected redirect to Home, got {location}");
        var homeResponse = await _client.GetAsync("/");
        var html = await homeResponse.Content.ReadAsStringAsync();
        Assert.Contains(name, html);
    }

    [Fact]
    public async Task Create_POST_with_empty_name_returns_200_and_form()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Name"] = "",
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync("/FamilyTree/Create")
        };

        // Act
        var response = await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Create", html);
    }

    [Fact]
    public async Task Index_redirects_when_not_authenticated()
    {
        // Arrange
        var client = _fixture.CreateClient(signIn: false);

        // Act
        var response = await client.GetAsync("/FamilyTree");

        // Assert — [Authorize] redirects anonymous users to login
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Edit_GET_returns_200_when_valid_id()
    {
        // Arrange
        var name = "Edit Tree " + Guid.NewGuid().ToString("N")[..8];
        var createForm = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync("/FamilyTree/Create")
        };
        await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(createForm));
        var indexHtml = await (await _client.GetAsync("/FamilyTree")).Content.ReadAsStringAsync();
        var namePos = indexHtml.IndexOf(name, StringComparison.Ordinal);
        Assert.True(namePos >= 0);
        var rowSlice = indexHtml[namePos..];
        var editLinkStart = rowSlice.IndexOf("/FamilyTree/Edit/", StringComparison.Ordinal);
        Assert.True(editLinkStart >= 0);
        var path = rowSlice.Substring(editLinkStart, rowSlice.IndexOf('"', editLinkStart) - editLinkStart);
        var id = long.Parse(path.Split('/').Last());

        // Act
        var response = await _client.GetAsync($"/FamilyTree/Edit/{id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(name, html);
    }

    [Fact]
    public async Task Edit_POST_updates_name_and_redirects_to_Home()
    {
        // Arrange
        var name = "Original " + Guid.NewGuid().ToString("N")[..6];
        var createForm = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync("/FamilyTree/Create")
        };
        await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(createForm));
        var indexHtml = await (await _client.GetAsync("/FamilyTree")).Content.ReadAsStringAsync();
        var namePos = indexHtml.IndexOf(name, StringComparison.Ordinal);
        Assert.True(namePos >= 0);
        var rowSlice = indexHtml[namePos..];
        var editLinkStart = rowSlice.IndexOf("/FamilyTree/Edit/", StringComparison.Ordinal);
        var path = rowSlice.Substring(editLinkStart, rowSlice.IndexOf('"', editLinkStart) - editLinkStart);
        var id = long.Parse(path.Split('/').Last());
        var editPageHtml = await (await _client.GetAsync($"/FamilyTree/Edit/{id}")).Content.ReadAsStringAsync();
        var token = GetAntiforgeryTokenFromHtml(editPageHtml);
        var uidMatch = System.Text.RegularExpressions.Regex.Match(editPageHtml, @"name=""Uid""[^>]*value=""([^""]+)""");
        var uid = uidMatch.Success ? uidMatch.Groups[1].Value : Guid.NewGuid().ToString();
        var newName = "Updated " + Guid.NewGuid().ToString("N")[..6];
        var editForm = new Dictionary<string, string>
        {
            ["Id"] = id.ToString(),
            ["Name"] = newName,
            ["Uid"] = uid,
            ["__RequestVerificationToken"] = token
        };

        // Act
        var response = await _client.PostAsync($"/FamilyTree/Edit/{id}", new FormUrlEncodedContent(editForm));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.True(location == "/" || location.Contains("/Home", StringComparison.OrdinalIgnoreCase),
            $"Expected redirect to Home, got {location}");
        var homeResponse = await _client.GetAsync("/");
        var html = await homeResponse.Content.ReadAsStringAsync();
        Assert.Contains(newName, html);
    }

    [Fact]
    public async Task Edit_POST_with_empty_name_returns_200_and_form()
    {
        // Arrange
        var name = "Tree For Edit " + Guid.NewGuid().ToString("N")[..6];
        var createForm = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync("/FamilyTree/Create")
        };
        await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(createForm));
        var indexHtml = await (await _client.GetAsync("/FamilyTree")).Content.ReadAsStringAsync();
        var namePos = indexHtml.IndexOf(name, StringComparison.Ordinal);
        Assert.True(namePos >= 0);
        var rowSlice = indexHtml[namePos..];
        var editLinkStart = rowSlice.IndexOf("/FamilyTree/Edit/", StringComparison.Ordinal);
        var path = rowSlice.Substring(editLinkStart, rowSlice.IndexOf('"', editLinkStart) - editLinkStart);
        var id = long.Parse(path.Split('/').Last());
        var editForm = new Dictionary<string, string>
        {
            ["Id"] = id.ToString(),
            ["Name"] = "",
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync($"/FamilyTree/Edit/{id}")
        };

        // Act
        var response = await _client.PostAsync($"/FamilyTree/Edit/{id}", new FormUrlEncodedContent(editForm));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Edit", html);
    }

    [Fact]
    public async Task Delete_GET_returns_200_when_valid_id()
    {
        // Arrange
        var name = "Delete Tree " + Guid.NewGuid().ToString("N")[..8];
        var createForm = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync("/FamilyTree/Create")
        };
        await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(createForm));
        var indexHtml = await (await _client.GetAsync("/FamilyTree")).Content.ReadAsStringAsync();
        var namePos = indexHtml.IndexOf(name, StringComparison.Ordinal);
        Assert.True(namePos >= 0);
        var rowSlice = indexHtml[namePos..];
        var deleteLinkStart = rowSlice.IndexOf("/FamilyTree/Delete/", StringComparison.Ordinal);
        Assert.True(deleteLinkStart >= 0);
        var path = rowSlice.Substring(deleteLinkStart, rowSlice.IndexOf('"', deleteLinkStart) - deleteLinkStart);
        var id = long.Parse(path.Split('/').Last());

        // Act
        var response = await _client.GetAsync($"/FamilyTree/Delete/{id}");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains(name, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeleteConfirmed_removes_tree_and_redirects_to_Home()
    {
        // Arrange
        var name = "To Delete " + Guid.NewGuid().ToString("N")[..8];
        var createForm = new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Uid"] = Guid.NewGuid().ToString(),
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync("/FamilyTree/Create")
        };
        await _client.PostAsync("/FamilyTree/Create", new FormUrlEncodedContent(createForm));
        long id;
        using (var scope = _fixture.CreateScope())
        {
            var db = _fixture.GetDbContext(scope);
            var tree = await db.FamilyTrees.FirstOrDefaultAsync(t => t.Name == name);
            Assert.NotNull(tree);
            id = tree.Id;
        }
        var deleteForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync($"/FamilyTree/Delete/{id}")
        };

        // Act
        var response = await _client.PostAsync($"/FamilyTree/Delete/{id}", new FormUrlEncodedContent(deleteForm));

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.True(location == "/" || location.Contains("/Home", StringComparison.OrdinalIgnoreCase),
            $"Expected redirect to Home, got {location}");
        var listResponse = await _client.GetAsync("/FamilyTree");
        var html = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(name, html);
    }

    private async Task<string> GetAntiforgeryTokenAsync(string pageUrl)
    {
        var getResponse = await _client.GetAsync(pageUrl);
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
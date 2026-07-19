using Microsoft.Playwright;

using Xunit;

namespace GMO.FamilyTree.Web.UiTests;

[Collection("AppFixture Collection")]
public class MemberDetailsHoverTests : IAsyncLifetime
{
    private readonly AppFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public MemberDetailsHoverTests(AppFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 720 } });
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _playwright?.Dispose();
        }
    }

    private async Task OpenTreeAsync()
    {
        await _page.GotoAsync(_fixture.ServerAddress + "/TestAuth/SignIn");
        await _page.GotoAsync(_fixture.ServerAddress + AppFixture.TreePagePath);
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
    }

    private async Task SetGraphViewClassAsync(string viewClass)
    {
        await _page.EvaluateAsync(
            @"(viewClass) => {
                const graph = document.querySelector('#family-tree-graph');
                if (!graph) throw new Error('missing graph');
                [...graph.classList].filter(c => c.startsWith('ft-view-')).forEach(c => graph.classList.remove(c));
                graph.classList.add(viewClass);
            }",
            viewClass);
    }

    [Fact]
    public async Task Desktop_hover_compact_shows_full_details_and_is_non_interactive()
    {
        await OpenTreeAsync();
        await SetGraphViewClassAsync("ft-view-compact");

        var card = _page.Locator(".family-tree-card")
            .Filter(new() { Has = _page.GetByText("Father", new() { Exact = true }) })
            .First;
        await card.HoverAsync();

        var hover = _page.Locator("#member-hover-card");
        await hover.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var text = await hover.InnerTextAsync();
        Assert.Contains("Father", text);
        Assert.Contains("Parents:", text);
        Assert.Contains("Siblings:", text);
        Assert.Contains("Fathers Brother", text);
        Assert.Contains("Children:", text);
        Assert.DoesNotContain("Manage", text);

        var pointerEvents = await hover.EvaluateAsync<string>("el => getComputedStyle(el).pointerEvents");
        Assert.Equal("none", pointerEvents);
    }

    [Fact]
    public async Task Desktop_hover_details_view_includes_relationship_names()
    {
        await OpenTreeAsync();
        await SetGraphViewClassAsync("ft-view-details");

        var card = _page.Locator(".family-tree-card")
            .Filter(new() { Has = _page.GetByText("Father", new() { Exact = true }) })
            .First;
        await card.HoverAsync();

        var hover = _page.Locator("#member-hover-card");
        await hover.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var text = await hover.InnerTextAsync();
        Assert.Contains("Father", text);
        Assert.Contains("Children:", text);
        Assert.Contains("Me", text);
        Assert.Contains("Siblings:", text);
    }

    [Fact]
    public async Task Desktop_photo_only_hover_includes_manage_and_is_interactive()
    {
        await OpenTreeAsync();
        await SetGraphViewClassAsync("ft-view-photo-medium");

        var card = _page.Locator(".family-tree-card")
            .Filter(new() { Has = _page.GetByText("Father", new() { Exact = true }) })
            .First;
        await Assertions.Expect(card.Locator(".member-action-trigger")).ToBeVisibleAsync();
        await card.HoverAsync();

        var hover = _page.Locator("#member-hover-card");
        await hover.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.Contains("Manage", await hover.InnerTextAsync());
        var pointerEvents = await hover.EvaluateAsync<string>("el => getComputedStyle(el).pointerEvents");
        Assert.Equal("auto", pointerEvents);

        await hover.Locator(".ft-hover-manage-btn").ClickAsync();
        await _page.Locator("#member-action-popup").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"edit\"]").WaitForAsync();
    }

    [Fact]
    public async Task Desktop_card_body_click_opens_details_and_hides_hover()
    {
        await OpenTreeAsync();
        var card = _page.Locator(".family-tree-card")
            .Filter(new() { Has = _page.GetByText("Father", new() { Exact = true }) })
            .First;
        await card.HoverAsync();
        await _page.Locator("#member-hover-card").WaitForAsync(new() { State = WaitForSelectorState.Visible });

        await card.Locator(".family-tree-card-text").ClickAsync();
        var details = _page.Locator("#member-details-popup");
        await details.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.False(await _page.Locator("#member-hover-card").IsVisibleAsync());
        Assert.True(await details.Locator(".ft-details-section-title")
            .Filter(new() { HasText = "Siblings" }).CountAsync() > 0);
        Assert.True(await details.Locator(".ft-details-manage-btn").IsVisibleAsync());
        Assert.Equal(0, await details.Locator(".ft-details-photo-btn").CountAsync());
    }

    [Fact]
    public async Task Desktop_avatar_click_opens_photo_modal()
    {
        await OpenTreeAsync();
        var card = _page.Locator(".family-tree-card")
            .Filter(new() { Has = _page.GetByText("Father", new() { Exact = true }) })
            .First;
        await card.Locator(".member-photo-trigger").ClickAsync();
        await _page.Locator("#memberPhotoModal.show").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.False(await _page.Locator("#member-details-popup").IsVisibleAsync());
    }
}
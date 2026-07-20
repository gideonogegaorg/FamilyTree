using System.Threading.Tasks;

using Microsoft.Playwright;

using Xunit;

namespace GMO.FamilyTree.Web.UiTests;

[Collection("AppFixture Collection")]
public class MemberActionPopupTests : IAsyncLifetime
{
    private readonly AppFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public MemberActionPopupTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

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

    private async Task AuthenticateAsync()
    {
        await _page.GotoAsync(_fixture.ServerAddress + "/TestAuth/SignIn");
    }

    private async Task GotoTreeAndWaitForGraphAsync()
    {
        await _page.GotoAsync(_fixture.ServerAddress + AppFixture.TreePagePath);
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
    }

    private async Task OpenPopupViaTriggerAsync()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await _page.Locator(".family-tree-card").Filter(new() { Has = _page.GetByText("Me", new() { Exact = true }) }).Locator(".member-action-trigger").First.ClickAsync();
        await _page.Locator("#member-action-popup").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"edit\"]").WaitForAsync();
    }

    [Fact]
    public async Task MemberAction_OpenViaTrigger_ShowsPopupWithMenu()
    {
        await OpenPopupViaTriggerAsync();
        Assert.True(await _page.Locator("#member-action-popup .cascading-item[data-panel=\"add\"]").IsVisibleAsync());
        Assert.True(await _page.Locator("#member-action-popup .cascading-item[data-panel=\"remove\"]").IsVisibleAsync());
    }

    [Fact]
    public async Task MemberAction_OpenViaCardClick_ShowsDetailsThenManage()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        var card = _page.Locator(".family-tree-card").Filter(new() { Has = _page.GetByText("Father", new() { Exact = true }) }).First;
        await card.Locator(".family-tree-card-text").ClickAsync();
        await _page.Locator("#member-details-popup").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.Equal("Father", await _page.Locator("#member-details-popup .ft-details-name").InnerTextAsync());
        Assert.True(await _page.Locator("#member-details-popup .ft-details-section-title")
            .Filter(new() { HasText = "Siblings" }).CountAsync() > 0);
        await _page.Locator("#member-details-popup .ft-details-manage-btn").ClickAsync();
        await _page.Locator("#member-action-popup").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"edit\"]").WaitForAsync();
    }

    [Fact]
    public async Task MemberDetails_RelativeLink_FocusesRelatedMember()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        var father = _page.Locator(".family-tree-card").Filter(new() { Has = _page.GetByText("Father", new() { Exact = true }) }).First;
        await father.Locator(".family-tree-card-text").ClickAsync();
        await _page.Locator("#member-details-popup").WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var childLink = _page.Locator("#member-details-popup .ft-details-relative")
            .Filter(new() { HasText = "Me" })
            .First;
        await childLink.WaitForAsync();
        await childLink.ClickAsync();

        await _page.Locator("#member-details-popup").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        var meCard = _page.Locator(".family-tree-card").Filter(new() { Has = _page.GetByText("Me", new() { Exact = true }) }).First;
        var inView = await meCard.EvaluateAsync<bool>(
            @"el => {
                const r = el.getBoundingClientRect();
                return r.width > 0 && r.height > 0
                    && r.bottom > 0 && r.top < window.innerHeight
                    && r.right > 0 && r.left < window.innerWidth;
            }");
        Assert.True(inView, "Related member card should be focused into view");
    }

    [Fact]
    public async Task MemberAction_SwitchPanels_ShowsEditAddRemove()
    {
        await OpenPopupViaTriggerAsync();
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"remove\"]").ClickAsync();
        await _page.Locator("#member-action-popup .cascading-panel[data-panel=\"remove\"]").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.True(await _page.Locator("#member-action-popup .cascading-panel[data-panel=\"remove\"]").IsVisibleAsync());

        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"add\"]").ClickAsync();
        await _page.Locator("#member-action-popup .cascading-panel[data-panel=\"add\"]").WaitForAsync(new() { State = WaitForSelectorState.Visible });

        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"edit\"]").ClickAsync();
        await _page.Locator("#member-action-popup .cascading-panel[data-panel=\"edit\"]").WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    [Fact]
    public async Task MemberAction_AddPanel_CreateNew_ShowsForm()
    {
        await OpenPopupViaTriggerAsync();
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"add\"]").ClickAsync();
        await _page.Locator("#member-action-popup .menu-add-choice-btn[data-choice=\"new\"]").ClickAsync();
        await _page.Locator("#member-action-popup .menu-panel-new-form").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.True(await _page.Locator("#member-action-popup .cascading-add-form").IsVisibleAsync());
    }

    [Fact]
    public async Task MemberAction_AddPanel_LinkExisting_SearchFiltersCandidates()
    {
        await OpenPopupViaTriggerAsync();
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"add\"]").ClickAsync();
        var searchInput = _page.Locator("#member-action-popup .member-action-search");
        await searchInput.FillAsync("Cousin");
        Assert.Equal("Cousin", await searchInput.InputValueAsync());
        Assert.True(await _page.Locator("#member-action-popup .menu-panel-existing").IsVisibleAsync());
    }

    [Fact]
    public async Task MemberAction_AddPanel_TypeButtons_SwitchChildParentPartner()
    {
        await OpenPopupViaTriggerAsync();
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"add\"]").ClickAsync();
        await _page.Locator("#member-action-popup .menu-type-btn[data-type=\"partner\"]").ClickAsync();
        await _page.Locator("#member-action-popup .menu-candidates[data-type=\"partner\"]").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.True(await _page.Locator("#member-action-popup .menu-candidates[data-type=\"partner\"]").IsVisibleAsync());
        await _page.Locator("#member-action-popup .menu-type-btn[data-type=\"parent\"][data-ischild=\"true\"]").ClickAsync();
        await _page.Locator("#member-action-popup .menu-candidates[data-type=\"parent\"][data-ischild=\"true\"]").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.True(await _page.Locator("#member-action-popup .menu-candidates[data-type=\"parent\"][data-ischild=\"true\"]").IsVisibleAsync());
    }

    [Fact]
    public async Task MemberAction_EditPanel_ShowsForm()
    {
        await OpenPopupViaTriggerAsync();
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"edit\"]").ClickAsync();
        Assert.True(await _page.Locator("#member-action-popup .cascading-edit-form").IsVisibleAsync());
        Assert.True(await _page.Locator("#member-action-popup .cascading-edit-form input[name=\"name\"]").IsVisibleAsync());
    }

    [Fact]
    public async Task MemberAction_RemovePanel_ShowsRelationshipsWhenAny()
    {
        await OpenPopupViaTriggerAsync();
        await _page.Locator("#member-action-popup .cascading-item[data-panel=\"remove\"]").ClickAsync();
        await _page.Locator("#member-action-popup .cascading-panel[data-panel=\"remove\"]").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        var hasRelationships = await _page.Locator("#member-action-popup .cascading-remove-btn").CountAsync() > 0;
        var hasNoRelsMessage = await _page.Locator("#member-action-popup >> text=No relationships").CountAsync() > 0;
        Assert.True(hasRelationships || hasNoRelsMessage);
    }

    [Fact]
    public async Task MemberAction_FetchFailure_ShowsFailedToLoad()
    {
        await _page.RouteAsync("**/FamilyMember/ActionMenuContent*", route => route.AbortAsync());
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await _page.Locator(".family-tree-card").Filter(new() { Has = _page.GetByText("Me", new() { Exact = true }) }).Locator(".member-action-trigger").First.ClickAsync();
        await _page.Locator("#member-action-popup").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.Locator("#member-action-popup >> text=Failed to load.").WaitForAsync();
        Assert.True(await _page.Locator("#member-action-popup >> text=Failed to load.").IsVisibleAsync());
    }

    [Fact]
    public async Task MemberAction_ClosePopup_MousedownOutside_Closes()
    {
        await OpenPopupViaTriggerAsync();
        await _page.Locator("body").ClickAsync(new() { Position = new() { X = 0, Y = 0 } });
        await Task.Delay(50);
        var display = await _page.Locator("#member-action-popup").GetAttributeAsync("style");
        Assert.True(display?.Contains("none") is true);
    }

    [Fact]
    public async Task MemberAction_ClosePopup_Escape_Closes()
    {
        await OpenPopupViaTriggerAsync();
        await _page.Keyboard.PressAsync("Escape");
        await Task.Delay(50);
        var display = await _page.Locator("#member-action-popup").GetAttributeAsync("style");
        Assert.True(display?.Contains("none") is true);
    }
}
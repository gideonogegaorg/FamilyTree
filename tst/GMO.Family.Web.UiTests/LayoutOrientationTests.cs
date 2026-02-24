using Microsoft.Playwright;
using System.Threading.Tasks;
using Xunit;

namespace GMO.Family.Web.UiTests;

[Collection("AppFixture Collection")]
public class LayoutOrientationTests : IAsyncLifetime
{
    private readonly AppFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public LayoutOrientationTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    private async Task AuthenticateAsync()
    {
        // Hit the dummy endpoint to issue the auth cookie
        await _page.GotoAsync(_fixture.ServerAddress + "/TestAuth/SignIn");
    }

    [Fact]
    public async Task LayoutOrientation_CanToggleBetweenHorizontalAndVertical()
    {
        await AuthenticateAsync();

        // 1. Go to the root page (Family Tree)
        await _page.GotoAsync(_fixture.ServerAddress + "/");
        
        // Ensure the graph is loaded by waiting for at least one node to be injected by JS
        var graphNode = _page.Locator("#family-tree-graph .family-tree-card").First;
        try {
            await graphNode.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 5000 });
        } catch {
            var screenshotPath = System.IO.Path.Combine("..", "..", "..", "..", "..", "working", "debug_screenshot.png");
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(screenshotPath)))
            {
                screenshotPath = "debug_screenshot.png";
            }
            await _page.ScreenshotAsync(new() { Path = screenshotPath });
            throw;
        }

        var graph = _page.Locator("#family-tree-graph");

        // 2. Default orientation is generally Horizontal (enum 0) or whatever is set in DB.
        // Let's assert it is Horizontal initially.
        var classValue = await graph.GetAttributeAsync("class");
        Assert.Contains("ft-orientation-horizontal", classValue);

        // 3. Open user menu and click "Vertical"
        await _page.ClickAsync("#userMenuDropdown");
        
        // Wait for user menu to appear and click Vertical form button
        var verticalBtn = _page.Locator("button:has-text('Vertical')");
        await verticalBtn.WaitForAsync();
        await verticalBtn.ClickAsync();

        // 4. Page should reload (or redirect back), wait for graph again
        await _page.WaitForURLAsync(_fixture.ServerAddress + "/");
        graph = _page.Locator("#family-tree-graph");
        await graph.WaitForAsync();

        // Verify it is now Vertical (class removed)
        classValue = await graph.GetAttributeAsync("class");
        Assert.DoesNotContain("ft-orientation-horizontal", classValue);

        // 5. Open user menu and click "Horizontal" again
        await _page.ClickAsync("#userMenuDropdown");
        var horizontalBtn = _page.Locator("button:has-text('Horizontal')");
        await horizontalBtn.WaitForAsync();
        await horizontalBtn.ClickAsync();

        // 6. Page should reload
        await _page.WaitForURLAsync(_fixture.ServerAddress + "/");
        graph = _page.Locator("#family-tree-graph");
        await graph.WaitForAsync();

        // Verify it is Horizontal again
        classValue = await graph.GetAttributeAsync("class");
        Assert.Contains("ft-orientation-horizontal", classValue);
    }
}

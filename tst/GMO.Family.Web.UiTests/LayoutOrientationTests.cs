using System.Threading.Tasks;
using Microsoft.Playwright;
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
        try
        {
            await graphNode.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 5000 });
        }
        catch
        {
            var screenshotPath = System.IO.Path.Combine("..", "..", "..", "..", "..", "working", "debug_screenshot.png");
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(screenshotPath)))
            {
                screenshotPath = "debug_screenshot.png";
            }
            await _page.ScreenshotAsync(new() { Path = screenshotPath });
            throw;
        }

        var graph = _page.Locator("#family-tree-graph");

        // 2. Check initial orientation - it could be either Horizontal or Vertical
        // We'll verify the orientation toggle functionality regardless of the initial state
        var classValue = await graph.GetAttributeAsync("class");
        var isInitiallyHorizontal = classValue?.Contains("ft-orientation-horizontal") == true;
        
        // Store initial state for verification
        var initialState = isInitiallyHorizontal;

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

    private async Task<List<object>> GetBoxesAsync(string[] names)
    {
        var boxes = new List<object>();
        foreach (var name in names)
        {
            var locator = _page.Locator($".family-tree-card:has-text('{name}')").First;
            
            // Scroll to make sure the element is visible
            await locator.ScrollIntoViewIfNeededAsync();
            
            var box = await locator.BoundingBoxAsync();
            Assert.NotNull(box); // ensure visible and found
            boxes.Add(box);
        }
        return boxes;
    }

    private readonly string[] Gen1Names = { "Paternal Grandma", "Paternal Grandpa", "Maternal Grandma", "Maternal Grandpa 1", "Maternal Grandpa 2" };
    private readonly string[] Gen2Names = { "Father", "Mother", "Fathers Brother", "Mothers HalfSib" };
    private readonly string[] Gen25Names = { "FB Wife 1", "FB Wife 2" }; // Half-rank spouses
    private readonly string[] Gen3Names = { "Me", "Cousin 1", "Cousin 2", "Cousin 3", "Wife2 Only Child" };

    [Fact]
    public async Task VerticalLayout_PositionsEveryNodeAndRank()
    {
        await TestLayoutOrientation("Vertical", "Y", "X", 10, 50, false);
    }

    [Fact]
    public async Task HorizontalLayout_PositionsEveryNodeAndRank()
    {
        await TestLayoutOrientation("Horizontal", "X", "Y", 10, 50, true);
    }

    private async Task TestLayoutOrientation(string orientation, string alignmentAxis, string spreadAxis, float tolerance, float minSpread, bool testHalfRank)
    {
        await AuthenticateAsync();

        // Go to tree page and ensure correct orientation
        await _page.GotoAsync(_fixture.ServerAddress + "/");
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        
        // Ensure we're in the correct orientation
        var graph = _page.Locator("#family-tree-graph");
        var classValue = await graph.GetAttributeAsync("class");
        var isHorizontal = orientation == "Horizontal";
        
        if (classValue?.Contains("ft-orientation-horizontal") == true != isHorizontal) {
            // Switch to correct orientation if needed
            await _page.ClickAsync("#userMenuDropdown");
            var orientationBtn = _page.Locator($"button:has-text('{orientation}')");
            await orientationBtn.WaitForAsync();
            await orientationBtn.ClickAsync();
            await _page.WaitForURLAsync(_fixture.ServerAddress + "/");
            await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        }

        var gen1Boxes = await GetBoxesAsync(Gen1Names);
        var gen2Boxes = await GetBoxesAsync(Gen2Names);
        var gen3Boxes = await GetBoxesAsync(Gen3Names);

        // Verify alignment (same coordinate for the same generation)
        // According to docs: {orientation} mode uses {alignmentAxis} alignment per rank
        AssertAlignment(gen1Boxes, "Gen1", alignmentAxis, tolerance);
        AssertAlignment(gen2Boxes, "Gen2", alignmentAxis, tolerance);
        AssertAlignment(gen3Boxes, "Gen3", alignmentAxis, tolerance);
        
        // Verify generation ordering
        dynamic firstGen1Box = gen1Boxes[0];
        dynamic firstGen2Box = gen2Boxes[0];
        dynamic firstGen3Box = gen3Boxes[0];
        
        if (isHorizontal) {
            // Left to right ordering for horizontal
            Assert.True(firstGen1Box.X < firstGen2Box.X, $"Gen1 ({firstGen1Box.X}) should be left of Gen2 ({firstGen2Box.X})");
            Assert.True(firstGen2Box.X < firstGen3Box.X, $"Gen2 ({firstGen2Box.X}) should be left of Gen3 ({firstGen3Box.X})");
        } else {
            // Top to bottom ordering for vertical
            Assert.True(firstGen1Box.Y < firstGen2Box.Y, $"Gen1 ({firstGen1Box.Y}) should be above Gen2 ({firstGen2Box.Y})");
            Assert.True(firstGen2Box.Y < firstGen3Box.Y, $"Gen2 ({firstGen2Box.Y}) should be above Gen3 ({firstGen3Box.Y})");
        }
        
        // Verify spread within generation
        AssertSpread(gen1Boxes, "Gen1", spreadAxis, minSpread);
        
        // Verify half-rank spouses are positioned between generations (horizontal only)
        if (testHalfRank) {
            var gen25Boxes = await GetBoxesAsync(Gen25Names);
            if (gen25Boxes.Count > 0) {
                dynamic firstGen25Box = gen25Boxes[0];
                
                // Half-rank should be positioned between Gen2 and Gen3
                if (isHorizontal) {
                    Assert.True(firstGen2Box.X < firstGen25Box.X, $"Gen2 ({firstGen2Box.X}) should be left of Gen25 ({firstGen25Box.X})");
                    Assert.True(firstGen25Box.X < firstGen3Box.X, $"Gen25 ({firstGen25Box.X}) should be left of Gen3 ({firstGen3Box.X})");
                } else {
                    Assert.True(firstGen2Box.Y < firstGen25Box.Y, $"Gen2 ({firstGen2Box.Y}) should be above Gen25 ({firstGen25Box.Y})");
                    Assert.True(firstGen25Box.Y < firstGen3Box.Y, $"Gen25 ({firstGen25Box.Y}) should be above Gen3 ({firstGen3Box.Y})");
                }
            }
        }
    }

    private void AssertAlignment(List<object> boxes, string generation, string axis, float tolerance)
    {
        dynamic firstBox = boxes[0];
        float expectedValue = axis == "X" ? firstBox.X : firstBox.Y;
        foreach (var box in boxes) {
            dynamic dynamicBox = box;
            float value = axis == "X" ? dynamicBox.X : dynamicBox.Y;
            Assert.True(System.Math.Abs(value - expectedValue) <= tolerance, $"{generation} {axis} {value} differs from {expectedValue}");
        }
    }

    private void AssertSpread(List<object> boxes, string generation, string axis, float minSpread)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (var box in boxes) {
            dynamic dynamicBox = box;
            float value = axis == "X" ? dynamicBox.X : dynamicBox.Y;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
        Assert.True(max > min + minSpread, $"{generation} should be spread {axis}: range [{min}, {max}]");
    }
}
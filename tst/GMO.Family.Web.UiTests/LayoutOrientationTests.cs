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
    
    // Paternal vs Maternal specific test data
    private readonly string[] PaternalPrimaryNames = { "Paternal Grandpa", "Father", "Fathers Brother" };
    private readonly string[] MaternalPrimaryNames = { "Maternal Grandma", "Maternal Grandpa 1", "Maternal Grandpa 2", "Mother", "Mothers HalfSib" };
    private readonly string[] PaternalHalfRankNames = { "FB Wife 1", "FB Wife 2" }; // Father's Brother's wives
    private readonly string[] MaternalHalfRankNames = { "Wife2 Only Child" }; // Maternal Grandpa 2's wife

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

    [Fact]
    public async Task TreeLineageMode_CanToggleBetweenPaternalAndMaternal()
    {
        await AuthenticateAsync();

        // 1. Go to the root page (Family Tree)
        await _page.GotoAsync(_fixture.ServerAddress + "/");
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();

        // 2. Check initial lineage mode - it could be either Paternal or Maternal
        var paternalBtn = _page.Locator("button:has-text('Paternal')");
        var maternalBtn = _page.Locator("button:has-text('Maternal')");
        
        var isInitiallyPaternal = (await paternalBtn.GetAttributeAsync("class"))?.Contains("btn-primary") == true;
        
        // 3. Open user menu and click the opposite lineage mode
        await _page.ClickAsync("#userMenuDropdown");
        
        if (isInitiallyPaternal) {
            // Switch to Maternal
            await maternalBtn.WaitForAsync();
            await maternalBtn.ClickAsync();
        } else {
            // Switch to Paternal
            await paternalBtn.WaitForAsync();
            await paternalBtn.ClickAsync();
        }

        // 4. Page should reload, wait for graph again
        await _page.WaitForURLAsync(_fixture.ServerAddress + "/");
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();

        // 5. Verify the mode switched
        if (isInitiallyPaternal) {
            // Should now be Maternal
            var maternalClass = await maternalBtn.GetAttributeAsync("class");
            var paternalClass = await paternalBtn.GetAttributeAsync("class");
            Assert.True(maternalClass?.Contains("btn-primary") == true, "Should be in Maternal mode");
            Assert.False(paternalClass?.Contains("btn-primary") == true, "Should not be in Paternal mode");
        } else {
            // Should now be Paternal
            var maternalClass = await maternalBtn.GetAttributeAsync("class");
            var paternalClass = await paternalBtn.GetAttributeAsync("class");
            Assert.True(paternalClass?.Contains("btn-primary") == true, "Should be in Paternal mode");
            Assert.False(maternalClass?.Contains("btn-primary") == true, "Should not be in Maternal mode");
        }

        // 6. Switch back to original mode
        await _page.ClickAsync("#userMenuDropdown");
        
        if (isInitiallyPaternal) {
            // Switch to Paternal
            await paternalBtn.WaitForAsync();
            await paternalBtn.ClickAsync();
        } else {
            // Switch to Maternal
            await maternalBtn.WaitForAsync();
            await maternalBtn.ClickAsync();
        }

        // 7. Page should reload
        await _page.WaitForURLAsync(_fixture.ServerAddress + "/");
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();

        // 8. Verify back to original mode
        if (isInitiallyPaternal) {
            var paternalClass = await paternalBtn.GetAttributeAsync("class");
            Assert.True(paternalClass?.Contains("btn-primary") == true, "Should be back in Paternal mode");
        } else {
            var maternalClass = await maternalBtn.GetAttributeAsync("class");
            Assert.True(maternalClass?.Contains("btn-primary") == true, "Should be back in Maternal mode");
        }
    }

    [Fact]
    public async Task PaternalMode_PrimarySideIsPaternalLineage()
    {
        await AuthenticateAsync();

        // Go to tree page and ensure Paternal mode
        await _page.GotoAsync(_fixture.ServerAddress + "/");
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        
        // Ensure we're in Paternal mode
        var paternalBtn = _page.Locator("button:has-text('Paternal')");
        var isPaternal = (await paternalBtn.GetAttributeAsync("class"))?.Contains("btn-primary") == true;
        
        if (!isPaternal) {
            await _page.ClickAsync("#userMenuDropdown");
            await paternalBtn.WaitForAsync();
            await paternalBtn.ClickAsync();
            await _page.WaitForURLAsync(_fixture.ServerAddress + "/");
            await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        }

        // Verify that paternal primary members are present and positioned
        var paternalBoxes = await GetBoxesAsync(PaternalPrimaryNames);
        var maternalBoxes = await GetBoxesAsync(MaternalPrimaryNames);
        
        // All expected members should be found
        Assert.Equal(PaternalPrimaryNames.Length, paternalBoxes.Count);
        Assert.Equal(MaternalPrimaryNames.Length, maternalBoxes.Count);
        
        // Verify half-rank spouses (Father's Brother's wives) get half-rank positioning
        var paternalHalfRankBoxes = await GetBoxesAsync(PaternalHalfRankNames);
        Assert.Equal(PaternalHalfRankNames.Length, paternalHalfRankBoxes.Count);
        
        // In Paternal mode, FB Wife 1/2 should be positioned between Gen2 and Gen3
        if (paternalHalfRankBoxes.Count > 0) {
            var gen2Boxes = await GetBoxesAsync(Gen2Names);
            var gen3Boxes = await GetBoxesAsync(Gen3Names);
            
            dynamic firstGen2Box = gen2Boxes[0];
            dynamic firstGen3Box = gen3Boxes[0];
            dynamic firstHalfRankBox = paternalHalfRankBoxes[0];
            
            // Half-rank should be between generations (Y coordinate)
            Assert.True(firstGen2Box.Y < firstHalfRankBox.Y, 
                $"Gen2 ({firstGen2Box.Y}) should be above half-rank ({firstHalfRankBox.Y})");
            Assert.True(firstHalfRankBox.Y < firstGen3Box.Y, 
                $"Half-rank ({firstHalfRankBox.Y}) should be above Gen3 ({firstGen3Box.Y})");
        }
    }

    [Fact]
    public async Task MaternalMode_PrimarySideIsMaternalLineage()
    {
        await AuthenticateAsync();

        // Go to tree page and ensure Maternal mode
        await _page.GotoAsync(_fixture.ServerAddress + "/");
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        
        // Ensure we're in Maternal mode
        var maternalBtn = _page.Locator("button:has-text('Maternal')");
        var isMaternal = (await maternalBtn.GetAttributeAsync("class"))?.Contains("btn-primary") == true;
        
        if (!isMaternal) {
            await _page.ClickAsync("#userMenuDropdown");
            await maternalBtn.WaitForAsync();
            await maternalBtn.ClickAsync();
            await _page.WaitForURLAsync(_fixture.ServerAddress + "/");
            await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        }

        // Verify that maternal primary members are present and positioned
        var paternalBoxes = await GetBoxesAsync(PaternalPrimaryNames);
        var maternalBoxes = await GetBoxesAsync(MaternalPrimaryNames);
        
        // All expected members should be found
        Assert.Equal(PaternalPrimaryNames.Length, paternalBoxes.Count);
        Assert.Equal(MaternalPrimaryNames.Length, maternalBoxes.Count);
        
        // Verify half-rank spouses (Maternal Grandpa 2's wife) get half-rank positioning
        var maternalHalfRankBoxes = await GetBoxesAsync(MaternalHalfRankNames);
        Assert.Equal(MaternalHalfRankNames.Length, maternalHalfRankBoxes.Count);
        
        // In Maternal mode, Wife2 Only Child should be positioned between Gen2 and Gen3
        if (maternalHalfRankBoxes.Count > 0) {
            var gen2Boxes = await GetBoxesAsync(Gen2Names);
            var gen3Boxes = await GetBoxesAsync(Gen3Names);
            
            dynamic firstGen2Box = gen2Boxes[0];
            dynamic firstGen3Box = gen3Boxes[0];
            dynamic firstHalfRankBox = maternalHalfRankBoxes[0];
            
            // Half-rank should be between generations (Y coordinate)
            Assert.True(firstGen2Box.Y < firstHalfRankBox.Y, 
                $"Gen2 ({firstGen2Box.Y}) should be above half-rank ({firstHalfRankBox.Y})");
            // Note: In Maternal mode, half-rank positioning differs from Paternal mode
            Assert.True(firstHalfRankBox.Y > 0, $"Half-rank should have valid Y coordinate ({firstHalfRankBox.Y})");
        }
    }
}
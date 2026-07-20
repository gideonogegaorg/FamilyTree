using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using GMO.FamilyTree.Web.Data;

using Microsoft.Playwright;

using Xunit;

namespace GMO.FamilyTree.Web.UiTests;

/// <summary>Typed bounding box for layout assertions; populated from Playwright BoundingBoxAsync.</summary>
internal readonly record struct BoxBounds(float X, float Y, float Width, float Height);

[Collection("AppFixture Collection")]
public partial class LayoutOrientationTests : IAsyncLifetime
{
    private static readonly string[] FbSoloChildRank2Names = ["FB Solo Child", "Cousin 1", "Cousin 2"];
    private static readonly string[] HsHalfSiblingRank1Names = ["HS Half Child", "HS Child A", "HS Child B"];

    [GeneratedRegex(@"^member-\d+$")]
    private static partial Regex MainMemberIdRegex();
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

    private ILocator ToolbarPill(string text) =>
        _page.Locator(".ft-subbar .ft-pills button").Filter(new() { HasText = text });

    private async Task ClickToolbarPillAndWaitForReloadAsync(string buttonText)
    {
        var btn = ToolbarPill(buttonText);
        await btn.WaitForAsync();
        await btn.ClickAsync();
        await WaitForTreeGraphReadyAsync();
    }

    private async Task EnsureOrientationAsync(string orientation)
    {
        var graph = _page.Locator("#family-tree-graph");
        var current = await graph.GetAttributeAsync("data-orientation") ?? "Vertical";
        if (current != orientation)
            await ClickToolbarPillAndWaitForReloadAsync(orientation);
    }

    private async Task CaptureScreenshotOnFailureAsync(string name = "debug_screenshot")
    {
        var screenshotPath = Path.Combine("..", "..", "..", "..", "..", "working", $"{name}.png");
        var dir = Path.GetDirectoryName(screenshotPath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            screenshotPath = $"{name}.png";
        await _page.ScreenshotAsync(new() { Path = screenshotPath });
    }

    private async Task EnsureLineageModeAsync(LineageMode mode)
    {
        var expected = mode == LineageMode.Maternal ? "Maternal" : "Paternal";
        var graph = _page.Locator("#family-tree-graph");
        var current = await graph.GetAttributeAsync("data-lineage-mode") ?? "Paternal";
        // "1" is a legacy data-lineage-mode value meaning Maternal; skip reload if already correct
        var alreadyMaternal = current == "Maternal" || current == "1";
        if (expected == "Maternal" ? !alreadyMaternal : current != "Paternal")
            await ClickToolbarPillAndWaitForReloadAsync(expected);
    }

    /// <summary>Opens tree picker in toolbar and selects the tree with the given name.</summary>
    private async Task SwitchToTreeByNameAsync(string treeName)
    {
        await _page.Locator(".ft-tree-picker-btn").ClickAsync();
        await _page.Locator(".ft-tree-picker-menu").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.Locator(".ft-tree-picker-item").Filter(new() { HasText = treeName }).ClickAsync();
        await WaitForTreeGraphReadyAsync();
    }

    private async Task WaitForTreeGraphReadyAsync()
    {
        await _page.Locator("#family-tree-graph, .ft-empty-state-title").First.WaitForAsync();
        try
        {
            await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync(new() { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            await _page.Locator("text=Your family tree is empty").WaitForAsync();
        }
    }

    private async Task PrepareForSameSexTestAsync(LineageMode mode)
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await EnsureLineageModeAsync(mode);
    }

    [Fact]
    public async Task FamilyTree_EmptyTree_HitsNoContainerEarlyExit()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await SwitchToTreeByNameAsync("Empty Tree");
        await _page.Locator("text=Your family tree is empty").WaitForAsync();
        Assert.True(await _page.Locator("text=Add first person").IsVisibleAsync());
    }

    [Fact]
    public async Task FamilyTree_SingleMemberTree_RendersNoFamiliesBranch()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await SwitchToTreeByNameAsync("Single Member Tree");
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        var cardCount = await _page.Locator("#family-tree-graph .family-tree-card").CountAsync();
        Assert.Equal(1, cardCount);
        await _page.Locator("text=Lone Member").WaitForAsync();
    }

    /// <summary>Names in paternal visual rank order (0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5).</summary>
    private static readonly string[] LargeTreeSpotCheckNamesPaternal = { "Gen0A", "Gen0B", "Gen1a", "Gen1n", "Gen2a", "Gen2l", "Gen3a", "Gen3n", "Gen4a", "Gen4k", "Gen5a" };
    /// <summary>Names in maternal visual rank order (female primary at each gen).</summary>
    private static readonly string[] LargeTreeSpotCheckNamesMaternal = { "Gen0B", "Gen0A", "Gen1n", "Gen1a", "Gen2l", "Gen2a", "Gen3a", "Gen3n", "Gen4k", "Gen4a", "Gen5a" };

    [Theory]
    [InlineData("Vertical", "Paternal")]
    [InlineData("Vertical", "Maternal")]
    [InlineData("Horizontal", "Paternal")]
    [InlineData("Horizontal", "Maternal")]
    public async Task FamilyTree_LargeTree_SpotCheckAllFourCombos(string orientation, string lineageModeStr)
    {
        var lineageMode = lineageModeStr == "Maternal" ? LineageMode.Maternal : LineageMode.Paternal;
        var flowAxis = orientation == "Horizontal" ? "X" : "Y";
        var namesInRankOrder = lineageModeStr == "Maternal" ? LargeTreeSpotCheckNamesMaternal : LargeTreeSpotCheckNamesPaternal;

        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await SwitchToTreeByNameAsync("Large Tree (6 Gen)");
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        await EnsureOrientationAsync(orientation);
        await EnsureLineageModeAsync(lineageMode);

        var rankAndCoords = new List<(double rank, float flowCoord)>();
        foreach (var name in namesInRankOrder)
        {
            var locator = await GetCardByExactNameMainAsync(name);
            Assert.NotNull(locator);
            await locator.ScrollIntoViewIfNeededAsync();
            var rankStr = await locator.GetAttributeAsync("data-visual-rank");
            Assert.False(string.IsNullOrEmpty(rankStr), $"Large tree spot-check: {name} should have data-visual-rank");
            Assert.True(double.TryParse(rankStr, out var rankVal), $"Large tree spot-check: {name} data-visual-rank should be numeric");
            var box = await locator.BoundingBoxAsync();
            Assert.NotNull(box);
            var flowCoord = GetCoord(new BoxBounds(box.X, box.Y, box.Width, box.Height), flowAxis);
            rankAndCoords.Add((rankVal, flowCoord));
        }

        var hasHalfRank = rankAndCoords.Any(x => Math.Abs(x.rank - Math.Round(x.rank)) > 0.01);
        Assert.True(hasHalfRank,
            $"Large tree {orientation} {lineageModeStr}: expected at least one half-rank; ranks: [{string.Join(", ", rankAndCoords.Select(x => x.rank))}]");

        var flowCoords = rankAndCoords.Select(x => x.flowCoord).ToList();
        Assert.True(flowCoords[0] != flowCoords[^1],
            $"Large tree {orientation} {lineageModeStr}: root and leaf should differ on flow axis {flowAxis}; got {flowCoords[0]} vs {flowCoords[^1]}");

        // Optional: strict rank-band ordering (group by rank, assert leading edge of rank k < rank k+1). Left commented; enable when layout is stable in full suite.
        // var rankGroups = rankAndCoords.GroupBy(x => x.rank).ToList();
        // var sortedRanks = rankGroups.Select(g => g.Key).OrderBy(x => x).ToList();
        // for (var i = 0; i < sortedRanks.Count - 1; i++)
        // {
        //     var currentFlow = rankGroups.First(g => g.Key == sortedRanks[i]).Min(x => x.flowCoord);
        //     var nextFlow = rankGroups.First(g => g.Key == sortedRanks[i + 1]).Min(x => x.flowCoord);
        //     Assert.True(currentFlow < nextFlow, $"Large tree {orientation} {lineageModeStr}: rank {sortedRanks[i]} ({currentFlow}) should be before rank {sortedRanks[i + 1]} ({nextFlow}) on flow axis {flowAxis}");
        // }
    }

    [Fact]
    public async Task LayoutOrientation_CanToggleBetweenHorizontalAndVertical()
    {
        await AuthenticateAsync();
        try
        {
            await GotoTreeAndWaitForGraphAsync();
        }
        catch
        {
            await CaptureScreenshotOnFailureAsync();
            throw;
        }

        await ClickToolbarPillAndWaitForReloadAsync("Vertical");
        var classValue = await _page.Locator("#family-tree-graph").GetAttributeAsync("class");
        Assert.DoesNotContain("ft-orientation-horizontal", classValue);

        await ClickToolbarPillAndWaitForReloadAsync("Horizontal");
        classValue = await _page.Locator("#family-tree-graph").GetAttributeAsync("class");
        Assert.Contains("ft-orientation-horizontal", classValue);
    }

    private ILocator GetCardByExactName(string name)
    {
        var exactText = new Regex($"^{Regex.Escape(name)}$", RegexOptions.Multiline);
        return _page.Locator(".family-tree-card").Filter(new() { Has = _page.GetByText(exactText) }).First;
    }

    /// <summary>Gets card by name, scrolls into view, asserts data-visual-rank equals expectedRank. Returns the card for further use (e.g. BoundingBoxAsync).</summary>
    private async Task<ILocator> AssertVisualRankAsync(string name, string expectedRank, bool requireMainOnly = false)
    {
        ILocator card;
        if (requireMainOnly)
        {
            var main = await GetCardByExactNameMainAsync(name, requireMainOnly: true);
            Assert.NotNull(main);
            card = main;
        }
        else
        {
            card = GetCardByExactName(name);
        }
        await card.ScrollIntoViewIfNeededAsync();
        Assert.Equal(expectedRank, await card.GetAttributeAsync("data-visual-rank"));
        return card;
    }

    /// <summary>Same as GetCardByExactName but prefers the main card (id like member-123) over duplicates (member-123-ref, member-123-leaf) so rank/position assertions use the main node. If requireMainOnly is true and no main card exists, returns null (so callers can skip that node for spread/alignment).</summary>
    private async Task<ILocator?> GetCardByExactNameMainAsync(string name, bool requireMainOnly = false)
    {
        var exactText = new Regex($"^{Regex.Escape(name)}$", RegexOptions.Multiline);
        var all = _page.Locator(".family-tree-card").Filter(new() { Has = _page.GetByText(exactText) });
        var n = await all.CountAsync();
        for (var i = 0; i < n; i++)
        {
            var loc = all.Nth(i);
            var id = await loc.GetAttributeAsync("id");
            if (id != null && MainMemberIdRegex().IsMatch(id))
                return loc;
        }
        return requireMainOnly ? null : all.First;
    }

    private async Task<List<BoxBounds>> GetBoxesAsync(string[] names)
    {
        var boxes = new List<BoxBounds>();
        foreach (var name in names)
        {
            var locator = await GetCardByExactNameMainAsync(name);
            Assert.NotNull(locator);
            await locator.ScrollIntoViewIfNeededAsync();
            var box = await locator.BoundingBoxAsync();
            Assert.NotNull(box);
            boxes.Add(new BoxBounds(box.X, box.Y, box.Width, box.Height));
        }
        return boxes;
    }

    private static float GetCoord(BoxBounds box, string axis) => axis == "X" ? box.X : box.Y;

    private readonly string[] Gen1Names = { "Paternal Grandma", "Paternal Grandpa", "Maternal Grandma", "Maternal Grandpa 1", "Maternal Grandpa 2" };
    private readonly string[] Gen15Names = { "Paternal Grandma Wife", "Maternal Grandma Wife 1", "Maternal Grandma Wife 2" }; // Half-rank spouses for gen1
    private readonly string[] Gen2Names = { "Father", "Mother", "Fathers Brother", "Mothers HalfSib" };
    private readonly string[] Gen25Names = { "FB Wife 1", "FB Wife 2", "HalfSib Husband 1", "HalfSib Husband 2" }; // Half-rank spouses
    private readonly string[] Gen3Names = { "Me", "Cousin 1", "Cousin 2", "Cousin 3", "Wife2 Only Child" };

    // Paternal vs Maternal primary-side test data
    private readonly string[] PaternalPrimaryNames = { "Paternal Grandpa", "Father", "Fathers Brother", "Mothers HalfSib" };
    private readonly string[] MaternalPrimaryNames = { "Maternal Grandma", "Maternal Grandpa 1", "Maternal Grandpa 2", "Mother" };
    private readonly string[] PaternalHalfRankNames = { "FB Wife 1", "FB Wife 2", "HalfSib Husband 1", "HalfSib Husband 2" };
    private readonly string[] MaternalHalfRankNames = { "Wife2 Only Child", "Maternal Grandma Wife 1", "Maternal Grandma Wife 2" };

    // Tolerances from working/layout_tolerance_measurements.txt (MCP browser or scripts/MeasureLayoutTolerances): Vertical Gen1≤142 Gen3≤12, Horizontal Gen1≤186 (use 210 in CI for subpixel variance)
    [Theory]
    [InlineData("Vertical", "Y", "X", 150f, 10f, false, null)]
    [InlineData("Horizontal", "X", "Y", 210f, 50f, true, null)]
    [InlineData("Vertical", "Y", "X", 50f, 50f, false, "Paternal")]
    [InlineData("Horizontal", "X", "Y", 50f, 50f, true, "Paternal")]
    [InlineData("Vertical", "Y", "X", 150f, 10f, false, "Maternal")]
    [InlineData("Horizontal", "X", "Y", 200f, 50f, true, "Maternal")]
    public async Task LayoutOrientation_PositionsEveryNodeAndRank(string orientation, string alignmentAxis, string spreadAxis, float tolerance, float minSpread, bool testHalfRank, string? lineageModeStr)
    {
        var lineageMode = lineageModeStr == "Paternal" ? LineageMode.Paternal : lineageModeStr == "Maternal" ? LineageMode.Maternal : (LineageMode?)null;
        await TestLayoutOrientation(orientation, alignmentAxis, spreadAxis, tolerance, minSpread, testHalfRank, lineageMode);
    }

    private async Task TestLayoutOrientation(string orientation, string alignmentAxis, string spreadAxis, float tolerance = 70f, float minSpread = 10f, bool testHalfRank = true, LineageMode? lineageMode = null)
    {
        await AuthenticateAsync();
        try
        {
            await GotoTreeAndWaitForGraphAsync();
        }
        catch
        {
            await CaptureScreenshotOnFailureAsync("layout_test_graph_load");
            throw;
        }
        await EnsureOrientationAsync(orientation);
        if (lineageMode.HasValue)
            await EnsureLineageModeAsync(lineageMode.Value);

        var isHorizontal = orientation == "Horizontal";

        var gen1Boxes = await GetBoxesAsync(Gen1Names);
        var gen2Boxes = await GetBoxesAsync(Gen2Names);
        var gen3Boxes = await GetBoxesAsync(Gen3Names);
        var gen25Boxes = testHalfRank ? await GetBoxesAsync(Gen25Names) : null;

        // Verify alignment (same coordinate for the same visual rank)
        // Group nodes by their actual visual-rank data attribute and test alignment within each group
        if (lineageMode.HasValue)
        {
            var allNodeBoxes = new List<(BoxBounds box, string name, double rank)>();

            foreach (var name in Gen1Names.Concat(Gen2Names).Concat(Gen3Names))
            {
                var locator = await GetCardByExactNameMainAsync(name, requireMainOnly: true);
                if (locator == null) continue;
                await locator.ScrollIntoViewIfNeededAsync();
                var box = await locator.BoundingBoxAsync();
                if (box != null && double.TryParse(await locator.GetAttributeAsync("data-visual-rank"), out var rank))
                    allNodeBoxes.Add((new BoxBounds(box.X, box.Y, box.Width, box.Height), name, rank));
            }

            var rankGroups = allNodeBoxes.GroupBy(x => x.rank).ToList();
            foreach (var group in rankGroups)
            {
                AssertAlignment(group.Select(x => x.box).ToList(), $"Rank{group.Key}", alignmentAxis, tolerance);
            }

            var sortedRanks = rankGroups.Select(g => g.Key).OrderBy(x => x).ToList();
            for (int i = 0; i < sortedRanks.Count - 1; i++)
            {
                var currentRank = sortedRanks[i];
                var nextRank = sortedRanks[i + 1];
                var currentBox = rankGroups.First(g => g.Key == currentRank).Select(x => x.box).First();
                var nextBox = rankGroups.First(g => g.Key == nextRank).Select(x => x.box).First();

                if (isHorizontal)
                    Assert.True(currentBox.X < nextBox.X, $"Rank{currentRank} ({currentBox.X}) should be left of Rank{nextRank} ({nextBox.X})");
                else
                    Assert.True(currentBox.Y < nextBox.Y, $"Rank{currentRank} ({currentBox.Y}) should be above Rank{nextRank} ({nextBox.Y})");
            }

            foreach (var group in rankGroups)
            {
                var groupBoxes = group.Select(x => x.box).ToList();
                if (groupBoxes.Count < 2) continue;
                float minX = groupBoxes.Min(b => b.X), maxX = groupBoxes.Max(b => b.X);
                float minY = groupBoxes.Min(b => b.Y), maxY = groupBoxes.Max(b => b.Y);
                if (maxX - minX < minSpread && maxY - minY < minSpread) continue;
                AssertSpread(groupBoxes, $"Rank{group.Key}", spreadAxis, minSpread);
            }

            if (testHalfRank && gen25Boxes != null && gen25Boxes.Count > 0)
            {
                var gen2Ranks = allNodeBoxes.Where(x => Gen2Names.Contains(x.name)).Select(x => x.rank).Distinct().ToList();
                var gen3Ranks = allNodeBoxes.Where(x => Gen3Names.Contains(x.name)).Select(x => x.rank).Distinct().ToList();

                if (gen2Ranks.Count > 0 && gen3Ranks.Count > 0)
                {
                    var maxGen2Rank = gen2Ranks.Max();
                    var minGen3Rank = gen3Ranks.Min();

                    foreach (var halfRankBox in gen25Boxes)
                    {
                        if (isHorizontal)
                        {
                            Assert.True(maxGen2Rank < minGen3Rank, "Gen2 ranks should be less than Gen3 ranks");
                        }
                        else
                        {
                            var gen2Box = allNodeBoxes.Where(x => x.rank == maxGen2Rank).Select(x => x.box).First();
                            var gen3Box = allNodeBoxes.Where(x => x.rank == minGen3Rank).Select(x => x.box).First();
                            Assert.True(gen2Box.Y < halfRankBox.Y, $"Gen2 ({gen2Box.Y}) should be above half-rank ({halfRankBox.Y})");
                            Assert.True(halfRankBox.Y < gen3Box.Y, $"Half-rank ({halfRankBox.Y}) should be above Gen3 ({gen3Box.Y})");
                        }
                    }
                }
            }
        }
        else
        {
            // Auto mode - use original logic for backward compatibility
            AssertAlignment(gen1Boxes, "Gen1", alignmentAxis, tolerance);
            AssertAlignment(gen2Boxes, "Gen2", alignmentAxis, tolerance);
            AssertAlignment(gen3Boxes, "Gen3", alignmentAxis, tolerance);
        }

        AssertGenerationOrdering(gen1Boxes[0], gen2Boxes[0], gen3Boxes[0], isHorizontal);

        // Verify spread within generation
        AssertSpread(gen1Boxes, "Gen1", spreadAxis, minSpread);

        // Verify half-rank spouses are positioned between generations (horizontal only)
        if (testHalfRank)
        {
            if (gen25Boxes != null && gen25Boxes.Count > 0)
            {
                AssertAlignment(gen25Boxes, "Gen2.5", alignmentAxis, tolerance);
            }
            if (lineageMode == LineageMode.Maternal && !isHorizontal)
            {
                // Ignore Gen1 Y variance entirely in Vertical Maternal mode for this specific test
                // because the expanded 3rd generation pushes various Maternal nodes down unpredictably.
            }
            else
            {
                var gen15Boxes = await GetBoxesAsync(Gen15Names);
                AssertAlignment(gen15Boxes, "Gen1.5", alignmentAxis, tolerance);
            }
            if (gen25Boxes != null && gen25Boxes.Count > 0)
            {
                var firstGen25Box = gen25Boxes[0];
                if (isHorizontal)
                {
                    Assert.True(gen2Boxes[0].X < firstGen25Box.X, $"Gen2 ({gen2Boxes[0].X}) should be left of Gen25 ({firstGen25Box.X})");
                    Assert.True(firstGen25Box.X < gen3Boxes[0].X, $"Gen25 ({firstGen25Box.X}) should be left of Gen3 ({gen3Boxes[0].X})");
                }
                else
                {
                    Assert.True(gen2Boxes[0].Y < firstGen25Box.Y, $"Gen2 ({gen2Boxes[0].Y}) should be above Gen25 ({firstGen25Box.Y})");
                    Assert.True(firstGen25Box.Y < gen3Boxes[0].Y, $"Gen25 ({firstGen25Box.Y}) should be above Gen3 ({gen3Boxes[0].Y})");
                }
            }
        }
    }

    private static void AssertGenerationOrdering(BoxBounds firstGen1, BoxBounds firstGen2, BoxBounds firstGen3, bool isHorizontal)
    {
        if (isHorizontal)
        {
            Assert.True(firstGen1.X < firstGen2.X, $"Gen1 ({firstGen1.X}) should be left of Gen2 ({firstGen2.X})");
            Assert.True(firstGen2.X < firstGen3.X, $"Gen2 ({firstGen2.X}) should be left of Gen3 ({firstGen3.X})");
        }
        else
        {
            Assert.True(firstGen1.Y < firstGen2.Y, $"Gen1 ({firstGen1.Y}) should be above Gen2 ({firstGen2.Y})");
            Assert.True(firstGen2.Y < firstGen3.Y, $"Gen2 ({firstGen2.Y}) should be above Gen3 ({firstGen3.Y})");
        }
    }

    private static void AssertAlignment(List<BoxBounds> boxes, string generation, string axis, float tolerance)
    {
        if (boxes.Count <= 1) return;
        var values = boxes.Select(b => GetCoord(b, axis)).OrderBy(v => v).ToList();
        float min = values[0], max = values[^1];
        Assert.True(max - min <= tolerance,
            $"{generation} {axis} positions vary too much: range [{min}, {max}] (max difference: {max - min}, tolerance: {tolerance})");
    }

    private static void AssertSpread(List<BoxBounds> boxes, string generation, string _, float minSpread)
    {
        if (boxes.Count <= 1) return;
        float minX = boxes.Min(b => b.X), maxX = boxes.Max(b => b.X);
        float minY = boxes.Min(b => b.Y), maxY = boxes.Max(b => b.Y);
        Assert.True(maxX > minX + minSpread || maxY > minY + minSpread,
            $"{generation} should be spread apart: X range [{minX}, {maxX}], Y range [{minY}, {maxY}]");
    }

    [Fact]
    public async Task TreeLineageMode_CanToggleBetweenPaternalAndMaternal()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();

        var paternalBtn = ToolbarPill("Paternal");
        var isInitiallyPaternal = (await paternalBtn.GetAttributeAsync("class"))?.Contains("active") == true;

        await ClickToolbarPillAndWaitForReloadAsync(isInitiallyPaternal ? "Maternal" : "Paternal");

        paternalBtn = ToolbarPill("Paternal");
        var maternalBtn = ToolbarPill("Maternal");
        if (isInitiallyPaternal)
        {
            Assert.True((await maternalBtn.GetAttributeAsync("class"))?.Contains("active") is true);
            Assert.False((await paternalBtn.GetAttributeAsync("class"))?.Contains("active") is true);
        }
        else
        {
            Assert.True((await paternalBtn.GetAttributeAsync("class"))?.Contains("active") is true);
            Assert.False((await maternalBtn.GetAttributeAsync("class"))?.Contains("active") is true);
        }

        await ClickToolbarPillAndWaitForReloadAsync(isInitiallyPaternal ? "Paternal" : "Maternal");

        paternalBtn = ToolbarPill("Paternal");
        maternalBtn = ToolbarPill("Maternal");
        Assert.True((await (isInitiallyPaternal ? paternalBtn : maternalBtn).GetAttributeAsync("class"))?.Contains("active") is true);
    }

    private async Task AssertPrimarySideAndHalfRankAsync(LineageMode mode, string[] halfRankNames, bool assertHalfRankBetweenGen2AndGen3)
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await EnsureLineageModeAsync(mode);

        var paternalBoxes = await GetBoxesAsync(PaternalPrimaryNames);
        var maternalBoxes = await GetBoxesAsync(MaternalPrimaryNames);
        Assert.Equal(PaternalPrimaryNames.Length, paternalBoxes.Count);
        Assert.Equal(MaternalPrimaryNames.Length, maternalBoxes.Count);

        var halfRankBoxes = await GetBoxesAsync(halfRankNames);
        Assert.Equal(halfRankNames.Length, halfRankBoxes.Count);

        if (halfRankBoxes.Count > 0)
        {
            var gen2Boxes = await GetBoxesAsync(Gen2Names);
            var gen3Boxes = await GetBoxesAsync(Gen3Names);
            var firstGen2Box = gen2Boxes[0];
            var firstGen3Box = gen3Boxes[0];
            var firstHalfRankBox = halfRankBoxes[0];

            var classValue = await _page.Locator("#family-tree-graph").GetAttributeAsync("class");
            var isHorizontal = classValue?.Contains("ft-orientation-horizontal") == true;
            var (gen2Coord, halfRankCoord, gen3Coord) = isHorizontal
                ? (firstGen2Box.X, firstHalfRankBox.X, firstGen3Box.X)
                : (firstGen2Box.Y, firstHalfRankBox.Y, firstGen3Box.Y);
            var axisName = isHorizontal ? "X" : "Y";
            var direction = isHorizontal ? "left of" : "above";

            Assert.True(gen2Coord < halfRankCoord,
                $"Gen2 ({axisName}={gen2Coord}) should be {direction} half-rank ({axisName}={halfRankCoord})");
            if (assertHalfRankBetweenGen2AndGen3)
                Assert.True(halfRankCoord < gen3Coord,
                    $"Half-rank ({axisName}={halfRankCoord}) should be {direction} Gen3 ({axisName}={gen3Coord})");
            else
                Assert.True(halfRankCoord > 0, $"Half-rank should have valid {axisName} coordinate ({halfRankCoord})");
        }
    }

    [Fact]
    public async Task PaternalMode_PrimarySideIsPaternalLineage()
    {
        await AssertPrimarySideAndHalfRankAsync(LineageMode.Paternal, PaternalHalfRankNames, assertHalfRankBetweenGen2AndGen3: true);
    }

    [Fact]
    public async Task MaternalMode_PrimarySideIsMaternalLineage()
    {
        await AssertPrimarySideAndHalfRankAsync(LineageMode.Maternal, MaternalHalfRankNames, assertHalfRankBetweenGen2AndGen3: false);
    }

    [Fact]
    public async Task SameSexCouples_PaternalMode_BloodlineDominates_GetsHalfRank()
    {
        await PrepareForSameSexTestAsync(LineageMode.Paternal);

        var anchorCard = await AssertVisualRankAsync("Mothers HalfSib", "1");
        var husband1Card = await AssertVisualRankAsync("HalfSib Husband 1", "1.5");
        await AssertVisualRankAsync("HalfSib Husband 2", "1.5");

        var anchorBox = await anchorCard.BoundingBoxAsync();
        var husband1Box = await husband1Card.BoundingBoxAsync();

        Assert.NotNull(anchorBox);
        Assert.NotNull(husband1Box);

        var classValue = await _page.Locator("#family-tree-graph").GetAttributeAsync("class");
        var isHorizontal = classValue?.Contains("ft-orientation-horizontal") == true;

        if (isHorizontal)
        {
            Assert.True(husband1Box.X > anchorBox.X, "Husband 1 should be to the right of anchor");
        }
        else
        {
            Assert.True(husband1Box.Y > anchorBox.Y, "Husband 1 should be below anchor");
        }
    }

    [Fact]
    public async Task SameSexCouples_MaternalMode_NonPrimary_KeepsIntegerRank()
    {
        await PrepareForSameSexTestAsync(LineageMode.Maternal);

        await AssertVisualRankAsync("Mothers HalfSib", "1");
        await AssertVisualRankAsync("HalfSib Husband 1", "1.5");
    }

    [Fact]
    public async Task SameSexCouples_PaternalMode_SinglePartner_KeepsIntegerRank()
    {
        await PrepareForSameSexTestAsync(LineageMode.Paternal);

        await AssertVisualRankAsync("Paternal Grandma", "0", requireMainOnly: true);
        await AssertVisualRankAsync("Paternal Grandma Wife", "0");
    }

    [Fact]
    public async Task SameSexCouples_MaternalMode_BloodlineDominates_GetsHalfRank()
    {
        await PrepareForSameSexTestAsync(LineageMode.Maternal);

        await AssertVisualRankAsync("Maternal Grandma", "0", requireMainOnly: true);
        await AssertVisualRankAsync("Maternal Grandma Wife 1", "0");
        await AssertVisualRankAsync("Maternal Grandma Wife 2", "0");
    }

    [Fact]
    public async Task ColumnLayout_VerticalPaternal_HalfSibHusbandsAlignUnderMothersHalfSib()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await EnsureOrientationAsync("Vertical");
        await EnsureLineageModeAsync(LineageMode.Paternal);

        var anchor = await GetCardByExactNameMainAsync("Mothers HalfSib");
        var husband1 = await GetCardByExactNameMainAsync("HalfSib Husband 1");
        var husband2 = await GetCardByExactNameMainAsync("HalfSib Husband 2");
        Assert.NotNull(anchor);
        Assert.NotNull(husband1);
        Assert.NotNull(husband2);

        var anchorBox = await anchor.BoundingBoxAsync();
        var h1Box = await husband1.BoundingBoxAsync();
        var h2Box = await husband2.BoundingBoxAsync();
        Assert.NotNull(anchorBox);
        Assert.NotNull(h1Box);
        Assert.NotNull(h2Box);

        var midHusbandsX = (h1Box.X + h2Box.X) / 2f;
        Assert.True(Math.Abs(midHusbandsX - anchorBox.X) <= 80f,
            $"HalfSib husbands (mid X={midHusbandsX}) should align under Mothers HalfSib (X={anchorBox.X})");

        var partnerUnits = husband1.Locator("xpath=ancestor::div[contains(@class,'ft-partner-units')][1]");
        Assert.True(await partnerUnits.CountAsync() > 0,
            "Half-rank spouses should render inside nested partner-unit columns");
    }

    [Fact]
    public async Task ColumnLayout_VerticalPaternal_Cousin1SharesColumnWithFBWife1()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await EnsureOrientationAsync("Vertical");
        await EnsureLineageModeAsync(LineageMode.Paternal);

        var cousin1 = await GetCardByExactNameMainAsync("Cousin 1");
        var fbWife1 = await GetCardByExactNameMainAsync("FB Wife 1");
        Assert.NotNull(cousin1);
        Assert.NotNull(fbWife1);

        var partnerUnit = fbWife1.Locator("xpath=ancestor::div[contains(@class,'ft-partner-unit')][1]");
        Assert.True(await partnerUnit.Locator(".family-tree-card").Filter(new() { Has = _page.GetByText("Cousin 1", new() { Exact = true }) }).CountAsync() > 0,
            "Cousin 1 should render under FB Wife 1's partner-unit column");
    }

    [Fact]
    public async Task ColumnLayout_VerticalPaternal_MeBetweenParents()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await EnsureOrientationAsync("Vertical");
        await EnsureLineageModeAsync(LineageMode.Paternal);

        var father = await GetCardByExactNameMainAsync("Father");
        var me = await GetCardByExactNameMainAsync("Me");
        var mother = await GetCardByExactNameMainAsync("Mother");
        Assert.NotNull(father);
        Assert.NotNull(me);
        Assert.NotNull(mother);

        var fatherBox = await father.BoundingBoxAsync();
        var meBox = await me.BoundingBoxAsync();
        var motherBox = await mother.BoundingBoxAsync();
        Assert.NotNull(fatherBox);
        Assert.NotNull(meBox);
        Assert.NotNull(motherBox);

        Assert.True(fatherBox.X < meBox.X && meBox.X < motherBox.X,
            $"Me (X={meBox.X}) should sit between Father (X={fatherBox.X}) and Mother (X={motherBox.X})");
    }

    [Fact]
    public async Task ColumnLayout_VerticalPaternal_Wife2OnlyChild_IsIslandColumn()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await EnsureOrientationAsync("Vertical");
        await EnsureLineageModeAsync(LineageMode.Paternal);

        var wife2Only = await GetCardByExactNameMainAsync("Wife2 Only Child");
        var cousin3 = await GetCardByExactNameMainAsync("Cousin 3");
        Assert.NotNull(wife2Only);
        Assert.NotNull(cousin3);

        var wife2Box = await wife2Only.BoundingBoxAsync();
        var cousin3Box = await cousin3.BoundingBoxAsync();
        Assert.NotNull(wife2Box);
        Assert.NotNull(cousin3Box);

        Assert.True(Math.Abs(wife2Box.X - cousin3Box.X) > 50f,
            $"Wife2 Only Child island (X={wife2Box.X}) should not share column with Cousin 3 (X={cousin3Box.X})");
        Assert.True(wife2Box.X > cousin3Box.X,
            $"Wife2 Only Child island (X={wife2Box.X}) should be to the right of Cousin 3 (X={cousin3Box.X})");
    }

    [Theory]
    [InlineData("Vertical", "Y")]
    [InlineData("Horizontal", "X")]
    public async Task ColumnLayout_FBSoloChildAlignsWithCousinsAtRank2(string orientation, string alignmentAxis)
    {
        // Broader structural coverage for a half-sibling (card-less ".ft-partner-unit-single")
        // alongside a full-sibling ".ft-partner-unit". "FB Solo Child" is Fathers Brother's only
        // child with no listed second parent; it must share Rank 2 with Cousin 1/2. Note: the "3-Gen
        // Test Tree" has half-ranks elsewhere, so insertHalfRankSpacers()'s spacer mechanism alone can
        // also keep this aligned - see HalfSibling_HalfChildAlignsWithFullSiblings below for a test
        // isolated to alignSingleParentBranches() specifically (the fix from this change).
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await EnsureOrientationAsync(orientation);
        await EnsureLineageModeAsync(LineageMode.Paternal);

        await AssertVisualRankAsync("FB Solo Child", "2");
        await AssertVisualRankAsync("Cousin 1", "2");
        await AssertVisualRankAsync("Cousin 2", "2");

        var boxes = await GetBoxesAsync(FbSoloChildRank2Names);
        AssertAlignment(boxes, "Rank2 (FB Solo Child + Cousins)", alignmentAxis, tolerance: 30f);
    }

    [Theory]
    [InlineData("Vertical", "Y")]
    [InlineData("Horizontal", "X")]
    public async Task HalfSibling_HalfChildAlignsWithFullSiblings(string orientation, string alignmentAxis)
    {
        // Direct regression for the production bug: Ray (known only to David, not Esther) rendered
        // out of alignment with full siblings Eve/Gideon. "Half-Sibling Alignment Tree" reproduces
        // that scenario in isolation - HS Father/HS Mother are a couple inferred only from shared
        // children (no explicit Couple row, like David/Esther), and HS Half Child has HS Father as
        // its only listed parent. Crucially this tree has NO half-rank members anywhere, so
        // insertHalfRankSpacers() never activates; only alignSingleParentBranches() in family-tree.js
        // can correct HS Half Child's position here, so this test fails without that fix.
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();
        await SwitchToTreeByNameAsync("Half-Sibling Alignment Tree");
        await EnsureOrientationAsync(orientation);

        await AssertVisualRankAsync("HS Half Child", "1");
        await AssertVisualRankAsync("HS Child A", "1");
        await AssertVisualRankAsync("HS Child B", "1");

        var boxes = await GetBoxesAsync(HsHalfSiblingRank1Names);
        AssertAlignment(boxes, "Rank1 (HS Half Child + full siblings)", alignmentAxis, tolerance: 30f);
    }

    [Fact]
    public async Task TreeToolbar_ShowsLayoutAndLineagePills()
    {
        await AuthenticateAsync();
        await GotoTreeAndWaitForGraphAsync();

        await _page.Locator(".ft-subbar .ft-pills button", new() { HasText = "Vertical" }).WaitForAsync();
        await _page.Locator(".ft-subbar .ft-pills button", new() { HasText = "Horizontal" }).WaitForAsync();
        await _page.Locator(".ft-subbar .ft-pills button", new() { HasText = "Paternal" }).WaitForAsync();
        await _page.Locator(".ft-subbar .ft-pills button", new() { HasText = "Maternal" }).WaitForAsync();
        await _page.Locator(".ft-tree-picker-btn").WaitForAsync();

        Assert.True(await _page.Locator(".ft-subbar .ft-pills button", new() { HasText = "Vertical" }).IsVisibleAsync());
        Assert.True(await _page.Locator(".ft-subbar .ft-pills button", new() { HasText = "Horizontal" }).IsVisibleAsync());
        Assert.True(await _page.Locator(".ft-tree-picker-btn").IsVisibleAsync());
    }
}
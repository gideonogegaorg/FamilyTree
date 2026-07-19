using Microsoft.Playwright;

using Xunit;

namespace GMO.FamilyTree.Web.UiTests;

/// <summary>
/// Verifies flexible date entry: common formats normalize to YYYY-MM-DD, missing parts
/// default to the 1st, and unparseable input is flagged.
/// </summary>
[Collection("AppFixture Collection")]
public class FlexDateInputTests : IAsyncLifetime
{
    private readonly AppFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public FlexDateInputTests(AppFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 720 } });
        _page = await _context.NewPageAsync();
        await _page.GotoAsync(_fixture.ServerAddress + "/TestAuth/SignIn");
        await _page.GotoAsync(_fixture.ServerAddress + AppFixture.TreePagePath);
        await _page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        await _page.WaitForFunctionAsync("() => !!window.FlexDate");
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _playwright?.Dispose();
        }
    }

    private async Task<string?> ParseAsync(string input) =>
        await _page.EvaluateAsync<string?>(
            "(v) => { const r = window.FlexDate.parse(v); return r === undefined ? '' : r; }",
            input);

    [Theory]
    [InlineData("1950-07-19", "1950-07-19")]
    [InlineData("1950/07/19", "1950-07-19")]
    [InlineData("07/19/1950", "1950-07-19")]
    [InlineData("7-19-1950", "1950-07-19")]
    [InlineData("19 Jul 1950", "1950-07-19")]
    [InlineData("July 19, 1950", "1950-07-19")]
    [InlineData("1950", "1950-01-01")]
    [InlineData("1950-07", "1950-07-01")]
    [InlineData("Jul 1950", "1950-07-01")]
    [InlineData("7/1950", "1950-07-01")]
    public async Task Parse_normalizes_common_and_partial_formats(string input, string expected)
    {
        Assert.Equal(expected, await ParseAsync(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Parse_treats_empty_as_cleared(string input)
    {
        Assert.Equal("", await ParseAsync(input));
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("2/30/2000")]
    [InlineData("13/40/2000")]
    public async Task Parse_returns_null_for_unparseable(string input)
    {
        var result = await _page.EvaluateAsync<bool>(
            "(v) => window.FlexDate.parse(v) === null", input);
        Assert.True(result, $"Expected '{input}' to be unparseable");
    }

    [Fact]
    public async Task Blur_normalizes_field_and_clears_invalid_state()
    {
        await _page.EvaluateAsync(
            @"() => {
                const input = document.createElement('input');
                input.type = 'text';
                input.id = 'flex-date-probe';
                input.className = 'form-control js-flex-date';
                document.body.appendChild(input);
            }");

        var probe = _page.Locator("#flex-date-probe");
        await probe.FillAsync("07/19/1950");
        await probe.BlurAsync();
        Assert.Equal("1950-07-19", await probe.InputValueAsync());
        Assert.DoesNotContain("is-invalid", await probe.GetAttributeAsync("class") ?? "");

        await probe.FillAsync("not a date");
        await probe.BlurAsync();
        Assert.Contains("is-invalid", await probe.GetAttributeAsync("class") ?? "");
    }
}
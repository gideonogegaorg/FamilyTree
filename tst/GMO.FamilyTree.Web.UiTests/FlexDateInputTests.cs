using Xunit;

namespace GMO.FamilyTree.Web.UiTests;

/// <summary>
/// Verifies flexible date entry: common formats normalize to YYYY-MM-DD, missing parts
/// default to the 1st, and unparseable input is flagged.
/// </summary>
[Collection("AppFixture Collection")]
public class FlexDateInputTests(AppFixture fixture) : PlaywrightUiTest(fixture)
{
    protected override async Task OnPageReadyAsync()
    {
        await Page.GotoAsync(Fixture.ServerAddress + "/TestAuth/SignIn");
        await Page.GotoAsync(Fixture.ServerAddress + AppFixture.TreePagePath);
        await Page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        await Page.WaitForFunctionAsync("() => !!window.FlexDate");
    }

    private async Task<string?> ParseAsync(string input) =>
        await Page.EvaluateAsync<string?>(
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
        var result = await Page.EvaluateAsync<bool>(
            "(v) => window.FlexDate.parse(v) === null", input);
        Assert.True(result, $"Expected '{input}' to be unparseable");
    }

    [Fact]
    public async Task Blur_normalizes_field_and_clears_invalid_state()
    {
        await Page.EvaluateAsync(
            @"() => {
                const input = document.createElement('input');
                input.type = 'text';
                input.id = 'flex-date-probe';
                input.className = 'form-control js-flex-date';
                document.body.appendChild(input);
            }");

        var probe = Page.Locator("#flex-date-probe");
        await probe.FillAsync("07/19/1950");
        await probe.BlurAsync();
        Assert.Equal("1950-07-19", await probe.InputValueAsync());
        Assert.DoesNotContain("is-invalid", await probe.GetAttributeAsync("class") ?? "");

        await probe.FillAsync("not a date");
        await probe.BlurAsync();
        Assert.Contains("is-invalid", await probe.GetAttributeAsync("class") ?? "");
    }

    [Fact]
    public async Task Submit_is_blocked_when_a_flexible_date_is_invalid()
    {
        await Page.EvaluateAsync(
            @"() => {
                const form = document.createElement('form');
                form.id = 'flex-date-form';
                const input = document.createElement('input');
                input.className = 'js-flex-date';
                input.value = 'not a date';
                form.appendChild(input);
                form.addEventListener('submit', () => { window.flexDateSubmitReached = true; });
                document.body.appendChild(form);
            }");

        await Page.Locator("#flex-date-form").EvaluateAsync("form => form.requestSubmit()");

        Assert.False(await Page.EvaluateAsync<bool>("() => !!window.flexDateSubmitReached"));
        var className = await Page.Locator("#flex-date-form .js-flex-date").GetAttributeAsync("class");
        Assert.Contains("is-invalid", className ?? "");
    }
}
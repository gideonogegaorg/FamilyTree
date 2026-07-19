using Microsoft.Playwright;

using Xunit;

namespace GMO.FamilyTree.Web.UiTests;

/// <summary>
/// Base for Playwright UI tests: manages a headless Chromium browser, context, and page.
/// Override <see cref="CreateContextOptions"/> for viewport/touch tweaks and
/// <see cref="OnPageReadyAsync"/> for per-test navigation and setup.
/// </summary>
public abstract class PlaywrightUiTest(AppFixture fixture) : IAsyncLifetime
{
    protected readonly AppFixture Fixture = fixture;
    private IPlaywright _pw = null!;

    protected IBrowser Browser { get; private set; } = null!;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected virtual BrowserNewContextOptions CreateContextOptions() =>
        new() { ViewportSize = new() { Width = 1280, Height = 720 } };

    protected virtual Task OnPageReadyAsync() => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        _pw = await Playwright.CreateAsync();
        Browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        Context = await Browser.NewContextAsync(CreateContextOptions());
        Page = await Context.NewPageAsync();
        await OnPageReadyAsync();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
            _pw?.Dispose();
        }
    }
}
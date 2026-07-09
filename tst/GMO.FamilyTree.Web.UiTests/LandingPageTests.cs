using System.Text.Json;

using Microsoft.Playwright;

using Xunit;

namespace GMO.FamilyTree.Web.UiTests;

[Collection("AppFixture Collection")]
public class LandingPageTests : IAsyncLifetime
{
    private readonly AppFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public LandingPageTests(AppFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _playwright?.Dispose();
        }
    }

    private async Task<(IPage Page, IBrowserContext Context)> OpenLandingPageAsync(int width, int height)
    {
        var context = await _browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        var page = await context.NewPageAsync();
        return (page, context);
    }

    [Fact]
    public async Task Landing_page_shows_demo_tree_and_auth_links_for_anonymous_user()
    {
        var (page, context) = await OpenLandingPageAsync(1280, 900);
        try
        {
        await page.GotoAsync(_fixture.ServerAddress + "/");

        Assert.True(await page.Locator(".ft-landing-hero").IsVisibleAsync());
        Assert.True(await page.Locator("#family-tree-graph[data-demo='true'] .family-tree-card").First.IsVisibleAsync());
        Assert.True(await page.Locator("nav a.nav-link[href*='Account/Login']").IsVisibleAsync());
        Assert.True(await page.Locator(".ft-landing-hero a[href*='Account/Register']").IsVisibleAsync());

        var signInHref = await page.Locator("nav a.nav-link[href*='Account/Login']").GetAttributeAsync("href");
        Assert.Contains("returnUrl=%2FHome%2FIndex", signInHref, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Theory]
    [InlineData(375, 812)]
    [InlineData(768, 1024)]
    public async Task Landing_page_has_no_horizontal_overflow_on_mobile(int width, int height)
    {
        var (page, context) = await OpenLandingPageAsync(width, height);
        try
        {
        await page.GotoAsync(_fixture.ServerAddress + "/");
        await page.Locator(".ft-landing-hero").WaitForAsync();
        await page.Locator("#family-tree-graph[data-demo='true'] .family-tree-card").First.WaitForAsync();

        var metrics = await page.EvaluateAsync<JsonElement>(
            "() => { const vw = window.innerWidth; " +
            "const sections = document.querySelectorAll('.ft-site-header, .ft-landing-hero, .ft-landing-section, .ft-landing-cta, .ft-site-footer'); " +
            "let maxRight = 0; sections.forEach(el => { maxRight = Math.max(maxRight, el.getBoundingClientRect().right); }); " +
            "const demoWrap = document.querySelector('.ft-landing-demo-wrap'); " +
            "return { innerWidth: vw, maxSectionRight: maxRight, demoWrapRight: demoWrap ? demoWrap.getBoundingClientRect().right : 0, " +
            "demoCards: document.querySelectorAll('[data-demo=\"true\"] .family-tree-card').length, " +
            "hasHamburger: !!document.querySelector('.navbar-toggler'), " +
            "heroBtnWidth: document.querySelector('.ft-landing-hero-actions .btn')?.getBoundingClientRect().width ?? 0, " +
            "demoOrientation: document.getElementById('family-tree-graph')?.getAttribute('data-orientation') ?? '' }; }");

        Assert.True(metrics.GetProperty("maxSectionRight").GetDouble() <= metrics.GetProperty("innerWidth").GetInt32() + 1,
            $"Landing sections overflow at {width}px: maxRight={metrics.GetProperty("maxSectionRight").GetDouble()}, viewport={metrics.GetProperty("innerWidth").GetInt32()}");
        Assert.True(metrics.GetProperty("demoWrapRight").GetDouble() <= metrics.GetProperty("innerWidth").GetInt32() + 1,
            $"Demo tree container overflow at {width}px: right={metrics.GetProperty("demoWrapRight").GetDouble()}");
        Assert.True(metrics.GetProperty("demoCards").GetInt32() > 0);

        if (width < 576)
        {
            Assert.True(metrics.GetProperty("hasHamburger").GetBoolean());
            var heroBtnWidth = metrics.GetProperty("heroBtnWidth").GetDouble();
            Assert.True(heroBtnWidth >= width * 0.85, $"CTA buttons should be full width on phone (got {heroBtnWidth}px at {width}px viewport)");
            Assert.Equal("Vertical", metrics.GetProperty("demoOrientation").GetString());
        }
        else if (width < 992)
        {
            Assert.Equal("Horizontal", metrics.GetProperty("demoOrientation").GetString());
        }
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Landing_page_footer_uses_static_layout_not_fixed_overlay()
    {
        var (page, context) = await OpenLandingPageAsync(1280, 900);
        try
        {
        await page.GotoAsync(_fixture.ServerAddress + "/");
        await page.Locator(".ft-landing-section").Last.WaitForAsync();

        var footer = page.Locator(".ft-site-footer");
        var footerStyle = await footer.EvaluateAsync<string>("el => getComputedStyle(el).position");
        Assert.Equal("static", footerStyle);

        var bodyClass = await page.Locator("body.ft-site-body").GetAttributeAsync("class");
        Assert.Contains("ft-site-body", bodyClass);
        Assert.Contains("ft-site-content", await page.Locator(".ft-site-content").GetAttributeAsync("class"));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Login_and_register_pages_show_back_link_to_landing()
    {
        var (page, context) = await OpenLandingPageAsync(1280, 900);
        try
        {
        await page.GotoAsync(_fixture.ServerAddress + "/Account/Login");
        Assert.True(await page.Locator("a.ft-auth-back[href='/']").IsVisibleAsync());

        await page.GotoAsync(_fixture.ServerAddress + "/Account/Register");
        Assert.True(await page.Locator("a.ft-auth-back[href='/']").IsVisibleAsync());
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Authenticated_user_visiting_root_redirects_to_tree_app()
    {
        var (page, context) = await OpenLandingPageAsync(1280, 900);
        try
        {
        await page.GotoAsync(_fixture.ServerAddress + "/TestAuth/SignIn");
        await page.GotoAsync(_fixture.ServerAddress + "/");

        await page.WaitForURLAsync("**/Home/Index**");
        Assert.True(await page.Locator(".ft-subbar").IsVisibleAsync());
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}

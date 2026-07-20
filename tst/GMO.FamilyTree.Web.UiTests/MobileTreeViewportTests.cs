using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Playwright;

using Xunit;

namespace GMO.FamilyTree.Web.UiTests;

/// <summary>
/// Mobile-friendly tree viewport checks: touch contract, one-finger pan, pinch zoom, and card taps.
/// </summary>
[Collection("AppFixture Collection")]
public partial class MobileTreeViewportTests : IAsyncLifetime
{
    [GeneratedRegex(@"translate\(([-\d.]+)px,\s*([-\d.]+)px\)\s*scale\(([-\d.]+)\)", RegexOptions.Compiled)]
    private static partial Regex TransformRegex();

    private readonly AppFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public MobileTreeViewportTests(AppFixture fixture) => _fixture = fixture;

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

    private async Task<(IPage Page, IBrowserContext Context)> OpenMobileTreeAsync(
        int width = 375,
        int height = 812)
    {
        var context = await _browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = width, Height = height },
            HasTouch = true,
            IsMobile = true,
            UserAgent =
                "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36"
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress + "/TestAuth/SignIn");
        await page.GotoAsync(_fixture.ServerAddress + AppFixture.TreePagePath);
        await page.Locator("#family-tree-graph .family-tree-card").First.WaitForAsync();
        await page.Locator(".family-tree-stage").WaitForAsync();
        return (page, context);
    }

    private static async Task<(double PanX, double PanY, double Scale)> ReadWorldTransformAsync(IPage page)
    {
        var transform = await page.Locator(".family-tree-world").EvaluateAsync<string>(
            "el => el.style.transform || ''");
        var match = TransformRegex().Match(transform ?? "");
        if (!match.Success)
            return (0, 0, 1);

        return (
            double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Dispatches touch PointerEvents on the stage (and optional start target) so pan/pinch
    /// handlers run without relying on Playwright mouse (pointerType mouse).
    /// </summary>
    private static async Task DispatchTouchPointerAsync(
        IPage page,
        string type,
        int pointerId,
        double clientX,
        double clientY)
    {
        await page.EvaluateAsync(
            @"({ type, pointerId, clientX, clientY }) => {
                const stage = document.querySelector('.family-tree-stage');
                if (!stage) throw new Error('missing .family-tree-stage');
                // Prefer the hit-tested element so pan-on-card uses a card target.
                const hit = document.elementFromPoint(clientX, clientY);
                const target = hit && stage.contains(hit) ? hit : stage;
                const event = new PointerEvent(type, {
                    bubbles: true,
                    cancelable: true,
                    composed: true,
                    pointerId,
                    pointerType: 'touch',
                    isPrimary: pointerId === 1,
                    clientX,
                    clientY,
                    buttons: type === 'pointerup' || type === 'pointercancel' ? 0 : 1,
                    view: window
                });
                target.dispatchEvent(event);
            }",
            new
            {
                type,
                pointerId,
                clientX,
                clientY
            });
    }

    [Fact]
    public async Task Mobile_tree_stage_has_touch_action_none_and_fills_viewport()
    {
        var (page, context) = await OpenMobileTreeAsync();
        try
        {
            var metrics = await page.EvaluateAsync<JsonElement>(
                @"() => {
                    const stage = document.querySelector('.family-tree-stage');
                    const subbar = document.querySelector('.ft-subbar');
                    if (!stage) return { ok: false };
                    const style = getComputedStyle(stage);
                    const rect = stage.getBoundingClientRect();
                    return {
                        ok: true,
                        touchAction: style.touchAction,
                        width: rect.width,
                        height: rect.height,
                        subbarVisible: !!(subbar && getComputedStyle(subbar).display !== 'none'
                            && subbar.getBoundingClientRect().height > 0),
                        overflowX: document.documentElement.scrollWidth > window.innerWidth + 1
                    };
                }");

            Assert.True(metrics.GetProperty("ok").GetBoolean());
            Assert.Equal("none", metrics.GetProperty("touchAction").GetString());
            Assert.True(metrics.GetProperty("width").GetDouble() >= 300,
                "Stage should use most of the phone width");
            Assert.True(metrics.GetProperty("height").GetDouble() >= 200,
                "Stage should have usable height on phone");
            Assert.True(metrics.GetProperty("subbarVisible").GetBoolean(),
                "Toolbar/nav should remain available on mobile");
            Assert.False(metrics.GetProperty("overflowX").GetBoolean(),
                "Tree page shell should not force horizontal document overflow");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Mobile_one_finger_pan_on_card_moves_world_transform()
    {
        var (page, context) = await OpenMobileTreeAsync();
        try
        {
            var card = page.Locator(".family-tree-card")
                .Filter(new() { Has = page.GetByText("Me", new() { Exact = true }) })
                .First;
            await card.WaitForAsync();
            var box = await card.BoundingBoxAsync();
            Assert.NotNull(box);

            var startX = box!.X + box.Width / 2;
            var startY = box.Y + box.Height / 2;
            var endX = startX + 80;
            var endY = startY + 40;

            var (beforePanX, beforePanY, _) = await ReadWorldTransformAsync(page);

            await DispatchTouchPointerAsync(page, "pointerdown", 1, startX, startY);
            await DispatchTouchPointerAsync(page, "pointermove", 1, startX + 20, startY + 10);
            await DispatchTouchPointerAsync(page, "pointermove", 1, endX, endY);
            await DispatchTouchPointerAsync(page, "pointerup", 1, endX, endY);

            var (afterPanX, afterPanY, _) = await ReadWorldTransformAsync(page);
            var dx = Math.Abs(afterPanX - beforePanX);
            var dy = Math.Abs(afterPanY - beforePanY);
            Assert.True(dx + dy >= 40,
                $"Expected pan after touch drag; before=({beforePanX},{beforePanY}) after=({afterPanX},{afterPanY})");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Mobile_pinch_zoom_changes_world_scale()
    {
        var (page, context) = await OpenMobileTreeAsync();
        try
        {
            var stageBox = await page.Locator(".family-tree-stage").BoundingBoxAsync();
            Assert.NotNull(stageBox);

            var cx = stageBox!.X + stageBox.Width / 2;
            var cy = stageBox.Y + stageBox.Height / 2;

            var (_, _, beforeScale) = await ReadWorldTransformAsync(page);

            // Two fingers close together, then spread apart (zoom in).
            await DispatchTouchPointerAsync(page, "pointerdown", 1, cx - 30, cy);
            await DispatchTouchPointerAsync(page, "pointerdown", 2, cx + 30, cy);
            await DispatchTouchPointerAsync(page, "pointermove", 1, cx - 90, cy);
            await DispatchTouchPointerAsync(page, "pointermove", 2, cx + 90, cy);
            await DispatchTouchPointerAsync(page, "pointerup", 2, cx + 90, cy);
            await DispatchTouchPointerAsync(page, "pointerup", 1, cx - 90, cy);

            var (_, _, afterScale) = await ReadWorldTransformAsync(page);
            Assert.True(afterScale > beforeScale + 0.15,
                $"Expected pinch zoom-in; before={beforeScale}, after={afterScale}");
            Assert.True(afterScale <= 2.5 + 0.01);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Mobile_short_tap_on_card_opens_details_popup()
    {
        var (page, context) = await OpenMobileTreeAsync();
        try
        {
            var card = page.Locator(".family-tree-card")
                .Filter(new() { Has = page.GetByText("Father", new() { Exact = true }) })
                .First;
            await card.WaitForAsync();
            var content = card.Locator(".family-tree-card-content");
            var box = await content.BoundingBoxAsync();
            Assert.NotNull(box);

            await page.Touchscreen.TapAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);

            var details = page.Locator("#member-details-popup");
            await details.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            Assert.Equal("Father", await details.Locator(".ft-details-name").InnerTextAsync());
            Assert.False(await page.Locator("#member-hover-card").IsVisibleAsync());
            Assert.True(await details.Locator(".ft-details-photo-btn").IsVisibleAsync());
            Assert.True(await details.Locator(".ft-details-manage-btn").IsVisibleAsync());
            Assert.True(await details.Locator(".ft-details-section-title")
                .Filter(new() { HasText = "Siblings" }).CountAsync() > 0);

            await details.Locator(".ft-details-manage-btn").ClickAsync();
            await page.Locator("#member-action-popup")
                .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await page.Locator("#member-action-popup .cascading-item[data-panel=\"edit\"]")
                .WaitForAsync();
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Mobile_details_change_picture_opens_photo_modal()
    {
        var (page, context) = await OpenMobileTreeAsync();
        try
        {
            var card = page.Locator(".family-tree-card")
                .Filter(new() { Has = page.GetByText("Father", new() { Exact = true }) })
                .First;
            await card.WaitForAsync();
            var content = card.Locator(".family-tree-card-content");
            var box = await content.BoundingBoxAsync();
            Assert.NotNull(box);

            await page.Touchscreen.TapAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
            var details = page.Locator("#member-details-popup");
            await details.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await details.Locator(".ft-details-photo-btn").ClickAsync();
            await page.Locator("#memberPhotoModal.show")
                .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Mobile_short_tap_on_avatar_opens_details_not_photo_modal()
    {
        var (page, context) = await OpenMobileTreeAsync();
        try
        {
            var card = page.Locator(".family-tree-card")
                .Filter(new() { Has = page.GetByText("Father", new() { Exact = true }) })
                .First;
            await card.WaitForAsync();
            var avatar = card.Locator(".member-details-trigger, .member-photo-trigger");
            var box = await avatar.BoundingBoxAsync();
            Assert.NotNull(box);

            await page.Touchscreen.TapAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);

            await page.Locator("#member-details-popup")
                .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            Assert.Equal(0, await page.Locator("#memberPhotoModal.show, .modal.show").CountAsync());
            Assert.True(await page.Locator("#member-details-popup .ft-details-photo-btn").IsVisibleAsync());
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
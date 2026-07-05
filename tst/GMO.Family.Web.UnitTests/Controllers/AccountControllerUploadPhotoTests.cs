using System.Security.Claims;
using System.Text.Json;

using GMO.Family.Web.Controllers;
using GMO.Family.Web.Data;
using GMO.Family.Web.Services.Photos;
using GMO.Family.Web.UnitTests.Fixtures;
using GMO.Family.Web.UnitTests.Helpers;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Moq;

using Xunit;

namespace GMO.Family.Web.UnitTests.Controllers;

public class AccountControllerUploadPhotoTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _fixture;

    public AccountControllerUploadPhotoTests(AccountControllerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UploadPhoto_GET_redirects_to_home()
    {
        await using var db = _fixture.CreateDb(nameof(UploadPhoto_GET_redirects_to_home));
        var (signInManager, userManager) = _fixture.CreateIdentityManagers(db);
        var controller = _fixture.CreateAccountController(
            signInManager, userManager, db,
            _fixture.CreateExternalLoginInfoProvider("user@example.com"),
            _fixture.CreateUrlHelper(),
            userId: "user-1");

        var result = controller.UploadPhoto();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task UploadPhoto_POST_json_saves_new_key_then_deletes_previous()
    {
        await using var db = _fixture.CreateDb(nameof(UploadPhoto_POST_json_saves_new_key_then_deletes_previous));
        db.UserProfiles.Add(new UserProfile { UserId = "user-1", PhotoKey = "profiles/user-1.jpg" });
        await db.SaveChangesAsync();

        var order = new List<string>();
        var photos = new Mock<IPhotoStorageService>();
        photos.Setup(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("save"))
            .Returns(Task.CompletedTask);
        photos.Setup(p => p.DeleteAsync("profiles/user-1.jpg", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("delete-old"))
            .Returns(Task.CompletedTask);

        var controller = CreateJsonController(db, photos.Object, "user-1");
        var png = MinimalPng();

        var result = await controller.UploadPhoto(new TestFormFile("avatar.png", png), CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.Serialize(json.Value);
        Assert.Contains("\"success\":true", payload);

        var profile = await db.UserProfiles.FindAsync("user-1");
        Assert.Equal("profiles/user-1.png", profile!.PhotoKey);
        Assert.Equal(["save", "delete-old"], order);
        photos.Verify(p => p.DeleteAsync("profiles/user-1.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadPhoto_POST_json_returns_error_when_storage_unavailable()
    {
        await using var db = _fixture.CreateDb(nameof(UploadPhoto_POST_json_returns_error_when_storage_unavailable));
        var photos = new Mock<IPhotoStorageService>();
        photos.Setup(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var controller = CreateJsonController(db, photos.Object, "user-1");
        var result = await controller.UploadPhoto(new TestFormFile("avatar.png", MinimalPng()), CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.Serialize(json.Value);
        Assert.Contains("\"success\":false", payload);
        Assert.Contains(PhotoStorageHelper.StorageUnavailableMessage, payload);
    }

    [Fact]
    public async Task UploadPhoto_POST_json_returns_error_when_file_missing()
    {
        await using var db = _fixture.CreateDb(nameof(UploadPhoto_POST_json_returns_error_when_file_missing));
        var controller = CreateJsonController(db, new Mock<IPhotoStorageService>().Object, "user-1");

        var result = await controller.UploadPhoto(null, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payload = JsonSerializer.Serialize(json.Value);
        Assert.Contains("Please select an image file", payload);
    }

    private AccountController CreateJsonController(AppDbContext db, IPhotoStorageService photos, string userId)
    {
        var (signInManager, userManager) = _fixture.CreateIdentityManagers(db);
        var controller = _fixture.CreateAccountController(
            signInManager, userManager, db,
            _fixture.CreateExternalLoginInfoProvider("user@example.com"),
            _fixture.CreateUrlHelper(),
            photos: photos,
            userId: userId);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";
        return controller;
    }

    private static byte[] MinimalPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xFF, 0xFF, 0x3F,
        0x00, 0x05, 0xFE, 0x02, 0xFE, 0xDC, 0xCC, 0x59, 0xE7, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82
    ];
}

using System.Security.Claims;

using GMO.Family.Web.Controllers;
using GMO.Family.Web.Data;
using GMO.Family.Web.Options;
using GMO.Family.Web.Services;
using GMO.Family.Web.Services.Photos;
using GMO.Family.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace GMO.Family.Web.UnitTests.Controllers;

public class PhotosControllerTests
{
    private const string OwnerId = "owner-1";

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static PhotosController CreateController(
        AppDbContext db,
        IPhotoStorageService photos,
        string? userId = OwnerId)
    {
        var env = new WebHostEnvironmentMock().Object;
        var paths = Microsoft.Extensions.Options.Options.Create(new PathsOptions());
        var controller = new PhotosController(
            db,
            photos,
            new FamilyTreeAccessService(db),
            env,
            paths);

        var httpContext = new DefaultHttpContext();
        if (userId != null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task MemberPhoto_returns_unauthorized_when_not_signed_in()
    {
        await using var db = CreateDb(nameof(MemberPhoto_returns_unauthorized_when_not_signed_in));
        var controller = CreateController(db, new Mock<IPhotoStorageService>().Object, userId: null);

        var result = await controller.MemberPhoto(1, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task MemberPhoto_returns_not_found_when_user_does_not_own_member()
    {
        await using var db = CreateDb(nameof(MemberPhoto_returns_not_found_when_user_does_not_own_member));
        db.FamilyTrees.Add(new FamilyTree { Id = 1, Name = "T", OwnerId = OwnerId });
        db.FamilyMembers.Add(new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "Alice", PhotoKey = "members/1/10.png" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new Mock<IPhotoStorageService>().Object, userId: "other-user");
        var result = await controller.MemberPhoto(10, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task MemberPhoto_returns_file_when_photo_exists()
    {
        await using var db = CreateDb(nameof(MemberPhoto_returns_file_when_photo_exists));
        db.FamilyTrees.Add(new FamilyTree { Id = 1, Name = "T", OwnerId = OwnerId });
        db.FamilyMembers.Add(new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "Alice", PhotoKey = "members/1/10.png" });
        await db.SaveChangesAsync();

        var bytes = new byte[] { 137, 80, 78, 71 };
        var photos = new Mock<IPhotoStorageService>();
        photos.Setup(p => p.GetAsync("members/1/10.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoStreamResult(new MemoryStream(bytes), "image/png"));

        var controller = CreateController(db, photos.Object);
        var result = await controller.MemberPhoto(10, CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", file.ContentType);
        using var reader = new MemoryStream();
        await file.FileStream.CopyToAsync(reader);
        Assert.Equal(bytes, reader.ToArray());
    }

    [Fact]
    public async Task MyProfilePhoto_returns_file_for_current_user()
    {
        await using var db = CreateDb(nameof(MyProfilePhoto_returns_file_for_current_user));
        db.UserProfiles.Add(new UserProfile { UserId = OwnerId, PhotoKey = "profiles/owner.png" });
        await db.SaveChangesAsync();

        var bytes = new byte[] { 1, 2, 3 };
        var photos = new Mock<IPhotoStorageService>();
        photos.Setup(p => p.GetAsync("profiles/owner.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoStreamResult(new MemoryStream(bytes), "image/png"));

        var controller = CreateController(db, photos.Object);
        var result = await controller.MyProfilePhoto(CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", file.ContentType);
    }

    [Fact]
    public async Task ProfilePhoto_returns_not_found_for_other_user()
    {
        await using var db = CreateDb(nameof(ProfilePhoto_returns_not_found_for_other_user));
        db.UserProfiles.Add(new UserProfile { UserId = "other-user", PhotoKey = "profiles/other.png" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new Mock<IPhotoStorageService>().Object);
        var result = await controller.ProfilePhoto("other-user", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
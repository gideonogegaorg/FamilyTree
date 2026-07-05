using System.Net;
using System.Net.Sockets;
using System.Security.Claims;

using Amazon.S3;

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

namespace GMO.Family.Web.UnitTests.Services;

public class PhotoStorageHelperTests
{
    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(AmazonS3Exception))]
    public void IsStorageException_returns_true_for_storage_failures(Type exceptionType)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "test")!;
        Assert.True(PhotoStorageHelper.IsStorageException(ex));
    }

    [Fact]
    public void IsStorageException_returns_true_for_socket_exception()
    {
        Assert.True(PhotoStorageHelper.IsStorageException(new SocketException()));
    }

    [Fact]
    public void IsStorageException_returns_false_for_other_exceptions()
    {
        Assert.False(PhotoStorageHelper.IsStorageException(new InvalidOperationException()));
    }

    [Fact]
    public async Task TryDeleteAsync_skips_null_or_empty_keys()
    {
        var photos = new Mock<IPhotoStorageService>(MockBehavior.Strict);
        await PhotoStorageHelper.TryDeleteAsync(photos.Object, null);
        await PhotoStorageHelper.TryDeleteAsync(photos.Object, "");
        photos.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryDeleteAsync_swallows_storage_exceptions()
    {
        var photos = new Mock<IPhotoStorageService>();
        photos.Setup(p => p.DeleteAsync("key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unavailable"));

        await PhotoStorageHelper.TryDeleteAsync(photos.Object, "key");
    }

    [Fact]
    public async Task DeleteManyAsync_deletes_each_non_empty_key()
    {
        var photos = new Mock<IPhotoStorageService>();
        await PhotoStorageHelper.DeleteManyAsync(photos.Object, ["a", null, "", "b"]);
        photos.Verify(p => p.DeleteAsync("a", It.IsAny<CancellationToken>()), Times.Once);
        photos.Verify(p => p.DeleteAsync("b", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_delegates_to_storage_service()
    {
        var photos = new Mock<IPhotoStorageService>();
        await using var stream = new MemoryStream([1, 2, 3]);
        await PhotoStorageHelper.SaveAsync(photos.Object, "members/1/2.png", stream, "image/png");
        photos.Verify(p => p.SaveAsync("members/1/2.png", stream, "image/png", It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class PhotoStorageKeysTests
{
    [Theory]
    [InlineData("photo.jpg", ".jpg")]
    [InlineData("photo.PNG", ".png")]
    [InlineData("photo.webp", ".webp")]
    public void NormalizeExtension_accepts_allowed_formats(string fileName, string expected)
    {
        Assert.Equal(expected, PhotoStorageKeys.NormalizeExtension(fileName));
    }

    [Theory]
    [InlineData("photo.bmp")]
    [InlineData("photo")]
    public void NormalizeExtension_rejects_invalid_formats(string fileName)
    {
        Assert.Null(PhotoStorageKeys.NormalizeExtension(fileName));
    }

    [Fact]
    public void Member_and_profile_keys_use_expected_paths()
    {
        Assert.Equal("members/5/12.jpg", PhotoStorageKeys.Member(5, 12, ".jpg"));
        Assert.Equal("profiles/user-1.png", PhotoStorageKeys.Profile("user-1", ".png"));
    }
}

public class TreeCardViewModeExtensionsTests
{
    [Fact]
    public void ToCssClass_maps_photo_modes_to_css_modifiers()
    {
        Assert.Equal("ft-view-photo-small", TreeCardViewMode.PhotoSmall.ToCssClass());
        Assert.Equal("ft-view-photo-xlarge", TreeCardViewMode.PhotoExtraLarge.ToCssClass());
        Assert.Equal("ft-view-large", TreeCardViewMode.Large.ToCssClass());
    }

    [Fact]
    public void IsPhotoOnly_identifies_gallery_modes()
    {
        Assert.True(TreeCardViewMode.PhotoMedium.IsPhotoOnly());
        Assert.False(TreeCardViewMode.Details.IsPhotoOnly());
    }
}

public class PhotoStoragePathsTests
{
    [Theory]
    [InlineData(null, "members/1/2.jpg", "members/1/2.jpg")]
    [InlineData("", "members/1/2.jpg", "members/1/2.jpg")]
    [InlineData("family/dev", "members/1/2.jpg", "family/dev/members/1/2.jpg")]
    [InlineData("family/dev/", "profiles/u.png", "family/dev/profiles/u.png")]
    [InlineData("/family/prod/", "members/1/2.jpg", "family/prod/members/1/2.jpg")]
    public void ToStorageKey_applies_prefix(string? prefix, string logicalKey, string expected)
    {
        Assert.Equal(expected, PhotoStoragePaths.ToStorageKey(prefix, logicalKey));
    }
}

public class LocalPhotoStorageServiceTests
{
    [Fact]
    public async Task Save_get_and_delete_round_trip()
    {
        var root = Path.Combine(Path.GetTempPath(), "family-photo-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var envMock = new WebHostEnvironmentMock();
            envMock.Setup(e => e.ContentRootPath).Returns(root);
            var options = Microsoft.Extensions.Options.Options.Create(new PhotosOptions
            {
                LocalBasePath = "photos",
                StoragePrefix = "family/local"
            });
            var service = new LocalPhotoStorageService(envMock.Object, options);
            var key = "members/1/2.png";
            var bytes = new byte[] { 137, 80, 78, 71 };
            await using (var ms = new MemoryStream(bytes))
                await service.SaveAsync(key, ms, "image/png");

            var expectedPath = Path.Combine(root, "photos", "family", "local", "members", "1", "2.png");
            Assert.True(File.Exists(expectedPath));

            var result = await service.GetAsync(key);
            Assert.NotNull(result);
            using (result)
            {
                Assert.Equal("image/png", result.ContentType);
                using var reader = new MemoryStream();
                await result.Stream.CopyToAsync(reader);
                Assert.Equal(bytes, reader.ToArray());
            }

            await service.DeleteAsync(key);
            Assert.Null(await service.GetAsync(key));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

public class FamilyTreeAccessServiceTests
{
    [Fact]
    public async Task UserOwnsMember_returns_true_only_for_tree_owner()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.FamilyTrees.Add(new FamilyTree { Id = 1, Name = "T", OwnerId = "owner" });
        db.FamilyMembers.Add(new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "A" });
        await db.SaveChangesAsync();

        var service = new FamilyTreeAccessService(db);
        Assert.True(await service.UserOwnsMemberAsync("owner", 10));
        Assert.False(await service.UserOwnsMemberAsync("other", 10));
    }
}

public class TreeCardViewModeServiceTests
{
    [Fact]
    public async Task SetAsync_persists_to_user_profile()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.UserProfiles.Add(new UserProfile { UserId = "u1" });
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "u1")],
                "test"));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var service = new TreeCardViewModeService(accessor, db);

        await service.SetAsync(TreeCardViewMode.Large);
        Assert.Equal(TreeCardViewMode.Large, await service.GetAsync());

        var profile = await db.UserProfiles.FindAsync("u1");
        Assert.Equal(TreeCardViewMode.Large, profile!.TreeCardViewMode);
    }
}
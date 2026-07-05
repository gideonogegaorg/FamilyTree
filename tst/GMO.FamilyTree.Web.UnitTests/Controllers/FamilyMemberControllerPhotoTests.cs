using System.Net;
using System.Security.Claims;
using System.Text.Json;

using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;
using GMO.FamilyTree.Web.UnitTests.Helpers;
using GMO.FamilyTree.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class FamilyMemberControllerPhotoTests
{
    private const string OwnerId = "owner-1";

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static FamilyMemberController CreateController(
        AppDbContext db,
        IPhotoStorageService photos,
        string userId = OwnerId,
        long? currentTreeId = 1)
    {
        var currentTree = new CurrentFamilyTreeServiceMock();
        if (currentTreeId.HasValue)
            currentTree.ReturnsCurrentTreeId(currentTreeId.Value);

        var controller = new FamilyMemberController(
            db,
            currentTree.Object,
            photos,
            new FamilyTreeAccessService(db));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task UploadMemberPhoto_saves_new_key_then_deletes_previous()
    {
        await using var db = CreateDb(nameof(UploadMemberPhoto_saves_new_key_then_deletes_previous));
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = OwnerId });
        db.FamilyMembers.Add(new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "Alice", PhotoKey = "members/1/10.jpg" });
        await db.SaveChangesAsync();

        var order = new List<string>();
        var photos = new Mock<IPhotoStorageService>();
        photos.Setup(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("save"))
            .Returns(Task.CompletedTask);
        photos.Setup(p => p.DeleteAsync("members/1/10.jpg", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("delete-old"))
            .Returns(Task.CompletedTask);

        var controller = CreateController(db, photos.Object);
        var result = await controller.UploadMemberPhoto(10, new TestFormFile("new.png", [137, 80, 78, 71]));

        var json = Assert.IsType<JsonResult>(result);
        Assert.Contains("\"success\":true", JsonSerializer.Serialize(json.Value));
        var member = await db.FamilyMembers.FindAsync(10L);
        Assert.Equal("members/1/10.png", member!.PhotoKey);
        Assert.Equal(["save", "delete-old"], order);
    }

    [Fact]
    public async Task UploadMemberPhoto_returns_not_found_for_non_owner()
    {
        await using var db = CreateDb(nameof(UploadMemberPhoto_returns_not_found_for_non_owner));
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = OwnerId });
        db.FamilyMembers.Add(new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "Alice" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new Mock<IPhotoStorageService>().Object, userId: "other-user");
        var result = await controller.UploadMemberPhoto(10, new TestFormFile("new.png", [137, 80, 78, 71]));

        var json = Assert.IsType<JsonResult>(result);
        Assert.Contains("Not found", JsonSerializer.Serialize(json.Value));
    }

    [Fact]
    public async Task RemoveMemberPhoto_clears_db_before_storage_delete()
    {
        await using var db = CreateDb(nameof(RemoveMemberPhoto_clears_db_before_storage_delete));
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = OwnerId });
        db.FamilyMembers.Add(new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "Alice", PhotoKey = "members/1/10.png" });
        await db.SaveChangesAsync();

        string? keyAtDelete = "unset";
        var photos = new Mock<IPhotoStorageService>();
        photos.Setup(p => p.DeleteAsync("members/1/10.png", It.IsAny<CancellationToken>()))
            .Callback(() => keyAtDelete = db.FamilyMembers.Find(10L)!.PhotoKey)
            .Returns(Task.CompletedTask);

        var controller = CreateController(db, photos.Object);
        var result = await controller.RemoveMemberPhoto(10);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Contains("\"success\":true", JsonSerializer.Serialize(json.Value));
        Assert.Null(await db.FamilyMembers.Where(m => m.Id == 10).Select(m => m.PhotoKey).FirstAsync());
        Assert.Null(keyAtDelete);
        photos.Verify(p => p.DeleteAsync("members/1/10.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveRelation_deletes_orphan_member_photo_from_storage()
    {
        await using var db = CreateDb(nameof(RemoveRelation_deletes_orphan_member_photo_from_storage));
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = OwnerId });
        db.FamilyMembers.Add(new FamilyMember { Id = 1, FamilyTreeId = 1, Name = "Parent" });
        db.FamilyMembers.Add(new FamilyMember { Id = 2, FamilyTreeId = 1, Name = "Child", PhotoKey = "members/1/2.png" });
        db.FamilyMemberRelationships.Add(new FamilyMemberRelationship
        {
            Id = 100,
            FamilyTreeId = 1,
            FromMemberId = 1,
            ToMemberId = 2,
            RelationshipType = RelationshipType.Parent
        });
        await db.SaveChangesAsync();

        var photos = new Mock<IPhotoStorageService>();
        photos.Setup(p => p.DeleteAsync("members/1/2.png", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = CreateController(db, photos.Object);
        var result = await controller.RemoveRelation(100);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Contains("\"success\":true", JsonSerializer.Serialize(json.Value));
        Assert.Null(await db.FamilyMembers.FindAsync(2L));
        photos.Verify(p => p.DeleteAsync("members/1/2.png", It.IsAny<CancellationToken>()), Times.Once);
    }
}
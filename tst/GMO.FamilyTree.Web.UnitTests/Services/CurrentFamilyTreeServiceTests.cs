using System.Security.Claims;

using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class CurrentFamilyTreeServiceTests
{
    private const string SessionKey = "CurrentFamilyTreeId";

    private static AppDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    private static HttpContextAccessor CreateHttpContextAccessor(
        TestSession? session = null,
        string? userId = null)
    {
        var context = new DefaultHttpContext();
        if (session != null)
            context.Session = session;
        if (userId != null)
        {
            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            context.User = new ClaimsPrincipal(identity);
        }
        return new HttpContextAccessor { HttpContext = context };
    }

    [Fact]
    public async Task GetCurrentFamilyTreeIdAsync_returns_null_when_HttpContext_is_null()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(GetCurrentFamilyTreeIdAsync_returns_null_when_HttpContext_is_null));
        var accessor = new HttpContextAccessor { HttpContext = null };
        var sut = new CurrentFamilyTreeService(accessor, db);

        // Act
        var result = await sut.GetCurrentFamilyTreeIdAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentFamilyTreeIdAsync_returns_session_value_when_session_has_valid_id()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(GetCurrentFamilyTreeIdAsync_returns_session_value_when_session_has_valid_id));
        var session = new TestSession();
        session.SetString(SessionKey, "42");
        var accessor = CreateHttpContextAccessor(session, "user1");
        var sut = new CurrentFamilyTreeService(accessor, db);

        // Act
        var result = await sut.GetCurrentFamilyTreeIdAsync();

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task GetCurrentFamilyTreeIdAsync_returns_null_when_userId_is_missing()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(GetCurrentFamilyTreeIdAsync_returns_null_when_userId_is_missing));
        var session = new TestSession();
        session.SetString(SessionKey, "invalid");
        var accessor = CreateHttpContextAccessor(session, userId: null);
        var sut = new CurrentFamilyTreeService(accessor, db);

        // Act
        var result = await sut.GetCurrentFamilyTreeIdAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentFamilyTreeIdAsync_returns_profile_id_and_sets_session_when_profile_has_CurrentFamilyTreeId()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(GetCurrentFamilyTreeIdAsync_returns_profile_id_and_sets_session_when_profile_has_CurrentFamilyTreeId));
        var userId = "user-" + Guid.NewGuid().ToString("N")[..8];
        db.UserProfiles.Add(new UserProfile { UserId = userId, CurrentFamilyTreeId = 99 });
        await db.SaveChangesAsync();
        var session = new TestSession();
        var accessor = CreateHttpContextAccessor(session, userId);
        var sut = new CurrentFamilyTreeService(accessor, db);

        // Act
        var result = await sut.GetCurrentFamilyTreeIdAsync();

        // Assert
        Assert.Equal(99, result);
        Assert.Equal("99", session.GetString(SessionKey));
    }

    [Fact]
    public async Task GetCurrentFamilyTreeIdAsync_returns_null_when_profile_is_null()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(GetCurrentFamilyTreeIdAsync_returns_null_when_profile_is_null));
        var userId = "user-" + Guid.NewGuid().ToString("N")[..8];
        var session = new TestSession();
        var accessor = CreateHttpContextAccessor(session, userId);
        var sut = new CurrentFamilyTreeService(accessor, db);

        // Act
        var result = await sut.GetCurrentFamilyTreeIdAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetCurrentFamilyTreeIdAsync_sets_session_when_id_has_value()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(SetCurrentFamilyTreeIdAsync_sets_session_when_id_has_value));
        var session = new TestSession();
        var userId = "user-" + Guid.NewGuid().ToString("N")[..8];
        var accessor = CreateHttpContextAccessor(session, userId);
        var sut = new CurrentFamilyTreeService(accessor, db);

        // Act
        await sut.SetCurrentFamilyTreeIdAsync(10);

        // Assert
        Assert.Equal("10", session.GetString(SessionKey));
        var profile = await db.UserProfiles.FindAsync(userId);
        Assert.NotNull(profile);
        Assert.Equal(10, profile.CurrentFamilyTreeId);
    }

    [Fact]
    public async Task SetCurrentFamilyTreeIdAsync_removes_session_key_when_id_is_null()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(SetCurrentFamilyTreeIdAsync_removes_session_key_when_id_is_null));
        var session = new TestSession();
        session.SetString(SessionKey, "5");
        var userId = "user-" + Guid.NewGuid().ToString("N")[..8];
        var accessor = CreateHttpContextAccessor(session, userId);
        var sut = new CurrentFamilyTreeService(accessor, db);

        // Act
        await sut.SetCurrentFamilyTreeIdAsync(null);

        // Assert
        Assert.Null(session.GetString(SessionKey));
    }

    [Fact]
    public async Task SetCurrentFamilyTreeIdAsync_updates_existing_profile()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(SetCurrentFamilyTreeIdAsync_updates_existing_profile));
        var userId = "user-" + Guid.NewGuid().ToString("N")[..8];
        db.UserProfiles.Add(new UserProfile { UserId = userId, CurrentFamilyTreeId = 1 });
        await db.SaveChangesAsync();
        var accessor = CreateHttpContextAccessor(new TestSession(), userId);
        var sut = new CurrentFamilyTreeService(accessor, db);

        // Act
        await sut.SetCurrentFamilyTreeIdAsync(2);

        // Assert
        var profile = await db.UserProfiles.FindAsync(userId);
        Assert.NotNull(profile);
        Assert.Equal(2, profile.CurrentFamilyTreeId);
    }
}
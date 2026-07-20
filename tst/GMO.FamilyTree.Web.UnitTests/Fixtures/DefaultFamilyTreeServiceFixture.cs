using AutoFixture;

using GMO.FamilyTree.Web.Data;

using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.UnitTests.Fixtures;

/// <summary>
/// Supplies default fixture and in-memory DbContext for DefaultFamilyTreeService tests.
/// </summary>
public sealed class DefaultFamilyTreeServiceFixture
{
    public IFixture Fixture { get; } = DefaultFixture.Create();

    /// <summary>Creates a unique in-memory database for the test.</summary>
    public AppDbContext CreateDb(string? name = null)
    {
        var dbName = name ?? "DefaultTree_" + Fixture.Create<Guid>().ToString("N")[..12];
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Creates an anonymous user id string.</summary>
    public string CreateUserId() => "user-" + Fixture.Create<Guid>().ToString("N")[..8];
}
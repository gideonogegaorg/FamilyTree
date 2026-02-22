using GMO.Family.Web.Services;

using Moq;

namespace GMO.Family.Web.UnitTests.Mocks;

/// <summary>
/// Mock of <see cref="IDefaultFamilyTreeService"/>; default: no new tree needed (returns null).
/// Override when the test needs a specific tree id or different behavior.
/// </summary>
public class DefaultFamilyTreeServiceMock : Mock<IDefaultFamilyTreeService>
{
    public DefaultFamilyTreeServiceMock()
        : base(MockBehavior.Loose)
    {
        Setup(d => d.EnsureDefaultFamilyTreeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);
    }

    /// <summary>Override to return a specific tree id (e.g. for assertions).</summary>
    public void ReturnsTreeId(long id) =>
        Setup(d => d.EnsureDefaultFamilyTreeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(id);
}
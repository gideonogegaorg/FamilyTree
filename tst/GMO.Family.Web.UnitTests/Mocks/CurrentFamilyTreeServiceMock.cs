using GMO.Family.Web.Services;

using Moq;

namespace GMO.Family.Web.UnitTests.Mocks;

/// <summary>
/// Mock of <see cref="ICurrentFamilyTreeService"/>; default: get returns null, set completes.
/// Override in tests when you need a specific current tree id or failure behavior.
/// </summary>
public class CurrentFamilyTreeServiceMock : Mock<ICurrentFamilyTreeService>
{
    public CurrentFamilyTreeServiceMock()
        : base(MockBehavior.Loose)
    {
        Setup(s => s.GetCurrentFamilyTreeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);
        Setup(s => s.SetCurrentFamilyTreeIdAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    /// <summary>Override so GetCurrentFamilyTreeIdAsync returns the given id.</summary>
    public void ReturnsCurrentTreeId(long id) =>
        Setup(s => s.GetCurrentFamilyTreeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(id);
}

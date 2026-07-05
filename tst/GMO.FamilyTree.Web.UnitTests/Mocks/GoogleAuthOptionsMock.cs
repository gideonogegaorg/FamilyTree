using GMO.FamilyTree.Web.Options;

using Microsoft.Extensions.Options;

using Moq;

namespace GMO.FamilyTree.Web.UnitTests.Mocks;

/// <summary>
/// Mock of <see cref="IOptionsMonitor{GoogleAuthOptions}"/>; default: Google auth enabled (ClientId/ClientSecret set).
/// Override when the test needs auth disabled or different values.
/// </summary>
public class GoogleAuthOptionsMock : Mock<IOptionsMonitor<GoogleAuthOptions>>
{
    public GoogleAuthOptionsMock()
        : base(MockBehavior.Loose)
    {
        var options = new GoogleAuthOptions { ClientId = "test-client-id", ClientSecret = "test-client-secret" };
        Setup(g => g.CurrentValue).Returns(options);
    }
}
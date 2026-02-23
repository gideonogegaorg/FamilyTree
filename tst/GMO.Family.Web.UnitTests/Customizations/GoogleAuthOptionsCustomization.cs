using AutoFixture;

using GMO.Family.Web.Options;

namespace GMO.Family.Web.UnitTests.Customizations;

/// <summary>
/// Sets test-friendly defaults for <see cref="GoogleAuthOptions"/> (Enabled = true via ClientId/ClientSecret).
/// </summary>
public sealed class GoogleAuthOptionsCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<GoogleAuthOptions>(c => c
            .With(o => o.ClientId, "test-client-id")
            .With(o => o.ClientSecret, "test-client-secret"));
    }
}
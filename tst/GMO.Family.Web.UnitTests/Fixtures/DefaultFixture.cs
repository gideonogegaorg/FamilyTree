using System.Reflection;

using AutoFixture;
using AutoFixture.AutoMoq;

using GMO.Family.Web.UnitTests.Extensions;

namespace GMO.Family.Web.UnitTests.Fixtures;

/// <summary>
/// Default AutoFixture for unit tests. Composes AutoMoq and all concrete <see cref="ICustomization"/> types
/// from the test assembly (e.g. <see cref="Customizations.OmitRecursionCustomization"/>,
/// <see cref="Customizations.GoogleAuthOptionsCustomization"/>). Add new customizations in the Customizations
/// folder and they are applied automatically.
/// </summary>
public static class DefaultFixture
{
    /// <summary>
    /// Creates a new <see cref="IFixture"/> with AutoMoq (ConfigureMembers = true) and all
    /// customizations from the unit test assembly.
    /// </summary>
    public static IFixture Create()
    {
        var fixture = new Fixture();
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true });
        fixture.ApplyAllCustomizations(typeof(DefaultFixture).Assembly);
        return fixture;
    }
}

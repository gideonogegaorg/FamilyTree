using System.Reflection;

using AutoFixture;

namespace GMO.Family.Web.UnitTests.Extensions;

/// <summary>
/// Applies all concrete <see cref="ICustomization"/> types from an assembly to a fixture.
/// </summary>
public static class FixtureExtensions
{
    public static IFixture ApplyAllCustomizations(this IFixture fixture, Assembly assembly)
    {
        var customizationTypes = assembly
            .GetTypes()
            .Where(t => typeof(ICustomization).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in customizationTypes)
        {
            var customization = (ICustomization)Activator.CreateInstance(type)!;
            fixture.Customize(customization);
        }

        return fixture;
    }
}
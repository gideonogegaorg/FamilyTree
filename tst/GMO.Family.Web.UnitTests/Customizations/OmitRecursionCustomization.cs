using AutoFixture;

namespace GMO.Family.Web.UnitTests.Customizations;

/// <summary>
/// Removes throwing on recursion and omits instead, so fixture can create graphs without infinite loops.
/// </summary>
public sealed class OmitRecursionCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Behaviors.Remove(new ThrowingRecursionBehavior());
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }
}
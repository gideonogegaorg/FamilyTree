using Xunit;

namespace GMO.Family.Web.UiTests;

[CollectionDefinition("AppFixture Collection")]
public class AppFixtureCollection : ICollectionFixture<AppFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
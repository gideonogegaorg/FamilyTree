using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

using Moq;

namespace GMO.FamilyTree.Web.UnitTests.Fixtures;

internal static class ViewComponentTestHelper
{
    public static void AttachContext(ViewComponent component, ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = user ?? new ClaimsPrincipal()
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var viewContext = new ViewContext(
            actionContext,
            new NullView(),
            new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
            TextWriter.Null,
            new HtmlHelperOptions());

        component.ViewComponentContext = new ViewComponentContext(
            new ViewComponentDescriptor(),
            new Dictionary<string, object?>(),
            HtmlEncoder.Default,
            viewContext,
            TextWriter.Null);
    }

    public static ClaimsPrincipal AuthenticatedUser(string userId, string email = "user@example.com")
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, email)
            ],
            authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class NullView : IView
    {
        public string Path => string.Empty;
        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }
}
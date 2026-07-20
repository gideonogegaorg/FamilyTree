using System.Security.Claims;

using AutoFixture;

using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;

using GMO.FamilyTree.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Moq;

namespace GMO.FamilyTree.Web.UnitTests.Fixtures;

/// <summary>
/// Provides shared setup for AccountController unit tests using the default fixture and happy-path mocks.
/// </summary>
public sealed class AccountControllerFixture
{
    public IFixture Fixture { get; } = DefaultFixture.Create();

    /// <summary>Creates a unique in-memory database for the test.</summary>
    public AppDbContext CreateDb(string? name = null)
    {
        var dbName = name ?? "TestDb_" + Fixture.Create<Guid>().ToString("N")[..12];
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Creates SignInManager and UserManager backed by the given DbContext.</summary>
    public static (SignInManager<IdentityUser>, UserManager<IdentityUser>) CreateIdentityManagers(
        AppDbContext db,
        IdentityUser? existingUser = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton(db);
        services.AddScoped<AppDbContext>(_ => db);
        services.AddIdentityCore<IdentityUser>(o => o.SignIn.RequireConfirmedEmail = true)
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddSignInManager();
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ExternalScheme)
            .AddCookie(IdentityConstants.TwoFactorUserIdScheme);
        var sp = services.BuildServiceProvider();

        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        if (existingUser != null)
            userManager.CreateAsync(existingUser).GetAwaiter().GetResult();

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var contextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        contextAccessor.HttpContext = httpContext;
        var signInManager = sp.GetRequiredService<SignInManager<IdentityUser>>();
        return (signInManager, userManager);
    }

    /// <summary>Creates ExternalLoginInfo with the given email claim.</summary>
    public static ExternalLoginInfo CreateExternalLoginInfo(string email)
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Email, email));
        var principal = new ClaimsPrincipal(identity);
        return new ExternalLoginInfo(principal, "Google", "provider-key", "Google");
    }

    /// <summary>Creates an external login info provider. Happy path: pass email (e.g. "user@example.com"). Exception paths: pass null (no info) or "" (empty email).</summary>
    public static IExternalLoginInfoProvider CreateExternalLoginInfoProvider(string? email) =>
        new ExternalLoginInfoProviderMock(email).Object;

    /// <summary>Creates an IUrlHelper with redirect defaults. Pass contentReturn for tests that expect a specific return URL.</summary>
    public static IUrlHelper CreateUrlHelper(string? contentReturn = null) =>
        (contentReturn != null ? UrlHelperMock.WithContentReturn(contentReturn) : new UrlHelperMock(contentReturn)).Object;

    /// <summary>Creates AccountController with the given dependencies. Uses happy-path mocks by default; pass optional services to override (e.g. for exception paths).</summary>
    public AccountController CreateAccountController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        AppDbContext db,
        IExternalLoginInfoProvider externalLoginInfo,
        IUrlHelper? urlHelper = null,
        IDefaultFamilyTreeService? defaultFamilyTreeService = null,
        IFamilyTreeDeletionService? familyTreeDeletion = null,
        IPhotoStorageService? photos = null,
        string? userId = null)
    {
        var currentTree = new CurrentFamilyTreeServiceMock().Object;
        var treeViewOrientation = new Mock<ITreeViewOrientationService>().Object;
        var lineageMode = new Mock<ILineageModeService>().Object;
        var defaultTree = defaultFamilyTreeService ?? new DefaultFamilyTreeServiceMock().Object;
        var familyTreeDeletionService = familyTreeDeletion ?? new Mock<IFamilyTreeDeletionService>().Object;
        var urlHelperToUse = urlHelper ?? new UrlHelperMock().Object;
        var photosService = photos ?? new Mock<IPhotoStorageService>().Object;
        var treeCardViewMode = new Mock<ITreeCardViewModeService>().Object;
        var access = new FamilyTreeAccessService(db);

        var emailSender = Fixture.Create<Mock<IEmailSender>>().Object;
        var googleAuth = new GoogleAuthOptionsMock().Object;
        var rateLimiter = CreateAllowAllRateLimiter();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountController>.Instance;

        var deps = CreateDependencies(
            signInManager,
            userManager,
            emailSender,
            googleAuth,
            db,
            currentTree,
            treeViewOrientation,
            lineageMode,
            defaultTree,
            familyTreeDeletionService,
            externalLoginInfo,
            photosService,
            treeCardViewMode,
            access,
            rateLimiter);

        var controller = new AccountController(deps, logger);

        var services = new ServiceCollection();
        services.AddSingleton<IUrlHelperFactory>(new TestUrlHelperFactory(urlHelperToUse));
        services.AddSingleton<IAuthenticationService>(new NoOpAuthService());
        var requestServices = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        if (userId != null)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    public static IEmailRateLimiter CreateAllowAllRateLimiter()
    {
        var mock = new Mock<IEmailRateLimiter>();
        mock.Setup(r => r.TryAcquire(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        return mock.Object;
    }

    public static AccountControllerDependencies CreateDependencies(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IEmailSender emailSender,
        IOptionsMonitor<GoogleAuthOptions> googleAuth,
        AppDbContext db,
        ICurrentFamilyTreeService currentTree,
        ITreeViewOrientationService treeViewOrientation,
        ILineageModeService lineageMode,
        IDefaultFamilyTreeService defaultTree,
        IFamilyTreeDeletionService familyTreeDeletion,
        IExternalLoginInfoProvider externalLoginInfo,
        IPhotoStorageService photos,
        ITreeCardViewModeService treeCardViewMode,
        IFamilyTreeAccessService access,
        IEmailRateLimiter emailRateLimiter) =>
        new(
            new AccountIdentityDependencies(signInManager, userManager),
            new AccountFamilyTreeDependencies(
                currentTree,
                treeViewOrientation,
                lineageMode,
                defaultTree,
                familyTreeDeletion,
                access,
                treeCardViewMode),
            new AccountEmailDependencies(emailSender, emailRateLimiter),
            db,
            googleAuth,
            externalLoginInfo,
            photos);

    public static HomeControllerDependencies CreateHomeDependencies(
        AppDbContext db,
        ICurrentFamilyTreeService currentTree,
        ITreeViewOrientationService treeViewOrientation,
        ILineageModeService lineageMode,
        ITreeCardViewModeService treeCardViewMode,
        IFamilyTreeAccessService access,
        IOptionsMonitor<GoogleAuthOptions> googleAuth,
        IWebHostEnvironment env) =>
        new(
            db,
            currentTree,
            new HomeTreeViewDependencies(treeViewOrientation, lineageMode, treeCardViewMode),
            access,
            googleAuth,
            env);

    private sealed class TestUrlHelperFactory : IUrlHelperFactory
    {
        private readonly IUrlHelper _helper;
        public TestUrlHelperFactory(IUrlHelper helper) => _helper = helper;
        public IUrlHelper GetUrlHelper(ActionContext context) => _helper;
    }

    private sealed class NoOpAuthService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
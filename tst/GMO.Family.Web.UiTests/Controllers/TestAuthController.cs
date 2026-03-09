using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GMO.Family.Web.UiTests.Controllers;

/// <summary>
/// Test-only controller to obtain an authenticated HTTP client in integration tests.
/// Registered only when the test host runs (via AddApplicationPart in WebAppFixture).
/// Not part of the production application.
/// </summary>
[Route("[controller]")]
[ApiController]
public class TestAuthController : ControllerBase
{
    public const string TestUserEmail = "test@example.com";
    public const string TestUserPassword = "TestPassword1!";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public TestAuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet("SignIn")]
    public async Task<IActionResult> SignIn([FromServices] GMO.Family.Web.Services.ICurrentFamilyTreeService currentTree, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(TestUserEmail);
        if (user == null)
        {
            user = new IdentityUser { UserName = TestUserEmail, Email = TestUserEmail, EmailConfirmed = true };
            await _userManager.CreateAsync(user, TestUserPassword);
        }
        await _signInManager.SignInAsync(user, isPersistent: false);
        await currentTree.SetCurrentFamilyTreeIdAsync(1, cancellationToken);
        return Ok();
    }
}

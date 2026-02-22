using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GMO.Family.Web.Controllers;

/// <summary>
/// Only active when Environment is Testing. Used by integration tests to obtain an authenticated client.
/// </summary>
[Route("[controller]")]
[ApiController]
public class TestAuthController : ControllerBase
{
    public const string TestUserEmail = "test@example.com";
    public const string TestUserPassword = "TestPassword1!";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IWebHostEnvironment _env;

    public TestAuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IWebHostEnvironment env)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _env = env;
    }

    [HttpGet("SignIn")]
    public async Task<IActionResult> SignIn(CancellationToken cancellationToken)
    {
        if (!_env.IsEnvironment("Testing"))
            return NotFound();

        var user = await _userManager.FindByEmailAsync(TestUserEmail);
        if (user == null)
        {
            user = new IdentityUser { UserName = TestUserEmail, Email = TestUserEmail, EmailConfirmed = true };
            await _userManager.CreateAsync(user, TestUserPassword);
        }
        await _signInManager.SignInAsync(user, isPersistent: false);
        return Ok();
    }
}

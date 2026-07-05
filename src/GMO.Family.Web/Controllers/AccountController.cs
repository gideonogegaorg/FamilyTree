using System.Security.Claims;

using GMO.Family.Web.Data;
using GMO.Family.Web.Models;
using GMO.Family.Web.Options;
using GMO.Family.Web.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GMO.Family.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IOptionsMonitor<GoogleAuthOptions> _googleAuth;
    private readonly AppDbContext _db;
    private readonly ICurrentFamilyTreeService _currentFamilyTree;
    private readonly ITreeViewOrientationService _treeViewOrientation;
    private readonly ILineageModeService _lineageMode;
    private readonly IDefaultFamilyTreeService _defaultFamilyTree;
    private readonly IFamilyTreeDeletionService _familyTreeDeletion;
    private readonly IWebHostEnvironment _env;
    private readonly PathsOptions _paths;
    private readonly IExternalLoginInfoProvider _externalLoginInfo;

    public AccountController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IEmailSender emailSender,
        IOptionsMonitor<GoogleAuthOptions> googleAuth,
        AppDbContext db,
        ICurrentFamilyTreeService currentFamilyTree,
        ITreeViewOrientationService treeViewOrientation,
        ILineageModeService lineageMode,
        IDefaultFamilyTreeService defaultFamilyTree,
        IFamilyTreeDeletionService familyTreeDeletion,
        IWebHostEnvironment env,
        IOptions<PathsOptions> paths,
        IExternalLoginInfoProvider externalLoginInfo)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _emailSender = emailSender;
        _googleAuth = googleAuth;
        _db = db;
        _currentFamilyTree = currentFamilyTree;
        _treeViewOrientation = treeViewOrientation;
        _lineageMode = lineageMode;
        _defaultFamilyTree = defaultFamilyTree;
        _familyTreeDeletion = familyTreeDeletion;
        _env = env;
        _paths = paths.Value;
        _externalLoginInfo = externalLoginInfo;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.GoogleAuthEnabled = _googleAuth.CurrentValue.Enabled;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        returnUrl ??= Url.Content("~/");
        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.GoogleAuthEnabled = _googleAuth.CurrentValue.Enabled;

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var defaultTreeId = await _defaultFamilyTree.EnsureDefaultFamilyTreeAsync(user.Id!, cancellationToken);
                    if (defaultTreeId.HasValue)
                        await _currentFamilyTree.SetCurrentFamilyTreeIdAsync(defaultTreeId.Value, cancellationToken);
                }
                return LocalRedirect(returnUrl);
            }
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignIn(string? returnUrl = null)
    {
        var redirectUri = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl }) ?? "/";
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, redirectUri);
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null, CancellationToken cancellationToken = default)
    {
        returnUrl ??= Url.Content("~/");
        var remoteErrorResult = HandleExternalLoginRemoteError(returnUrl, remoteError);
        if (remoteErrorResult != null)
            return remoteErrorResult;

        var info = await _externalLoginInfo.GetExternalLoginInfoAsync(cancellationToken);
        if (info == null)
            return RedirectToAction(nameof(Login), new { returnUrl });

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            return RedirectToLoginWithError(returnUrl, "Email claim not received from the external provider.");

        var (user, createError) = await GetOrCreateUserForExternalLoginAsync(email, returnUrl);
        if (createError != null)
            return createError;

        await SignInAndSetDefaultFamilyTreeAsync(user!, cancellationToken);
        return LocalRedirect(returnUrl);
    }

    private IActionResult? HandleExternalLoginRemoteError(string returnUrl, string? remoteError)
    {
        if (string.IsNullOrEmpty(remoteError))
            return null;
        ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    private IActionResult RedirectToLoginWithError(string returnUrl, string message)
    {
        ModelState.AddModelError(string.Empty, message);
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    private async Task<(IdentityUser? user, IActionResult? errorResult)> GetOrCreateUserForExternalLoginAsync(string email, string returnUrl)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
            return (user, null);

        user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return (null, RedirectToAction(nameof(Login), new { returnUrl }));
        }
        return (user, null);
    }

    private async Task SignInAndSetDefaultFamilyTreeAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        await _signInManager.SignInAsync(user, isPersistent: false);
        var defaultTreeId = await _defaultFamilyTree.EnsureDefaultFamilyTreeAsync(user.Id!, cancellationToken);
        if (defaultTreeId.HasValue)
            await _currentFamilyTree.SetCurrentFamilyTreeIdAsync(defaultTreeId.Value, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public new async Task<IActionResult> SignOut()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        returnUrl ??= Url.Content("~/");
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var user = new IdentityUser { UserName = model.Email, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await SignInAndSetDefaultFamilyTreeAsync(user, cancellationToken);
                return LocalRedirect(returnUrl);
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken = default)
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return RedirectToAction(nameof(ForgotPasswordConfirmation));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(nameof(ResetPassword), "Account", new { token, email = user.Email }, Request.Scheme)!;
            await _emailSender.SendEmailAsync(model.Email, "Reset your password", $"Please reset your password by <a href='{callbackUrl}'>clicking here</a>.");
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? token = null, string? email = null)
    {
        if (token == null || email == null)
            return BadRequest();
        return View(new ResetPasswordViewModel { Code = token, Email = email ?? string.Empty });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return RedirectToAction(nameof(ResetPasswordConfirmation));

        var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
        if (result.Succeeded)
            return RedirectToAction(nameof(ResetPasswordConfirmation));

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public IActionResult UploadPhoto()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPhoto(IFormFile? photo, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Index", "Home");

        if (photo == null || photo.Length == 0)
        {
            TempData["PhotoError"] = "Please select an image file.";
            return RedirectToAction(nameof(UploadPhoto));
        }

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
        {
            TempData["PhotoError"] = "Allowed formats: JPG, PNG, GIF, WebP.";
            return RedirectToAction(nameof(UploadPhoto));
        }

        var uploadsBase = string.IsNullOrWhiteSpace(_paths.Uploads)
            ? Path.Combine(_env.WebRootPath, "uploads")
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, _paths.Uploads));
        var uploadsDir = Path.Combine(uploadsBase, "profiles");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{userId}{ext}";
        var path = Path.Combine(uploadsDir, fileName);
        await using (var stream = new FileStream(path, FileMode.Create))
            await photo.CopyToAsync(stream, cancellationToken);

        var photoUrl = $"/uploads/profiles/{fileName}";
        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
        {
            _db.UserProfiles.Add(new UserProfile { UserId = userId, PhotoUrl = photoUrl });
        }
        else
        {
            profile.PhotoUrl = photoUrl;
        }
        await _db.SaveChangesAsync(cancellationToken);

        TempData["PhotoSuccess"] = "Profile picture updated.";
        return RedirectToAction(nameof(UploadPhoto));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchFamilyTree(long id, CancellationToken cancellationToken)
    {
        await _currentFamilyTree.SetCurrentFamilyTreeIdAsync(id, cancellationToken);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTreeViewOrientation(int orientation, CancellationToken cancellationToken)
    {
        var value = Enum.IsDefined(typeof(TreeViewOrientation), orientation)
            ? (TreeViewOrientation)orientation
            : TreeViewOrientation.Horizontal;
        await _treeViewOrientation.SetOrientationAsync(value, cancellationToken);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFamilyTree(long id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return NotFound();

        var result = await _familyTreeDeletion.DeleteAsync(userId, id, cancellationToken);
        return result == FamilyTreeDeleteResult.NotFound
            ? NotFound()
            : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLineageMode(int mode, CancellationToken cancellationToken)
    {
        var value = Enum.IsDefined(typeof(LineageMode), mode)
            ? (LineageMode)mode
            : LineageMode.Paternal;
        await _lineageMode.SetAsync(value, cancellationToken);
        return RedirectToAction("Index", "Home");
    }
}
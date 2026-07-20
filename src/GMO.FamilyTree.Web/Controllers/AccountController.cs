using System.Security.Claims;
using System.Text;

using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Extensions;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Controllers;

[Authorize]
public class AccountController : Controller
{
    private const string DefaultReturnUrlPath = "~/Home/Index";
    private const string HomeIndexPath = "/Home/Index";
    private const string AccountControllerName = "Account";
    private const string ReturnUrlViewDataKey = "ReturnUrl";
    private const string AddPasswordErrorView = "AddPasswordError";
    private const string InvalidOrExpiredLinkMessage = "Invalid or expired link.";

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
    private readonly IExternalLoginInfoProvider _externalLoginInfo;
    private readonly IPhotoStorageService _photos;
    private readonly ITreeCardViewModeService _treeCardViewMode;
    private readonly IFamilyTreeAccessService _access;
    private readonly IEmailRateLimiter _emailRateLimiter;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AccountControllerDependencies deps, ILogger<AccountController> logger)
    {
        _signInManager = deps.SignInManager;
        _userManager = deps.UserManager;
        _emailSender = deps.EmailSender;
        _googleAuth = deps.GoogleAuth;
        _db = deps.Db;
        _currentFamilyTree = deps.CurrentFamilyTree;
        _treeViewOrientation = deps.TreeViewOrientation;
        _lineageMode = deps.LineageMode;
        _defaultFamilyTree = deps.DefaultFamilyTree;
        _familyTreeDeletion = deps.FamilyTreeDeletion;
        _externalLoginInfo = deps.ExternalLoginInfo;
        _photos = deps.Photos;
        _treeCardViewMode = deps.TreeCardViewMode;
        _access = deps.Access;
        _emailRateLimiter = deps.EmailRateLimiter;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData[ReturnUrlViewDataKey] = returnUrl;
        ViewBag.GoogleAuthEnabled = _googleAuth.CurrentValue.Enabled;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        returnUrl ??= Url.Content(DefaultReturnUrlPath);
        ViewData[ReturnUrlViewDataKey] = returnUrl;
        ViewBag.GoogleAuthEnabled = _googleAuth.CurrentValue.Enabled;
        ModelState.Remove(nameof(LoginViewModel.RememberMe));

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
                await EnsureEmailTwoFactorEnabledAsync(user);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe ?? false, lockoutOnFailure: false);
            if (result.Succeeded)
                return await CompleteSignInAsync(model.Email, returnUrl, cancellationToken);
            if (result.RequiresTwoFactor)
                return RedirectToAction(nameof(LoginWith2fa), new { returnUrl, model.RememberMe });
            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty, "Confirm your email before signing in.");
                ViewBag.ShowResendConfirmation = true;
                ViewBag.ResendEmail = model.Email;
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignIn(string? returnUrl = null)
    {
        var redirectUri = Url.Action(nameof(ExternalLoginCallback), AccountControllerName, new { returnUrl }) ?? "/";
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, redirectUri);
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null, CancellationToken cancellationToken = default)
    {
        returnUrl ??= Url.Content(DefaultReturnUrlPath);
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

        var addLogin = await _userManager.AddLoginAsync(user!, info);
        if (!addLogin.Succeeded && addLogin.Errors.Any(e => e.Code != "LoginAlreadyAssociated"))
        {
            foreach (var error in addLogin.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        await ConfirmEmailFromGoogleAsync(user!);

        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.Succeeded)
            return await FinishAuthenticatedSessionAsync(user!, returnUrl, cancellationToken);

        // Fallback when provider login is present but ExternalLoginSignIn did not succeed (e.g. lockout).
        await SignInAndSetDefaultFamilyTreeAsync(user!, cancellationToken);
        return LocalRedirect(returnUrl);
    }

    private RedirectToActionResult? HandleExternalLoginRemoteError(string returnUrl, string? remoteError)
    {
        if (string.IsNullOrEmpty(remoteError))
            return null;
        ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    private RedirectToActionResult RedirectToLoginWithError(string returnUrl, string message)
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
        return Redirect("/");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData[ReturnUrlViewDataKey] = returnUrl;
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        returnUrl ??= Url.Content(DefaultReturnUrlPath);
        ViewData[ReturnUrlViewDataKey] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var existingError = await GetExistingUserRegistrationErrorAsync(model.Email);
        if (existingError != null)
        {
            ModelState.AddModelError(string.Empty, existingError);
            return View(model);
        }

        return await CreateRegisteredUserAsync(model, cancellationToken);
    }

    private async Task<string?> GetExistingUserRegistrationErrorAsync(string email)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing == null)
            return null;

        return !await _userManager.HasPasswordAsync(existing)
            ? "An account with this email already exists (for example via Google). Sign in with Google, then use Set password from your account menu."
            : "An account with this email already exists. Sign in instead, or use Forgot password if you need to reset it.";
    }

    private async Task<IActionResult> CreateRegisteredUserAsync(RegisterViewModel model, CancellationToken cancellationToken)
    {
        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        if (!await TryCreateDefaultTreeProfileAsync(user, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
            return View(model);
        }

        if (!await SendConfirmationEmailAsync(user))
            TempData["EmailRateLimited"] = true;

        await EnsureEmailTwoFactorEnabledAsync(user);
        return RedirectToAction(nameof(RegisterConfirmation), new { email = user.Email });
    }

    private async Task<bool> TryCreateDefaultTreeProfileAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        try
        {
            var defaultTreeId = await _defaultFamilyTree.EnsureDefaultFamilyTreeAsync(user.Id!, cancellationToken);
            if (!defaultTreeId.HasValue)
                return true;

            // User is not signed in yet, so CurrentFamilyTreeService cannot resolve claims.
            _db.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id!,
                CurrentFamilyTreeId = defaultTreeId.Value
            });
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Default tree creation failed for {UserId}", user.Id);
            await _userManager.DeleteAsync(user);
            return false;
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult RegisterConfirmation(string? email = null)
    {
        ViewData["Email"] = email;
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            return View("ConfirmEmail", "Invalid confirmation link.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return View("ConfirmEmail", "Invalid confirmation link.");

        var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ConfirmEmailAsync(user, token);
        return View("ConfirmEmail", result.Succeeded
            ? "Thank you for confirming your email. You can sign in now."
            : "Email confirmation failed. The link may be invalid or expired.");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendConfirmationEmail(string? email = null)
    {
        return View(new ResendConfirmationViewModel { Email = email ?? string.Empty });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmationEmail(ResendConfirmationViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null
            && !await _userManager.IsEmailConfirmedAsync(user)
            && !await SendConfirmationEmailAsync(user))
        {
            TempData["EmailRateLimited"] = true;
        }

        return RedirectToAction(nameof(RegisterConfirmation), new { email = model.Email });
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
        _ = cancellationToken;
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            // Only send reset when the account exists, email is confirmed, and has a password.
            if (user == null
                || !(await _userManager.IsEmailConfirmedAsync(user))
                || !await _userManager.HasPasswordAsync(user))
                return RedirectToAction(nameof(ForgotPasswordConfirmation));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(nameof(ResetPassword), AccountControllerName, new { token, email = user.Email }, Request.Scheme)!;
            var (html, text) = TransactionalEmail.LinkMessage(
                model.Email,
                $"We received a password reset request for your {TransactionalEmail.Brand} account.",
                "Reset your password",
                callbackUrl);
            await TrySendEmailAsync(
                EmailRateLimitOperations.ResetRequest,
                model.Email,
                TransactionalEmail.Subject("reset your password"),
                html,
                text,
                user.Id);
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
        return token == null || email == null
            ? BadRequest()
            : View(new ResetPasswordViewModel { Code = token, Email = email });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
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
        return Redirect(HomeIndexPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPhoto(IFormFile? photo, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return PhotoUploadResult("Invalid input.");

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return WantsJson()
                ? Json(new { success = false, error = "You must be signed in." })
                : Redirect(HomeIndexPath);

        if (photo == null || photo.Length == 0)
            return PhotoUploadResult("Please select an image file.");

        var ext = PhotoStorageKeys.NormalizeExtension(photo.FileName);
        if (ext == null)
            return PhotoUploadResult("Allowed formats: JPG, PNG, GIF, WebP.");

        var key = PhotoStorageKeys.Profile(userId, ext);
        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _db.UserProfiles.Add(profile);
        }

        try
        {
            await using var stream = photo.OpenReadStream();
            await PhotoStorageHelper.SaveAsync(_photos, key, stream, PhotoStorageKeys.ContentTypeForExtension(ext), cancellationToken);
        }
        catch (Exception ex) when (PhotoStorageHelper.IsStorageException(ex))
        {
            return PhotoUploadResult(PhotoStorageHelper.StorageUnavailableMessage);
        }

        var previousKey = profile.PhotoKey;
        profile.PhotoKey = key;
        profile.PhotoUrl = null;
        await _db.SaveChangesAsync(cancellationToken);
        await PhotoStorageHelper.TryDeleteAsync(_photos, previousKey != key ? previousKey : null, cancellationToken);

        if (WantsJson())
            return Json(new { success = true, photoUrl = "/photos/profiles/me" });

        TempData["PhotoSuccess"] = "Profile picture updated.";
        return Redirect(HomeIndexPath);
    }

    private bool WantsJson() =>
        string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
        || (Request.Headers.Accept.ToString()?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);

    private IActionResult PhotoUploadResult(string error)
    {
        if (WantsJson())
            return Json(new { success = false, error });

        TempData["PhotoError"] = error;
        return Redirect(HomeIndexPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTreeCardViewMode(int mode, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var value = Enum.IsDefined(typeof(TreeCardViewMode), mode)
            ? (TreeCardViewMode)mode
            : TreeCardViewMode.Standard;
        await _treeCardViewMode.SetAsync(value, cancellationToken);
        return Redirect(HomeIndexPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchFamilyTree(long id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanViewAsync(userId, id, cancellationToken))
            return NotFound();

        await _currentFamilyTree.SetCurrentFamilyTreeIdAsync(id, cancellationToken);
        return Redirect(HomeIndexPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTreeViewOrientation(int orientation, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var value = Enum.IsDefined(typeof(TreeViewOrientation), orientation)
            ? (TreeViewOrientation)orientation
            : TreeViewOrientation.Horizontal;
        await _treeViewOrientation.SetOrientationAsync(value, cancellationToken);
        return Redirect(HomeIndexPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFamilyTree(long id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (userId == null) return NotFound();

        var result = await _familyTreeDeletion.DeleteAsync(userId, id, cancellationToken);
        return result == FamilyTreeDeleteResult.NotFound
            ? NotFound()
            : Redirect(HomeIndexPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLineageMode(int mode, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var value = Enum.IsDefined(typeof(LineageMode), mode)
            ? (LineageMode)mode
            : LineageMode.Paternal;
        await _lineageMode.SetAsync(value, cancellationToken);
        return Redirect(HomeIndexPath);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> LoginWith2fa(bool rememberMe, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
            return RedirectToAction(nameof(Login), new { returnUrl });

        ViewBag.EmailAddress = user.Email ?? user.UserName;
        if (await SendTwoFactorEmailAsync(user))
            ViewBag.EmailCodeSent = true;
        else
            ViewBag.EmailRateLimited = true;

        return View(new LoginWith2FaViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2fa(LoginWith2FaViewModel model, CancellationToken cancellationToken = default)
    {
        ModelState.Remove(nameof(LoginWith2FaViewModel.RememberMe));
        ModelState.Remove(nameof(LoginWith2FaViewModel.RememberMachine));

        if (!ModelState.IsValid)
            return View(model);

        var returnUrl = model.ReturnUrl ?? Url.Content(DefaultReturnUrlPath);
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
            return RedirectToAction(nameof(Login), new { returnUrl });

        var code = model.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await _signInManager.TwoFactorSignInAsync(
            TokenOptions.DefaultEmailProvider, code, model.RememberMe ?? false, model.RememberMachine ?? false);
        if (result.Succeeded)
        {
            await EnsureDefaultTreeForUserAsync(user, cancellationToken);
            return LocalRedirect(returnUrl);
        }

        ViewBag.EmailAddress = user.Email ?? user.UserName;
        ModelState.AddModelError(string.Empty, "Invalid verification code.");
        return View(model);
    }

    public const string AddPasswordTokenPurpose = "AddPassword";

    [HttpGet]
    public async Task<IActionResult> ManagePassword()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound();

        return await _userManager.HasPasswordAsync(user)
            ? View("ChangePassword", new ChangePasswordViewModel())
            : View("SetPassword", new SetPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPassword(SetPasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound();

        if (await _userManager.HasPasswordAsync(user))
            return RedirectToAction(nameof(ManagePassword));

        if (!ModelState.IsValid)
            return View(model);

        var result = await _userManager.AddPasswordAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.UpdateSecurityStampAsync(user);
        await EnsureEmailTwoFactorEnabledAsync(user);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestAddPassword()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound();

        if (await _userManager.HasPasswordAsync(user))
            return RedirectToAction(nameof(ManagePassword));

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Confirm your email before adding a password.");
            return View("RequestAddPassword");
        }

        if (!await SendAddPasswordEmailAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Too many email requests recently. Please wait a few minutes and try again.");
            return View("RequestAddPassword");
        }

        return View("RequestAddPasswordConfirmation", user.Email);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmAddPassword(string userId, string code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            return View(AddPasswordErrorView, InvalidOrExpiredLinkMessage);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return View(AddPasswordErrorView, InvalidOrExpiredLinkMessage);

        if (await _userManager.HasPasswordAsync(user))
            return View(AddPasswordErrorView, "This account already has a password. Sign in and use Change password instead.");

        if (!await _userManager.IsEmailConfirmedAsync(user))
            return View(AddPasswordErrorView, "Confirm your email before adding a password.");

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return View(AddPasswordErrorView, InvalidOrExpiredLinkMessage);
        }

        return !await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, AddPasswordTokenPurpose, token)
            ? View(AddPasswordErrorView, InvalidOrExpiredLinkMessage)
            : View("AddPassword", new AddPasswordViewModel { UserId = userId, Code = code });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPassword(AddPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
            return View(AddPasswordErrorView, InvalidOrExpiredLinkMessage);

        if (await _userManager.HasPasswordAsync(user))
            return View(AddPasswordErrorView, "This account already has a password. Sign in and use Change password instead.");

        if (!await _userManager.IsEmailConfirmedAsync(user))
            return View(AddPasswordErrorView, "Confirm your email before adding a password.");

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));
        }
        catch (FormatException)
        {
            return View(AddPasswordErrorView, InvalidOrExpiredLinkMessage);
        }

        if (!await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, AddPasswordTokenPurpose, token))
            return View(AddPasswordErrorView, InvalidOrExpiredLinkMessage);

        var result = await _userManager.AddPasswordAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.UpdateSecurityStampAsync(user);
        await EnsureEmailTwoFactorEnabledAsync(user);

        var signedIn = await _userManager.GetUserAsync(User);
        return signedIn?.Id == user.Id
            ? RedirectToAction("Index", "Home")
            : RedirectToAction(nameof(Login), new { returnUrl = HomeIndexPath });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound();

        if (!await _userManager.HasPasswordAsync(user))
            return RedirectToAction(nameof(ManagePassword));

        if (!ModelState.IsValid)
            return View(model);

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        model.StatusMessage = "Your password has been changed.";
        model.CurrentPassword = string.Empty;
        model.NewPassword = string.Empty;
        model.ConfirmPassword = string.Empty;
        return View(model);
    }

    private async Task ConfirmEmailFromGoogleAsync(IdentityUser user)
    {
        if (user.EmailConfirmed)
            return;

        user.EmailConfirmed = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            _logger.LogWarning("Failed to confirm email for user {UserId} after Google sign-in", user.Id);
    }

    private async Task<bool> SendAddPasswordEmailAsync(IdentityUser user)
    {
        var token = await _userManager.GenerateUserTokenAsync(
            user, TokenOptions.DefaultProvider, AddPasswordTokenPurpose);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var callbackUrl = Url.Action(nameof(ConfirmAddPassword), AccountControllerName, new { userId = user.Id, code }, Request.Scheme)!;
        var (html, text) = TransactionalEmail.LinkMessage(
            user.Email!,
            $"Confirm that you want to add a password to your {TransactionalEmail.Brand} account.",
            "Confirm adding a password",
            callbackUrl);
        return await TrySendEmailAsync(
            EmailRateLimitOperations.AddCredential,
            user.Email!,
            TransactionalEmail.Subject("confirm adding a password"),
            html,
            text,
            user.Id);
    }

    private async Task<bool> SendConfirmationEmailAsync(IdentityUser user)
    {
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Action(nameof(ConfirmEmail), AccountControllerName, new { userId = user.Id, code }, Request.Scheme)!;
        var (html, text) = TransactionalEmail.LinkMessage(
            user.Email!,
            $"You created a {TransactionalEmail.Brand} account with this email. Confirm your address to finish signing up.",
            "Confirm your email",
            callbackUrl);
        return await TrySendEmailAsync(
            EmailRateLimitOperations.Confirmation,
            user.Email!,
            TransactionalEmail.Subject("confirm your email"),
            html,
            text,
            user.Id);
    }

    private async Task<bool> TrySendEmailAsync(
        string operation,
        string recipientEmail,
        string subject,
        string htmlMessage,
        string plainTextMessage,
        string? userId = null)
    {
        if (!_emailRateLimiter.TryAcquire(operation, recipientEmail, GetClientIp()))
        {
            if (userId is null)
                _logger.LogWarning("Email rate limit denied, Operation={Operation}", operation);
            else
                _logger.LogWarning("Email rate limit denied, Operation={Operation}, UserId={UserId}", operation, userId);
            return false;
        }

        try
        {
            await _emailSender.SendEmailAsync(recipientEmail, subject, htmlMessage, plainTextMessage, operation);
            return true;
        }
        catch (Exception ex)
        {
            if (userId is null)
                _logger.LogError(ex, "Email send failed, Operation={Operation}", operation);
            else
                _logger.LogError(ex, "Email send failed, Operation={Operation}, UserId={UserId}", operation, userId);
            return false;
        }
    }

    private string GetClientIp() => HttpContext.GetClientIpForRateLimit();

    private async Task<IActionResult> CompleteSignInAsync(string email, string returnUrl, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
            await EnsureDefaultTreeForUserAsync(user, cancellationToken);

        return LocalRedirect(returnUrl);
    }

    private async Task<IActionResult> FinishAuthenticatedSessionAsync(IdentityUser user, string returnUrl, CancellationToken cancellationToken)
    {
        await EnsureDefaultTreeForUserAsync(user, cancellationToken);
        return LocalRedirect(returnUrl);
    }

    private async Task EnsureDefaultTreeForUserAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        var defaultTreeId = await _defaultFamilyTree.EnsureDefaultFamilyTreeAsync(user.Id!, cancellationToken);
        if (defaultTreeId.HasValue)
            await _currentFamilyTree.SetCurrentFamilyTreeIdAsync(defaultTreeId.Value, cancellationToken);
    }

    private async Task EnsureEmailTwoFactorEnabledAsync(IdentityUser user)
    {
        if (!await _userManager.HasPasswordAsync(user))
            return;

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            await _userManager.SetTwoFactorEnabledAsync(user, true);
    }

    private async Task<bool> SendTwoFactorEmailAsync(IdentityUser user)
    {
        var email = user.Email ?? user.UserName;
        if (string.IsNullOrEmpty(email))
            return false;

        var code = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
        var (html, text) = TransactionalEmail.CodeMessage(
            email,
            $"Use this code to finish signing in to {TransactionalEmail.Brand}.",
            code);
        return await TrySendEmailAsync(
            EmailRateLimitOperations.TwoFactor,
            email,
            TransactionalEmail.Subject("your sign-in code"),
            html,
            text,
            user.Id);
    }
}
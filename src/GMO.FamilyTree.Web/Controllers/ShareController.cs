using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Extensions;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Controllers;

[Authorize]
public sealed class ShareController : Controller
{
    private const string ManageViewName = "Manage";
    private const string AcceptErrorView = "AcceptError";

    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IFamilyTreeAccessService _access;
    private readonly IFamilyTreeShareService _share;
    private readonly ICurrentFamilyTreeService _currentTree;
    private readonly IEmailSender _emailSender;
    private readonly IEmailRateLimiter _emailRateLimiter;
    private readonly ILogger<ShareController> _logger;

    public ShareController(ShareControllerDependencies deps, ILogger<ShareController> logger)
    {
        _db = deps.Db;
        _userManager = deps.UserManager;
        _access = deps.Access;
        _share = deps.Share;
        _currentTree = deps.CurrentTree;
        _emailSender = deps.EmailSender;
        _emailRateLimiter = deps.EmailRateLimiter;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Manage(long treeId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        return View(await BuildManageModelAsync(treeId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLinkInvite(long treeId, CreateLinkInviteInput createLink, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        if (!ModelState.IsValid)
        {
            var invalid = await BuildManageModelAsync(treeId, cancellationToken);
            invalid.CreateLink = createLink;
            return ManageView(invalid);
        }

        var expiresAt = DaysToExpiry(createLink.ExpiresInDays);
        var invite = await _share.CreateLinkInviteAsync(treeId, userId, createLink.Role, expiresAt, cancellationToken);
        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.CreatedLinkUrl = AcceptUrl(invite.Token);
        model.StatusMessage = "Share link created. Copy it and send it to the people you want to invite.";
        return ManageView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmailInvite(long treeId, CreateEmailInviteInput createEmail, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        if (!ModelState.IsValid)
        {
            var invalid = await BuildManageModelAsync(treeId, cancellationToken);
            invalid.CreateEmail = createEmail;
            return ManageView(invalid);
        }

        if (!_emailRateLimiter.TryAcquire(
                EmailRateLimitOperations.ShareInvite,
                createEmail.Email,
                HttpContext.GetClientIpForRateLimit()))
        {
            var limited = await BuildManageModelAsync(treeId, cancellationToken);
            limited.StatusMessage = "Too many emails to that address recently. Wait a few minutes and try again.";
            return ManageView(limited);
        }

        var expiresAt = DaysToExpiry(createEmail.ExpiresInDays);
        var invite = await _share.CreateEmailInviteAsync(
            treeId, userId, createEmail.Email, createEmail.Role, expiresAt, cancellationToken);

        var tree = await _db.FamilyTrees.AsNoTracking().FirstAsync(t => t.Id == treeId, cancellationToken);
        if (!await TrySendInviteEmailAsync(invite, tree.Name, userId, cancellationToken))
        {
            var failed = await BuildManageModelAsync(treeId, cancellationToken);
            failed.StatusMessage = "Could not send the invite email. Please try again in a few minutes.";
            return ManageView(failed);
        }

        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = $"Invite sent to {createEmail.Email}.";
        return ManageView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeInvite(long treeId, long inviteId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        await _share.RevokeInviteAsync(inviteId, userId, cancellationToken);
        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = "Invite revoked.";
        return ManageView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendInvite(long treeId, long inviteId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        var pendingInvite = await _db.FamilyTreeInvites.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.FamilyTreeId == treeId, cancellationToken);
        if (pendingInvite == null || pendingInvite.IsLinkInvite || string.IsNullOrEmpty(pendingInvite.Email))
        {
            var failed = await BuildManageModelAsync(treeId, cancellationToken);
            failed.StatusMessage = "Could not resend that invite.";
            return ManageView(failed);
        }

        if (!_emailRateLimiter.TryAcquire(
                EmailRateLimitOperations.ShareInvite,
                pendingInvite.Email,
                HttpContext.GetClientIpForRateLimit()))
        {
            var limited = await BuildManageModelAsync(treeId, cancellationToken);
            limited.StatusMessage = "Too many emails to that address recently. Wait a few minutes and try again.";
            return ManageView(limited);
        }

        var invite = await _share.ResendEmailInviteAsync(inviteId, userId, cancellationToken);
        if (invite == null)
        {
            var failed = await BuildManageModelAsync(treeId, cancellationToken);
            failed.StatusMessage = "Could not resend that invite.";
            return ManageView(failed);
        }

        var tree = await _db.FamilyTrees.AsNoTracking().FirstAsync(t => t.Id == treeId, cancellationToken);
        if (!await TrySendInviteEmailAsync(invite, tree.Name, userId, cancellationToken))
        {
            var limited = await BuildManageModelAsync(treeId, cancellationToken);
            limited.StatusMessage = "Could not send the invite email. Please try again in a few minutes.";
            return ManageView(limited);
        }

        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = $"Invite resent to {invite.Email}.";
        return ManageView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCollaborator(long treeId, string collaboratorUserId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        await _share.RemoveCollaboratorAsync(treeId, collaboratorUserId, userId, cancellationToken);
        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = "Access removed.";
        return ManageView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(long treeId, string collaboratorUserId, TreeShareRole role, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        await _share.ChangeCollaboratorRoleAsync(treeId, collaboratorUserId, role, userId, cancellationToken);
        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = "Role updated.";
        return ManageView(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return View(AcceptErrorView, "This invite link is invalid.");

        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Url.Action(nameof(Accept), "Share", new { token }) ?? "/";
            return RedirectToAction("Login", "Account", new { returnUrl });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var (result, treeId) = await _share.AcceptInviteAsync(token, user.Id, user.Email, cancellationToken);
        return result switch
        {
            InviteAcceptResult.Success when treeId.HasValue => await FinishAcceptAsync(treeId.Value, cancellationToken),
            InviteAcceptResult.AlreadyOwner when treeId.HasValue => await FinishAcceptAsync(treeId.Value, cancellationToken),
            InviteAcceptResult.Expired => View(AcceptErrorView, "This invite has expired."),
            InviteAcceptResult.Revoked => View(AcceptErrorView, "This invite was revoked."),
            InviteAcceptResult.EmailMismatch => View(AcceptErrorView, "Sign in with the email address this invite was sent to."),
            _ => View(AcceptErrorView, "This invite link is invalid or no longer available.")
        };
    }

    private async Task<IActionResult> FinishAcceptAsync(long treeId, CancellationToken cancellationToken)
    {
        await _currentTree.SetCurrentFamilyTreeIdAsync(treeId, cancellationToken);
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    private async Task<ShareManageViewModel> BuildManageModelAsync(long treeId, CancellationToken cancellationToken)
    {
        var tree = await _db.FamilyTrees.AsNoTracking().FirstAsync(t => t.Id == treeId, cancellationToken);
        var collaborators = await _share.GetCollaboratorsAsync(treeId, cancellationToken);
        var invites = await _share.GetPendingInvitesAsync(treeId, cancellationToken);

        return new ShareManageViewModel
        {
            TreeId = tree.Id,
            TreeName = tree.Name,
            Collaborators = collaborators.Select(c => new ShareCollaboratorViewModel
            {
                UserId = c.UserId,
                Email = c.User.Email ?? c.User.UserName ?? c.UserId,
                Role = c.Role,
                GrantedAt = c.GrantedAt
            }).ToList(),
            PendingInvites = invites.Select(i => new ShareInviteViewModel
            {
                Id = i.Id,
                IsLink = i.IsLinkInvite,
                Email = i.Email,
                Role = i.Role,
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
                AcceptUrl = AcceptUrl(i.Token)
            }).ToList()
        };
    }

    private async Task<bool> TrySendInviteEmailAsync(
        FamilyTreeInvite invite,
        string treeName,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(invite.Email))
            return false;

        try
        {
            var inviter = await _userManager.FindByIdAsync(invite.CreatedByUserId);
            var inviterEmail = string.IsNullOrWhiteSpace(inviter?.Email)
                ? "A family tree member"
                : inviter.Email;
            var url = AcceptUrl(invite.Token);
            var roleLabel = invite.Role == TreeShareRole.Editor ? "edit" : "view";
            var subject = TransactionalEmail.Subject(
                $"{inviterEmail} invited you to {roleLabel} \"{treeName}\"");
            var (html, text) = TransactionalEmail.InviteMessage(
                invite.Email, inviterEmail, roleLabel, treeName, url);
            await _emailSender.SendEmailAsync(
                invite.Email,
                subject,
                html,
                text,
                EmailRateLimitOperations.ShareInvite);
            // codeql[cs/exposure-of-sensitive-information] InviteId is a numeric PK correlator, not mailbox PII.
            _logger.LogInformation(
                "Share invite email sent, InviteId={InviteId}, Operation={Operation}",
                invite.Id,
                EmailRateLimitOperations.ShareInvite);
            return true;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                // codeql[cs/exposure-of-sensitive-information] InviteId is a numeric PK correlator, not mailbox PII.
                _logger.LogError(
                    ex,
                    "Share invite email failed, InviteId={InviteId}, Operation={Operation}",
                    invite.Id,
                    EmailRateLimitOperations.ShareInvite);
            }
            await _share.RevokeInviteAsync(invite.Id, ownerUserId, cancellationToken);
            return false;
        }
    }

    private string AcceptUrl(string token)
        => Url.Action(nameof(Accept), "Share", new { token }, Request.Scheme) ?? $"/Share/Accept/{token}";

    private static DateTimeOffset? DaysToExpiry(int? days)
        => days is > 0 ? DateTimeOffset.UtcNow.AddDays(days.Value) : null;

    private ViewResult ManageView(ShareManageViewModel model) => View(ManageViewName, model);
}
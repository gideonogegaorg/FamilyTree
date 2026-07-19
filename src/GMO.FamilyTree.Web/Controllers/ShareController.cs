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
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IFamilyTreeAccessService _access;
    private readonly IFamilyTreeShareService _share;
    private readonly ICurrentFamilyTreeService _currentTree;
    private readonly IEmailSender _emailSender;
    private readonly IEmailRateLimiter _emailRateLimiter;

    public ShareController(
        AppDbContext db,
        UserManager<IdentityUser> userManager,
        IFamilyTreeAccessService access,
        IFamilyTreeShareService share,
        ICurrentFamilyTreeService currentTree,
        IEmailSender emailSender,
        IEmailRateLimiter emailRateLimiter)
    {
        _db = db;
        _userManager = userManager;
        _access = access;
        _share = share;
        _currentTree = currentTree;
        _emailSender = emailSender;
        _emailRateLimiter = emailRateLimiter;
    }

    [HttpGet]
    public async Task<IActionResult> Manage(long treeId, CancellationToken cancellationToken)
    {
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

        var expiresAt = DaysToExpiry(createLink.ExpiresInDays);
        var invite = await _share.CreateLinkInviteAsync(treeId, userId, createLink.Role, expiresAt, cancellationToken);
        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.CreatedLinkUrl = AcceptUrl(invite.Token);
        model.StatusMessage = "Share link created. Copy it and send it to the people you want to invite.";
        return View("Manage", model);
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
            return View("Manage", invalid);
        }

        if (!_emailRateLimiter.TryAcquire(
                EmailRateLimitOperations.ShareInvite,
                createEmail.Email,
                HttpContext.GetClientIpForRateLimit()))
        {
            var limited = await BuildManageModelAsync(treeId, cancellationToken);
            limited.StatusMessage = "Too many emails to that address recently. Wait a few minutes and try again.";
            return View("Manage", limited);
        }

        var expiresAt = DaysToExpiry(createEmail.ExpiresInDays);
        var invite = await _share.CreateEmailInviteAsync(
            treeId, userId, createEmail.Email, createEmail.Role, expiresAt, cancellationToken);

        var tree = await _db.FamilyTrees.AsNoTracking().FirstAsync(t => t.Id == treeId, cancellationToken);
        if (!await TrySendInviteEmailAsync(invite, tree.Name, userId, cancellationToken))
        {
            var failed = await BuildManageModelAsync(treeId, cancellationToken);
            failed.StatusMessage = "Could not send the invite email. Please try again in a few minutes.";
            return View("Manage", failed);
        }

        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = $"Invite sent to {createEmail.Email}.";
        return View("Manage", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeInvite(long treeId, long inviteId, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        await _share.RevokeInviteAsync(inviteId, userId, cancellationToken);
        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = "Invite revoked.";
        return View("Manage", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendInvite(long treeId, long inviteId, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        var pendingInvite = await _db.FamilyTreeInvites.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.FamilyTreeId == treeId, cancellationToken);
        if (pendingInvite == null || pendingInvite.IsLinkInvite || string.IsNullOrEmpty(pendingInvite.Email))
        {
            var failed = await BuildManageModelAsync(treeId, cancellationToken);
            failed.StatusMessage = "Could not resend that invite.";
            return View("Manage", failed);
        }

        if (!_emailRateLimiter.TryAcquire(
                EmailRateLimitOperations.ShareInvite,
                pendingInvite.Email,
                HttpContext.GetClientIpForRateLimit()))
        {
            var limited = await BuildManageModelAsync(treeId, cancellationToken);
            limited.StatusMessage = "Too many emails to that address recently. Wait a few minutes and try again.";
            return View("Manage", limited);
        }

        var invite = await _share.ResendEmailInviteAsync(inviteId, userId, cancellationToken);
        if (invite == null)
        {
            var failed = await BuildManageModelAsync(treeId, cancellationToken);
            failed.StatusMessage = "Could not resend that invite.";
            return View("Manage", failed);
        }

        var tree = await _db.FamilyTrees.AsNoTracking().FirstAsync(t => t.Id == treeId, cancellationToken);
        if (!await TrySendInviteEmailAsync(invite, tree.Name, userId, cancellationToken))
        {
            var limited = await BuildManageModelAsync(treeId, cancellationToken);
            limited.StatusMessage = "Could not send the invite email. Please try again in a few minutes.";
            return View("Manage", limited);
        }

        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = $"Invite resent to {invite.Email}.";
        return View("Manage", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCollaborator(long treeId, string collaboratorUserId, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        await _share.RemoveCollaboratorAsync(treeId, collaboratorUserId, userId, cancellationToken);
        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = "Access removed.";
        return View("Manage", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(long treeId, string collaboratorUserId, TreeShareRole role, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null || !await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            return NotFound();

        await _share.ChangeCollaboratorRoleAsync(treeId, collaboratorUserId, role, userId, cancellationToken);
        var model = await BuildManageModelAsync(treeId, cancellationToken);
        model.StatusMessage = "Role updated.";
        return View("Manage", model);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return View("AcceptError", "This invite link is invalid.");

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
            InviteAcceptResult.Expired => View("AcceptError", "This invite has expired."),
            InviteAcceptResult.Revoked => View("AcceptError", "This invite was revoked."),
            InviteAcceptResult.EmailMismatch => View("AcceptError", "Sign in with the email address this invite was sent to."),
            _ => View("AcceptError", "This invite link is invalid or no longer available.")
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
            var inviterLabel = System.Net.WebUtility.HtmlEncode(inviterEmail);
            var treeNameEncoded = System.Net.WebUtility.HtmlEncode(treeName);
            var url = AcceptUrl(invite.Token);
            var roleLabel = invite.Role == TreeShareRole.Editor ? "edit" : "view";
            var subject = $"{inviterEmail} invited you to {roleLabel} the \"{treeName}\" family tree";
            var body = $"""
                <p>{inviterLabel} invited you to <strong>{roleLabel}</strong> the family tree <strong>{treeNameEncoded}</strong>.</p>
                <p><a href="{url}">Accept the invite</a></p>
                <p>You'll need to sign in or create an account. This link was sent to {System.Net.WebUtility.HtmlEncode(invite.Email)}.</p>
                """;
            await _emailSender.SendEmailAsync(invite.Email, subject, body);
            return true;
        }
        catch (Exception)
        {
            await _share.RevokeInviteAsync(invite.Id, ownerUserId, cancellationToken);
            return false;
        }
    }

    private string AcceptUrl(string token)
        => Url.Action(nameof(Accept), "Share", new { token }, Request.Scheme) ?? $"/Share/Accept/{token}";

    private static DateTimeOffset? DaysToExpiry(int? days)
        => days is > 0 ? DateTimeOffset.UtcNow.AddDays(days.Value) : null;
}
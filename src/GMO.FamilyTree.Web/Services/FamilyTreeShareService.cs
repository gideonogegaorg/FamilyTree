using System.Security.Cryptography;

using GMO.FamilyTree.Web.Data;

using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Services;

public sealed class FamilyTreeShareService : IFamilyTreeShareService
{
    private readonly AppDbContext _db;
    private readonly IFamilyTreeAccessService _access;

    public FamilyTreeShareService(AppDbContext db, IFamilyTreeAccessService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<FamilyTreeInvite> CreateLinkInviteAsync(
        long treeId,
        string createdByUserId,
        TreeShareRole role,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(treeId, createdByUserId, cancellationToken);
        var invite = NewInvite(treeId, createdByUserId, role, email: null, expiresAt);
        _db.FamilyTreeInvites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);
        return invite;
    }

    public async Task<FamilyTreeInvite> CreateEmailInviteAsync(
        long treeId,
        string createdByUserId,
        string email,
        TreeShareRole role,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(treeId, createdByUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        var invite = NewInvite(treeId, createdByUserId, role, email.Trim(), expiresAt);
        _db.FamilyTreeInvites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);
        return invite;
    }

    public async Task<(InviteAcceptResult Result, long? TreeId)> AcceptInviteAsync(
        string token,
        string userId,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
            return (InviteAcceptResult.NotFound, null);

        var invite = await _db.FamilyTreeInvites
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
        var inviteValidation = ValidateInvite(invite);
        if (inviteValidation.HasValue)
            return inviteValidation.Value;

        var emailValidation = ValidateInviteEmail(invite!, userEmail);
        if (emailValidation.HasValue)
            return emailValidation.Value;

        return await CompleteInviteAcceptanceAsync(invite!, userId, cancellationToken);
    }

    public async Task<bool> RevokeInviteAsync(long inviteId, string ownerUserId, CancellationToken cancellationToken = default)
    {
        var invite = await _db.FamilyTreeInvites.FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);
        if (invite == null)
            return false;
        if (!await _access.CanManageSharingAsync(ownerUserId, invite.FamilyTreeId, cancellationToken))
            return false;
        if (invite.RevokedAt != null)
            return true;

        invite.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveCollaboratorAsync(
        long treeId,
        string collaboratorUserId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await _access.CanManageSharingAsync(ownerUserId, treeId, cancellationToken))
            return false;
        if (collaboratorUserId == ownerUserId)
            return false;

        var grant = await _db.FamilyTreeAccesses
            .FirstOrDefaultAsync(a => a.FamilyTreeId == treeId && a.UserId == collaboratorUserId, cancellationToken);
        if (grant == null)
            return false;

        _db.FamilyTreeAccesses.Remove(grant);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ChangeCollaboratorRoleAsync(
        long treeId,
        string collaboratorUserId,
        TreeShareRole role,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await _access.CanManageSharingAsync(ownerUserId, treeId, cancellationToken))
            return false;

        var grant = await _db.FamilyTreeAccesses
            .FirstOrDefaultAsync(a => a.FamilyTreeId == treeId && a.UserId == collaboratorUserId, cancellationToken);
        if (grant == null)
            return false;

        grant.Role = role;
        grant.GrantedAt = DateTimeOffset.UtcNow;
        grant.GrantedByUserId = ownerUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<FamilyTreeInvite?> ResendEmailInviteAsync(
        long inviteId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var invite = await _db.FamilyTreeInvites.FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);
        if (invite == null || invite.IsLinkInvite || string.IsNullOrEmpty(invite.Email))
            return null;
        if (!await _access.CanManageSharingAsync(ownerUserId, invite.FamilyTreeId, cancellationToken))
            return null;
        if (invite.RevokedAt != null || invite.AcceptedAt != null)
            return null;

        // Rotate token so old emailed links stop working after resend.
        invite.Token = GenerateToken();
        invite.CreatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return invite;
    }

    public async Task<IReadOnlyList<FamilyTreeAccess>> GetCollaboratorsAsync(
        long treeId,
        CancellationToken cancellationToken = default)
    {
        return await _db.FamilyTreeAccesses.AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.FamilyTreeId == treeId)
            .OrderBy(a => a.User.Email)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FamilyTreeInvite>> GetPendingInvitesAsync(
        long treeId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.FamilyTreeInvites.AsNoTracking()
            .Where(i => i.FamilyTreeId == treeId
                && i.RevokedAt == null
                && (i.Email == null || i.AcceptedAt == null)
                && (i.ExpiresAt == null || i.ExpiresAt > now))
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private static (InviteAcceptResult Result, long? TreeId)? ValidateInvite(FamilyTreeInvite? invite)
    {
        if (invite == null)
            return (InviteAcceptResult.NotFound, null);
        if (invite.RevokedAt != null)
            return (InviteAcceptResult.Revoked, invite.FamilyTreeId);
        if (invite.ExpiresAt != null && invite.ExpiresAt <= DateTimeOffset.UtcNow)
            return (InviteAcceptResult.Expired, invite.FamilyTreeId);

        // Email invites are single-use; link invites remain reusable until revoked/expired.
        if (!invite.IsLinkInvite && invite.AcceptedAt != null)
            return (InviteAcceptResult.NotFound, invite.FamilyTreeId);

        return null;
    }

    private static (InviteAcceptResult Result, long? TreeId)? ValidateInviteEmail(FamilyTreeInvite invite, string? userEmail)
    {
        if (invite.IsLinkInvite)
            return null;

        if (string.IsNullOrEmpty(userEmail)
            || !string.Equals(userEmail.Trim(), invite.Email!.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (InviteAcceptResult.EmailMismatch, invite.FamilyTreeId);
        }

        return null;
    }

    private async Task<(InviteAcceptResult Result, long? TreeId)> CompleteInviteAcceptanceAsync(
        FamilyTreeInvite invite,
        string userId,
        CancellationToken cancellationToken)
    {
        var tree = await _db.FamilyTrees.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == invite.FamilyTreeId, cancellationToken);
        if (tree == null)
            return (InviteAcceptResult.NotFound, null);
        if (tree.OwnerId == userId)
            return (InviteAcceptResult.AlreadyOwner, tree.Id);

        await UpsertCollaboratorAccessAsync(invite, userId, cancellationToken);
        MarkEmailInviteAccepted(invite, userId);

        await _db.SaveChangesAsync(cancellationToken);
        return (InviteAcceptResult.Success, invite.FamilyTreeId);
    }

    private async Task UpsertCollaboratorAccessAsync(
        FamilyTreeInvite invite,
        string userId,
        CancellationToken cancellationToken)
    {
        var existing = await _db.FamilyTreeAccesses
            .FirstOrDefaultAsync(a => a.FamilyTreeId == invite.FamilyTreeId && a.UserId == userId, cancellationToken);
        if (existing == null)
        {
            _db.FamilyTreeAccesses.Add(new FamilyTreeAccess
            {
                FamilyTreeId = invite.FamilyTreeId,
                UserId = userId,
                Role = invite.Role,
                GrantedAt = DateTimeOffset.UtcNow,
                GrantedByUserId = invite.CreatedByUserId
            });
            return;
        }

        if (existing.Role < invite.Role)
        {
            // Upgrade only (Readonly -> Editor); never downgrade via a second invite accept.
            existing.Role = invite.Role;
            existing.GrantedAt = DateTimeOffset.UtcNow;
            existing.GrantedByUserId = invite.CreatedByUserId;
        }
    }

    private static void MarkEmailInviteAccepted(FamilyTreeInvite invite, string userId)
    {
        if (invite.IsLinkInvite)
            return;

        invite.AcceptedAt = DateTimeOffset.UtcNow;
        invite.AcceptedByUserId = userId;
    }

    private async Task EnsureOwnerAsync(long treeId, string userId, CancellationToken cancellationToken)
    {
        if (!await _access.CanManageSharingAsync(userId, treeId, cancellationToken))
            throw new UnauthorizedAccessException("Only the tree owner can manage sharing.");
    }

    private static FamilyTreeInvite NewInvite(
        long treeId,
        string createdByUserId,
        TreeShareRole role,
        string? email,
        DateTimeOffset? expiresAt)
    {
        return new FamilyTreeInvite
        {
            FamilyTreeId = treeId,
            Token = GenerateToken(),
            Role = role,
            Email = email,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
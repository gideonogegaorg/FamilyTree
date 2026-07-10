using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Services;

public enum InviteAcceptResult
{
    Success,
    NotFound,
    Expired,
    Revoked,
    EmailMismatch,
    AlreadyOwner,
}

public interface IFamilyTreeShareService
{
    Task<FamilyTreeInvite> CreateLinkInviteAsync(long treeId, string createdByUserId, TreeShareRole role, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default);
    Task<FamilyTreeInvite> CreateEmailInviteAsync(long treeId, string createdByUserId, string email, TreeShareRole role, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default);
    Task<(InviteAcceptResult Result, long? TreeId)> AcceptInviteAsync(string token, string userId, string? userEmail, CancellationToken cancellationToken = default);
    Task<bool> RevokeInviteAsync(long inviteId, string ownerUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveCollaboratorAsync(long treeId, string collaboratorUserId, string ownerUserId, CancellationToken cancellationToken = default);
    Task<bool> ChangeCollaboratorRoleAsync(long treeId, string collaboratorUserId, TreeShareRole role, string ownerUserId, CancellationToken cancellationToken = default);
    Task<FamilyTreeInvite?> ResendEmailInviteAsync(long inviteId, string ownerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FamilyTreeAccess>> GetCollaboratorsAsync(long treeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FamilyTreeInvite>> GetPendingInvitesAsync(long treeId, CancellationToken cancellationToken = default);
}
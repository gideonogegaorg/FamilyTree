using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Services;

public interface IFamilyTreeAccessService
{
    Task<TreeAccessLevel> GetAccessLevelAsync(string userId, long treeId, CancellationToken cancellationToken = default);
    Task<TreeAccessLevel> GetAccessLevelForMemberAsync(string userId, long memberId, CancellationToken cancellationToken = default);
    Task<bool> CanViewAsync(string userId, long treeId, CancellationToken cancellationToken = default);
    Task<bool> CanEditAsync(string userId, long treeId, CancellationToken cancellationToken = default);
    Task<bool> CanManageSharingAsync(string userId, long treeId, CancellationToken cancellationToken = default);
    Task<bool> CanDeleteTreeAsync(string userId, long treeId, CancellationToken cancellationToken = default);

    /// <summary>Trees the user owns or has been granted access to, ordered by name.</summary>
    Task<IReadOnlyList<Data.FamilyTree>> GetAccessibleTreesAsync(string userId, CancellationToken cancellationToken = default);

    // Legacy helpers kept for call sites that still check ownership explicitly.
    Task<bool> UserOwnsTreeAsync(string userId, long treeId, CancellationToken cancellationToken = default);
    Task<bool> UserOwnsMemberAsync(string userId, long memberId, CancellationToken cancellationToken = default);
}
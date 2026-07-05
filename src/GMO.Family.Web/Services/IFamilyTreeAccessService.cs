namespace GMO.Family.Web.Services;

public interface IFamilyTreeAccessService
{
    Task<bool> UserOwnsTreeAsync(string userId, long treeId, CancellationToken cancellationToken = default);
    Task<bool> UserOwnsMemberAsync(string userId, long memberId, CancellationToken cancellationToken = default);
}
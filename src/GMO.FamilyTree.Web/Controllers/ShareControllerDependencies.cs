using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Identity;

namespace GMO.FamilyTree.Web.Controllers;

public sealed class ShareControllerDependencies
{
    public ShareControllerDependencies(
        AppDbContext db,
        UserManager<IdentityUser> userManager,
        IFamilyTreeAccessService access,
        IFamilyTreeShareService share,
        ICurrentFamilyTreeService currentTree,
        IEmailSender emailSender,
        IEmailRateLimiter emailRateLimiter)
    {
        Db = db;
        UserManager = userManager;
        Access = access;
        Share = share;
        CurrentTree = currentTree;
        EmailSender = emailSender;
        EmailRateLimiter = emailRateLimiter;
    }

    public AppDbContext Db { get; }
    public UserManager<IdentityUser> UserManager { get; }
    public IFamilyTreeAccessService Access { get; }
    public IFamilyTreeShareService Share { get; }
    public ICurrentFamilyTreeService CurrentTree { get; }
    public IEmailSender EmailSender { get; }
    public IEmailRateLimiter EmailRateLimiter { get; }
}
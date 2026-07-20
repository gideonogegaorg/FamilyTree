using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Controllers;

public sealed class AccountControllerDependencies
{
    public AccountControllerDependencies(
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
        IExternalLoginInfoProvider externalLoginInfo,
        IPhotoStorageService photos,
        ITreeCardViewModeService treeCardViewMode,
        IFamilyTreeAccessService access,
        IEmailRateLimiter emailRateLimiter)
    {
        SignInManager = signInManager;
        UserManager = userManager;
        EmailSender = emailSender;
        GoogleAuth = googleAuth;
        Db = db;
        CurrentFamilyTree = currentFamilyTree;
        TreeViewOrientation = treeViewOrientation;
        LineageMode = lineageMode;
        DefaultFamilyTree = defaultFamilyTree;
        FamilyTreeDeletion = familyTreeDeletion;
        ExternalLoginInfo = externalLoginInfo;
        Photos = photos;
        TreeCardViewMode = treeCardViewMode;
        Access = access;
        EmailRateLimiter = emailRateLimiter;
    }

    public SignInManager<IdentityUser> SignInManager { get; }
    public UserManager<IdentityUser> UserManager { get; }
    public IEmailSender EmailSender { get; }
    public IOptionsMonitor<GoogleAuthOptions> GoogleAuth { get; }
    public AppDbContext Db { get; }
    public ICurrentFamilyTreeService CurrentFamilyTree { get; }
    public ITreeViewOrientationService TreeViewOrientation { get; }
    public ILineageModeService LineageMode { get; }
    public IDefaultFamilyTreeService DefaultFamilyTree { get; }
    public IFamilyTreeDeletionService FamilyTreeDeletion { get; }
    public IExternalLoginInfoProvider ExternalLoginInfo { get; }
    public IPhotoStorageService Photos { get; }
    public ITreeCardViewModeService TreeCardViewMode { get; }
    public IFamilyTreeAccessService Access { get; }
    public IEmailRateLimiter EmailRateLimiter { get; }
}
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Controllers;

public sealed record AccountIdentityDependencies(
    SignInManager<IdentityUser> SignInManager,
    UserManager<IdentityUser> UserManager);

public sealed record AccountFamilyTreeDependencies(
    ICurrentFamilyTreeService CurrentFamilyTree,
    ITreeViewOrientationService TreeViewOrientation,
    ILineageModeService LineageMode,
    IDefaultFamilyTreeService DefaultFamilyTree,
    IFamilyTreeDeletionService FamilyTreeDeletion,
    IFamilyTreeAccessService Access,
    ITreeCardViewModeService TreeCardViewMode);

public sealed record AccountEmailDependencies(
    IEmailSender EmailSender,
    IEmailRateLimiter EmailRateLimiter);

public sealed class AccountControllerDependencies
{
    public AccountControllerDependencies(
        AccountIdentityDependencies identity,
        AccountFamilyTreeDependencies familyTree,
        AccountEmailDependencies email,
        AppDbContext db,
        IOptionsMonitor<GoogleAuthOptions> googleAuth,
        IExternalLoginInfoProvider externalLoginInfo,
        IPhotoStorageService photos)
    {
        SignInManager = identity.SignInManager;
        UserManager = identity.UserManager;
        EmailSender = email.EmailSender;
        GoogleAuth = googleAuth;
        Db = db;
        CurrentFamilyTree = familyTree.CurrentFamilyTree;
        TreeViewOrientation = familyTree.TreeViewOrientation;
        LineageMode = familyTree.LineageMode;
        DefaultFamilyTree = familyTree.DefaultFamilyTree;
        FamilyTreeDeletion = familyTree.FamilyTreeDeletion;
        ExternalLoginInfo = externalLoginInfo;
        Photos = photos;
        TreeCardViewMode = familyTree.TreeCardViewMode;
        Access = familyTree.Access;
        EmailRateLimiter = email.EmailRateLimiter;
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
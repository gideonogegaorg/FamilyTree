using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;

using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Controllers;

public sealed class HomeControllerDependencies
{
    public HomeControllerDependencies(
        AppDbContext db,
        ICurrentFamilyTreeService currentTree,
        ITreeViewOrientationService treeViewOrientation,
        ILineageModeService lineageMode,
        ITreeCardViewModeService treeCardViewMode,
        IFamilyTreeAccessService access,
        IOptionsMonitor<GoogleAuthOptions> googleAuth,
        IWebHostEnvironment env)
    {
        Db = db;
        CurrentTree = currentTree;
        TreeViewOrientation = treeViewOrientation;
        LineageMode = lineageMode;
        TreeCardViewMode = treeCardViewMode;
        Access = access;
        GoogleAuth = googleAuth;
        Env = env;
    }

    public AppDbContext Db { get; }
    public ICurrentFamilyTreeService CurrentTree { get; }
    public ITreeViewOrientationService TreeViewOrientation { get; }
    public ILineageModeService LineageMode { get; }
    public ITreeCardViewModeService TreeCardViewMode { get; }
    public IFamilyTreeAccessService Access { get; }
    public IOptionsMonitor<GoogleAuthOptions> GoogleAuth { get; }
    public IWebHostEnvironment Env { get; }
}
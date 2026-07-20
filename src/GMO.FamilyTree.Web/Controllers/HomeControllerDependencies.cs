using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;

using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Controllers;

public sealed record HomeTreeViewDependencies(
    ITreeViewOrientationService TreeViewOrientation,
    ILineageModeService LineageMode,
    ITreeCardViewModeService TreeCardViewMode);

public sealed class HomeControllerDependencies
{
    public HomeControllerDependencies(
        AppDbContext db,
        ICurrentFamilyTreeService currentTree,
        HomeTreeViewDependencies treeView,
        IFamilyTreeAccessService access,
        IOptionsMonitor<GoogleAuthOptions> googleAuth,
        IWebHostEnvironment env)
    {
        Db = db;
        CurrentTree = currentTree;
        TreeViewOrientation = treeView.TreeViewOrientation;
        LineageMode = treeView.LineageMode;
        TreeCardViewMode = treeView.TreeCardViewMode;
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
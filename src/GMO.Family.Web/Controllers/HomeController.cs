using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

using GMO.Family.Web.Data;
using GMO.Family.Web.Models;
using GMO.Family.Web.Options;
using GMO.Family.Web.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GMO.Family.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentFamilyTreeService _currentTree;
    private readonly ITreeViewOrientationService _treeViewOrientation;
    private readonly ITreePathModeService _treePathMode;
    private readonly IOptionsMonitor<GoogleAuthOptions> _googleAuth;

    public HomeController(AppDbContext db, ICurrentFamilyTreeService currentTree, ITreeViewOrientationService treeViewOrientation, ITreePathModeService treePathMode, IOptionsMonitor<GoogleAuthOptions> googleAuth)
    {
        _db = db;
        _currentTree = currentTree;
        _treeViewOrientation = treeViewOrientation;
        _treePathMode = treePathMode;
        _googleAuth = googleAuth;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.GoogleAuthEnabled = _googleAuth.CurrentValue.Enabled;

        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue)
        {
            return RedirectToAction(nameof(FamilyTreeController.Index), "FamilyTree");
        }

        var tree = await _db.FamilyTrees.FindAsync(new object[] { treeId.Value }, cancellationToken);
        if (tree == null)
        {
            return RedirectToAction(nameof(FamilyTreeController.Index), "FamilyTree");
        }

        var members = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyTreeId == treeId.Value)
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var rels = await _db.FamilyMemberRelationships
            .AsNoTracking()
            .Where(r => r.FamilyTreeId == treeId.Value)
            .ToListAsync(cancellationToken);

        var memberDict = members.ToDictionary(m => m.Id);
        var childIds = rels.Where(r => r.RelationshipType == RelationshipType.Parent).Select(r => r.ToMemberId).ToHashSet();
        var rootIds = members.Where(m => !childIds.Contains(m.Id)).Select(m => m.Id).ToList();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var meMember = members.FirstOrDefault(m => m.UserId == currentUserId);
        var focusMemberId = meMember?.Id ?? rootIds.FirstOrDefault();

        var cards = members.Select(m => BuildCard(m, rels, memberDict, currentUserId)).ToList();
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        var pathMode = await _treePathMode.GetAsync(cancellationToken);
        var rankById = TreeLayoutRanking.ComputeVisualRank(cards, rowById, pathMode);

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var nodesJson = JsonSerializer.Serialize(cards.Select(c => new
        {
            c.Id,
            Label = string.IsNullOrEmpty(c.NickName) ? c.Name : $"{c.Name} ({c.NickName})",
            c.IsMe,
            Dob = c.DOB?.ToString("yyyy-MM-dd"),
            Row = rowById.TryGetValue(c.Id, out var row) ? row : 0,
            VisualRank = rankById.TryGetValue(c.Id, out var rank) ? rank : 0.0,
            ParentIds = c.ParentIds,
            ChildIds = c.ChildIds,
            PartnerIds = c.PartnerIds,
            c.BirthOrder,
            c.IsMale
        }), jsonOpts);

        var edgesJson = JsonSerializer.Serialize(rels.Select(r => new
        {
            r.Id,
            Source = r.FromMemberId,
            Target = r.ToMemberId,
            Type = r.RelationshipType switch
            {
                RelationshipType.Parent => "parent",
                RelationshipType.Couple => "couple",
                _ => "unknown"
            }
        }), jsonOpts);

        var orientation = await _treeViewOrientation.GetOrientationAsync(cancellationToken);

        var model = new FamilyTreeGraphViewModel
        {
            TreeId = tree.Id,
            TreeName = tree.Name,
            CurrentUserId = currentUserId,
            FocusMemberId = focusMemberId,
            TreeViewOrientation = orientation,
            TreePathMode = pathMode,
            Members = cards,
            NodesJson = nodesJson,
            EdgesJson = edgesJson
        };

        return View(model);
    }

    public async Task<IActionResult> AddFirstMember(CancellationToken cancellationToken)
    {
        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue) return RedirectToAction(nameof(FamilyTreeController.Index), "FamilyTree");
        var tree = await _db.FamilyTrees.FindAsync(new object[] { treeId.Value }, cancellationToken);
        if (tree == null) return RedirectToAction(nameof(FamilyTreeController.Index), "FamilyTree");
        return View(new AddRelationViewModel { FamilyTreeId = treeId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFirstMember(AddRelationViewModel model, CancellationToken cancellationToken)
    {
        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue || model.FamilyTreeId != treeId.Value)
            return RedirectToAction(nameof(Index));
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
            return View(model);
        }
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (model.SetAsMe && !string.IsNullOrEmpty(currentUserId))
        {
            var others = await _db.FamilyMembers
                .Where(m => m.FamilyTreeId == model.FamilyTreeId && m.UserId == currentUserId)
                .ToListAsync(cancellationToken);
            foreach (var m in others)
                m.UserId = null;
        }
        _db.FamilyMembers.Add(new FamilyMember
        {
            FamilyTreeId = model.FamilyTreeId,
            Name = model.Name.Trim(),
            NickName = string.IsNullOrWhiteSpace(model.NickName) ? null : model.NickName.Trim(),
            DOB = model.DOB,
            IsMale = model.IsMale,
            UserId = model.SetAsMe ? currentUserId : null
        });
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private static FamilyMemberCardViewModel BuildCard(
        FamilyMember m,
        List<FamilyMemberRelationship> rels,
        Dictionary<long, FamilyMember> memberDict,
        string? currentUserId)
    {
        var parentIds = rels.Where(r => r.RelationshipType == RelationshipType.Parent && r.ToMemberId == m.Id).Select(r => r.FromMemberId).ToList();
        var childIds = rels.Where(r => r.RelationshipType == RelationshipType.Parent && r.FromMemberId == m.Id).Select(r => r.ToMemberId).ToList();
        var partnerIds = rels.Where(r => r.RelationshipType == RelationshipType.Couple && (r.FromMemberId == m.Id || r.ToMemberId == m.Id))
            .Select(r => r.FromMemberId == m.Id ? r.ToMemberId : r.FromMemberId).ToList();

        return new FamilyMemberCardViewModel
        {
            Id = m.Id,
            Name = m.Name,
            NickName = m.NickName,
            DOB = m.DOB,
            BirthOrder = m.BirthOrder,
            IsMe = m.UserId == currentUserId,
            IsMale = m.IsMale,
            ParentIds = parentIds,
            ChildIds = childIds,
            SiblingIds = new List<long>(),
            PartnerIds = partnerIds
        };
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
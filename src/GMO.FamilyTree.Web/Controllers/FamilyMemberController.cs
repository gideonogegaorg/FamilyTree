using System.Security.Claims;

using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Controllers;

[Authorize]
public class FamilyMemberController : Controller
{
    private const string InvalidInputMessage = "Invalid input.";
    private const string NotFoundMessage = "Not found";

    private readonly AppDbContext _db;
    private readonly ICurrentFamilyTreeService _currentTree;
    private readonly IPhotoStorageService _photos;
    private readonly IFamilyTreeAccessService _access;

    public FamilyMemberController(
        AppDbContext db,
        ICurrentFamilyTreeService currentTree,
        IPhotoStorageService photos,
        IFamilyTreeAccessService access)
    {
        _db = db;
        _currentTree = currentTree;
        _photos = photos;
        _access = access;
    }

    public async Task<IActionResult> AddRelation(long memberId, RelationshipType type, bool isChild = false, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue) return RedirectToAction(nameof(HomeController.Index), "Home");
        if (!await EnsureCanEditAsync(treeId.Value, cancellationToken))
            return Forbid();

        var tree = await _db.FamilyTrees.FindAsync(new object[] { treeId.Value }, cancellationToken);
        if (tree == null) return RedirectToAction(nameof(HomeController.Index), "Home");

        var member = await _db.FamilyMembers.FindAsync(new object[] { memberId }, cancellationToken);
        if (member == null || member.FamilyTreeId != treeId.Value) return NotFound();

        var model = new AddRelationViewModel
        {
            ContextMemberId = memberId,
            FamilyTreeId = treeId.Value,
            RelationshipType = type,
            IsChild = isChild
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRelation(AddRelationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue || model.FamilyTreeId != treeId.Value)
            return RedirectToAction(nameof(HomeController.Index), "Home");
        if (!await EnsureCanEditAsync(treeId.Value, cancellationToken))
            return Forbid();

        var contextMember = await _db.FamilyMembers.FindAsync(new object[] { model.ContextMemberId }, cancellationToken);
        if (contextMember == null || contextMember.FamilyTreeId != treeId.Value)
            return NotFound();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
            return View(model);
        }

        var parentValidationError = await ValidateParentLimitAsync(model, contextMember, treeId.Value, cancellationToken);
        if (parentValidationError != null)
        {
            ModelState.AddModelError("", parentValidationError);
            return View(model);
        }

        await ApplySetAsMeForNewMemberAsync(model, cancellationToken);

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var newMember = CreateMemberFromModel(model, currentUserId);
        var relationshipAdded = await TryAddNewMemberRelationshipAsync(model, contextMember, newMember, cancellationToken);
        if (!relationshipAdded)
            return BadRequest();

        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    private async Task<string?> ValidateParentLimitAsync(
        AddRelationViewModel model,
        FamilyMember contextMember,
        long treeId,
        CancellationToken cancellationToken)
    {
        if (model.RelationshipType != RelationshipType.Parent || model.IsChild)
            return null;

        var parentCount = await _db.FamilyMemberRelationships
            .CountAsync(r => r.FamilyTreeId == treeId && r.RelationshipType == RelationshipType.Parent && r.ToMemberId == contextMember.Id, cancellationToken);
        return parentCount >= 2
            ? "This person already has two parents. Link an existing member instead if needed."
            : null;
    }

    private async Task ApplySetAsMeForNewMemberAsync(AddRelationViewModel model, CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!model.SetAsMe || string.IsNullOrEmpty(currentUserId))
            return;

        var others = await _db.FamilyMembers
            .Where(m => m.FamilyTreeId == model.FamilyTreeId && m.UserId == currentUserId)
            .ToListAsync(cancellationToken);
        foreach (var m in others)
            m.UserId = null;
    }

    private static FamilyMember CreateMemberFromModel(AddRelationViewModel model, string? currentUserId)
    {
        return new FamilyMember
        {
            FamilyTreeId = model.FamilyTreeId,
            Name = model.Name.Trim(),
            NickName = string.IsNullOrWhiteSpace(model.NickName) ? null : model.NickName.Trim(),
            DOB = model.DOB,
            DOD = model.DOD,
            IsMale = model.IsMale,
            UserId = model.SetAsMe ? currentUserId : null,
            BirthOrder = model.RelationshipType is RelationshipType.Parent or RelationshipType.Couple
                ? null
                : model.BirthOrder
        };
    }

    private async Task<bool> TryAddNewMemberRelationshipAsync(
        AddRelationViewModel model,
        FamilyMember contextMember,
        FamilyMember newMember,
        CancellationToken cancellationToken)
    {
        switch (model.RelationshipType!.Value)
        {
            case RelationshipType.Parent:
                _db.FamilyMembers.Add(newMember);
                await _db.SaveChangesAsync(cancellationToken);
                _db.FamilyMemberRelationships.Add(CreateParentRelationship(model, contextMember, newMember));
                return true;
            case RelationshipType.Couple:
                newMember.BirthOrder = null;
                _db.FamilyMembers.Add(newMember);
                await _db.SaveChangesAsync(cancellationToken);
                _db.FamilyMemberRelationships.Add(new FamilyMemberRelationship
                {
                    FamilyTreeId = model.FamilyTreeId,
                    FromMemberId = contextMember.Id,
                    ToMemberId = newMember.Id,
                    RelationshipType = RelationshipType.Couple
                });
                return true;
            default:
                return false;
        }
    }

    private static FamilyMemberRelationship CreateParentRelationship(
        AddRelationViewModel model,
        FamilyMember contextMember,
        FamilyMember newMember)
    {
        return model.IsChild
            ? new FamilyMemberRelationship
            {
                FamilyTreeId = model.FamilyTreeId,
                FromMemberId = contextMember.Id,
                ToMemberId = newMember.Id,
                RelationshipType = RelationshipType.Parent
            }
            : new FamilyMemberRelationship
            {
                FamilyTreeId = model.FamilyTreeId,
                FromMemberId = newMember.Id,
                ToMemberId = contextMember.Id,
                RelationshipType = RelationshipType.Parent
            };
    }

    public async Task<IActionResult> ActionMenuContent(long memberId, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue) return NotFound();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !await _access.CanViewAsync(userId, treeId.Value, cancellationToken))
            return NotFound();
        if (!await _access.CanEditAsync(userId, treeId.Value, cancellationToken))
            return Content("<div class=\"cascading-menu p-2 text-muted small\">View only — you cannot edit this tree.</div>", "text/html");

        var member = await _db.FamilyMembers.FindAsync(new object[] { memberId }, cancellationToken);
        if (member == null || member.FamilyTreeId != treeId.Value) return NotFound();

        var model = await BuildActionMenuViewModelAsync(memberId, member, treeId.Value, cancellationToken);
        return PartialView("_ActionMenuContent", model);
    }

    private async Task<MemberActionMenuViewModel> BuildActionMenuViewModelAsync(
        long memberId,
        FamilyMember member,
        long treeId,
        CancellationToken cancellationToken)
    {
        var allInTree = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyTreeId == treeId && m.Id != memberId)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
        var allMembersInTree = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyTreeId == treeId)
            .ToListAsync(cancellationToken);
        var memberNames = allMembersInTree.ToDictionary(m => m.Id, m => string.IsNullOrEmpty(m.NickName) ? m.Name : $"{m.Name} ({m.NickName})");
        var rels = await _db.FamilyMemberRelationships
            .AsNoTracking()
            .Where(r => r.FamilyTreeId == treeId && (r.FromMemberId == memberId || r.ToMemberId == memberId))
            .ToListAsync(cancellationToken);

        var existingRels = BuildExistingRelationships(memberId, rels, memberNames);
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var parentIds = GetExistingParentIds(memberId, rels);

        return new MemberActionMenuViewModel
        {
            ContextMemberId = memberId,
            FamilyTreeId = treeId,
            ContextMemberName = member.Name,
            CanAddParent = parentIds.Count < 2,
            Name = member.Name,
            NickName = member.NickName,
            DOB = member.DOB,
            DOD = member.DOD,
            BirthOrder = member.BirthOrder,
            IsMe = member.UserId == currentUserId,
            IsMale = member.IsMale,
            HasPhoto = !string.IsNullOrEmpty(member.PhotoKey),
            ExistingRelationships = existingRels,
            ParentCandidates = allInTree.Where(m => !parentIds.Contains(m.Id)).Select(m => new LinkExistingCandidateViewModel { Id = m.Id, DisplayName = memberNames.TryGetValue(m.Id, out var dn) ? dn : m.Name }).ToList(),
            ChildCandidates = allInTree.Where(m => !GetExistingChildIds(memberId, rels).Contains(m.Id)).Select(m => new LinkExistingCandidateViewModel { Id = m.Id, DisplayName = memberNames.TryGetValue(m.Id, out var dn) ? dn : m.Name }).ToList(),
            PartnerCandidates = allInTree.Where(m => !GetExistingPartnerIds(memberId, rels).Contains(m.Id)).Select(m => new LinkExistingCandidateViewModel { Id = m.Id, DisplayName = memberNames.TryGetValue(m.Id, out var dn) ? dn : m.Name }).ToList()
        };
    }

    private static List<ExistingRelationshipViewModel> BuildExistingRelationships(
        long memberId,
        List<FamilyMemberRelationship> rels,
        Dictionary<long, string> memberNames)
    {
        var existingRels = new List<ExistingRelationshipViewModel>();
        foreach (var r in rels)
        {
            var otherId = r.FromMemberId == memberId ? r.ToMemberId : r.FromMemberId;
            var otherName = memberNames.TryGetValue(otherId, out var n) ? n : "?";
            var label = r.RelationshipType switch
            {
                RelationshipType.Parent => r.ToMemberId == memberId ? "Parent: " + otherName : "Child: " + otherName,
                RelationshipType.Couple => "Partner: " + otherName,
                _ => otherName
            };
            existingRels.Add(new ExistingRelationshipViewModel { RelationshipId = r.Id, Label = label });
        }

        return existingRels;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRelation(long relationshipId, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, error = InvalidInputMessage });

        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue) return Json(new { success = false, error = "No tree" });
        if (!await EnsureCanEditAsync(treeId.Value, cancellationToken))
            return Json(new { success = false, error = "Forbidden" });
        var rel = await _db.FamilyMemberRelationships.FindAsync(new object[] { relationshipId }, cancellationToken);
        if (rel == null || rel.FamilyTreeId != treeId.Value) return Json(new { success = false, error = NotFoundMessage });

        var otherId = rel.FromMemberId;
        var contextId = rel.ToMemberId;
        _db.FamilyMemberRelationships.Remove(rel);
        await _db.SaveChangesAsync(cancellationToken);

        var orphanPhotoKeys = new List<string?>();
        foreach (var candidateId in new[] { otherId, contextId })
        {
            var hasRels = await _db.FamilyMemberRelationships.AnyAsync(
                r => r.FamilyTreeId == treeId.Value && (r.FromMemberId == candidateId || r.ToMemberId == candidateId), cancellationToken);
            if (!hasRels)
            {
                var orphan = await _db.FamilyMembers.FindAsync(new object[] { candidateId }, cancellationToken);
                if (orphan != null && orphan.FamilyTreeId == treeId.Value && string.IsNullOrEmpty(orphan.UserId))
                {
                    orphanPhotoKeys.Add(orphan.PhotoKey);
                    _db.FamilyMembers.Remove(orphan);
                }
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        await PhotoStorageHelper.DeleteManyAsync(_photos, orphanPhotoKeys, cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMember([FromForm] EditMemberRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return EditMemberError(InvalidInputMessage);

        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue) return EditMemberError("No tree");
        if (!await EnsureCanEditAsync(treeId.Value, cancellationToken))
            return EditMemberError("Forbidden");
        var member = await _db.FamilyMembers.FindAsync(new object[] { request.MemberId }, cancellationToken);
        if (member == null || member.FamilyTreeId != treeId.Value) return EditMemberError(NotFoundMessage);
        if (string.IsNullOrWhiteSpace(request.Name)) return EditMemberError("Name is required");
        if (request.Dob.HasValue && request.Dod.HasValue && request.Dod < request.Dob)
            return EditMemberError("Date of death cannot be before date of birth.");

        member.Name = request.Name.Trim();
        member.NickName = string.IsNullOrWhiteSpace(request.NickName) ? null : request.NickName.Trim();
        member.DOB = request.Dob;
        member.DOD = request.Dod;
        member.BirthOrder = request.BirthOrder;
        member.IsMale = request.IsMale ?? false;

        await ApplySetAsMeAsync(request.MemberId, member, request.SetAsMe ?? false, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    private JsonResult EditMemberError(string error) => Json(new { success = false, error });

    private async Task ApplySetAsMeAsync(long memberId, FamilyMember member, bool setAsMe, CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (setAsMe && !string.IsNullOrEmpty(currentUserId))
        {
            var others = await _db.FamilyMembers
                .Where(m => m.FamilyTreeId == member.FamilyTreeId && m.UserId == currentUserId && m.Id != memberId)
                .ToListAsync(cancellationToken);
            foreach (var m in others) m.UserId = null;
            member.UserId = currentUserId;
            return;
        }

        if (!setAsMe && member.UserId == currentUserId)
            member.UserId = null;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadMemberPhoto(long memberId, IFormFile? photo, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, error = InvalidInputMessage });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, error = "Unauthorized" });
        var level = await _access.GetAccessLevelForMemberAsync(userId, memberId, cancellationToken);
        if (level < TreeAccessLevel.Editor)
            return Json(new { success = false, error = NotFoundMessage });
        if (photo == null || photo.Length == 0)
            return Json(new { success = false, error = "Please select an image file." });

        var ext = PhotoStorageKeys.NormalizeExtension(photo.FileName);
        if (ext == null)
            return Json(new { success = false, error = "Allowed formats: JPG, PNG, GIF, WebP." });

        var member = await _db.FamilyMembers.FindAsync(new object[] { memberId }, cancellationToken);
        if (member == null)
            return Json(new { success = false, error = NotFoundMessage });

        var key = PhotoStorageKeys.Member(member.FamilyTreeId, memberId, ext);
        try
        {
            await using var stream = photo.OpenReadStream();
            await PhotoStorageHelper.SaveAsync(_photos, key, stream, PhotoStorageKeys.ContentTypeForExtension(ext), cancellationToken);
        }
        catch (Exception ex) when (PhotoStorageHelper.IsStorageException(ex))
        {
            return Json(new { success = false, error = PhotoStorageHelper.StorageUnavailableMessage });
        }

        var previousKey = member.PhotoKey;
        member.PhotoKey = key;
        await _db.SaveChangesAsync(cancellationToken);
        await PhotoStorageHelper.TryDeleteAsync(_photos, previousKey != key ? previousKey : null, cancellationToken);
        return Json(new { success = true, photoUrl = $"/photos/members/{memberId}" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMemberPhoto(long memberId, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, error = InvalidInputMessage });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, error = "Unauthorized" });
        var level = await _access.GetAccessLevelForMemberAsync(userId, memberId, cancellationToken);
        if (level < TreeAccessLevel.Editor)
            return Json(new { success = false, error = NotFoundMessage });

        var member = await _db.FamilyMembers.FindAsync(new object[] { memberId }, cancellationToken);
        if (member == null || string.IsNullOrEmpty(member.PhotoKey))
            return Json(new { success = true });

        var previousKey = member.PhotoKey;
        member.PhotoKey = null;
        await _db.SaveChangesAsync(cancellationToken);
        await PhotoStorageHelper.TryDeleteAsync(_photos, previousKey, cancellationToken);
        return Json(new { success = true });
    }

    public async Task<IActionResult> LinkExisting(long memberId, RelationshipType type, bool isChild = false, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue) return RedirectToAction(nameof(HomeController.Index), "Home");
        if (!await EnsureCanEditAsync(treeId.Value, cancellationToken))
            return Forbid();
        var tree = await _db.FamilyTrees.FindAsync(new object[] { treeId.Value }, cancellationToken);
        if (tree == null) return RedirectToAction(nameof(HomeController.Index), "Home");
        var member = await _db.FamilyMembers.FindAsync(new object[] { memberId }, cancellationToken);
        if (member == null || member.FamilyTreeId != treeId.Value) return NotFound();

        var allInTree = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyTreeId == treeId.Value && m.Id != memberId)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);

        var rels = await _db.FamilyMemberRelationships
            .AsNoTracking()
            .Where(r => r.FamilyTreeId == treeId.Value)
            .ToListAsync(cancellationToken);

        HashSet<long> existingIds = type switch
        {
            RelationshipType.Parent => GetExistingParentIds(memberId, rels),
            RelationshipType.Couple => GetExistingPartnerIds(memberId, rels),
            _ => new HashSet<long>()
        };

        if (type == RelationshipType.Parent && !isChild && existingIds.Count >= 2)
            return RedirectToAction(nameof(HomeController.Index), "Home");

        if (type == RelationshipType.Parent && isChild)
            existingIds = GetExistingChildIds(memberId, rels);

        var candidates = allInTree
            .Where(m => !existingIds.Contains(m.Id))
            .Select(m => new LinkExistingCandidateViewModel { Id = m.Id, DisplayName = string.IsNullOrEmpty(m.NickName) ? m.Name : $"{m.Name} ({m.NickName})" })
            .ToList();

        var actionLabel = (type, isChild) switch
        {
            (RelationshipType.Parent, false) => "Link as Parent",
            (RelationshipType.Parent, true) => "Link as Child",
            (RelationshipType.Couple, _) => "Link as Partner",
            _ => "Link"
        };

        var model = new LinkExistingViewModel
        {
            ContextMemberId = memberId,
            ContextMemberName = member.Name,
            FamilyTreeId = treeId.Value,
            RelationshipType = type,
            IsChild = isChild,
            ActionLabel = actionLabel,
            Candidates = candidates
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkExisting(LinkExistingViewModel model, CancellationToken cancellationToken = default)
    {
        var treeId = await _currentTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        if (!treeId.HasValue || model.FamilyTreeId != treeId.Value)
            return RedirectToAction(nameof(HomeController.Index), "Home");
        if (!await EnsureCanEditAsync(treeId.Value, cancellationToken))
            return Forbid();

        if (!ModelState.IsValid)
        {
            await RepopulateLinkExistingCandidatesAsync(model, cancellationToken);
            return View(model);
        }

        var validationResult = await ValidateLinkExistingAsync(model, treeId.Value, cancellationToken);
        if (validationResult != null)
            return validationResult;

        var contextMember = await _db.FamilyMembers.FindAsync(new object[] { model.ContextMemberId }, cancellationToken);
        var existingMember = await _db.FamilyMembers.FindAsync(new object[] { model.ExistingMemberId!.Value }, cancellationToken);
        AddLinkExistingRelationship(model, contextMember!, existingMember!);
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    private async Task<IActionResult?> ValidateLinkExistingAsync(
        LinkExistingViewModel model,
        long treeId,
        CancellationToken cancellationToken)
    {
        if (!model.ExistingMemberId.HasValue)
        {
            ModelState.AddModelError(nameof(model.ExistingMemberId), "Please select a person to link.");
            await RepopulateLinkExistingCandidatesAsync(model, cancellationToken);
            return View(model);
        }

        var contextMember = await _db.FamilyMembers.FindAsync(new object[] { model.ContextMemberId }, cancellationToken);
        var existingMember = await _db.FamilyMembers.FindAsync(new object[] { model.ExistingMemberId.Value }, cancellationToken);
        if (contextMember == null || existingMember == null || contextMember.FamilyTreeId != treeId || existingMember.FamilyTreeId != treeId)
            return NotFound();
        if (contextMember.Id == existingMember.Id)
            return BadRequest();

        if (model.RelationshipType == RelationshipType.Parent && !model.IsChild)
        {
            var parentCount = await _db.FamilyMemberRelationships
                .CountAsync(r => r.FamilyTreeId == treeId && r.RelationshipType == RelationshipType.Parent && r.ToMemberId == contextMember.Id, cancellationToken);
            if (parentCount >= 2)
            {
                ModelState.AddModelError("", "This person already has two parents.");
                await RepopulateLinkExistingCandidatesAsync(model, cancellationToken);
                return View(model);
            }
        }

        if (await RelationshipExistsAsync(model.FamilyTreeId, model.ContextMemberId, model.ExistingMemberId.Value, model.RelationshipType!.Value, model.IsChild, cancellationToken))
        {
            ModelState.AddModelError("", "This relationship already exists.");
            await RepopulateLinkExistingCandidatesAsync(model, cancellationToken);
            return View(model);
        }

        return null;
    }

    private void AddLinkExistingRelationship(LinkExistingViewModel model, FamilyMember contextMember, FamilyMember existingMember)
    {
        switch (model.RelationshipType!.Value)
        {
            case RelationshipType.Parent:
                if (model.IsChild)
                    _db.FamilyMemberRelationships.Add(new FamilyMemberRelationship { FamilyTreeId = model.FamilyTreeId, FromMemberId = contextMember.Id, ToMemberId = existingMember.Id, RelationshipType = RelationshipType.Parent });
                else
                    _db.FamilyMemberRelationships.Add(new FamilyMemberRelationship { FamilyTreeId = model.FamilyTreeId, FromMemberId = existingMember.Id, ToMemberId = contextMember.Id, RelationshipType = RelationshipType.Parent });
                break;
            case RelationshipType.Couple:
                _db.FamilyMemberRelationships.Add(new FamilyMemberRelationship { FamilyTreeId = model.FamilyTreeId, FromMemberId = contextMember.Id, ToMemberId = existingMember.Id, RelationshipType = RelationshipType.Couple });
                break;
            default:
                throw new InvalidOperationException("Unsupported relationship type.");
        }
    }

    private async Task<bool> EnsureCanEditAsync(long treeId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) && await _access.CanEditAsync(userId, treeId, cancellationToken);
    }

    private static HashSet<long> GetExistingParentIds(long memberId, List<FamilyMemberRelationship> rels) =>
        rels.Where(r => r.RelationshipType == RelationshipType.Parent && r.ToMemberId == memberId).Select(r => r.FromMemberId).ToHashSet();
    private static HashSet<long> GetExistingChildIds(long memberId, List<FamilyMemberRelationship> rels) =>
        rels.Where(r => r.RelationshipType == RelationshipType.Parent && r.FromMemberId == memberId).Select(r => r.ToMemberId).ToHashSet();
    private static HashSet<long> GetExistingPartnerIds(long memberId, List<FamilyMemberRelationship> rels) =>
        rels.Where(r => r.RelationshipType == RelationshipType.Couple && (r.FromMemberId == memberId || r.ToMemberId == memberId)).Select(r => r.FromMemberId == memberId ? r.ToMemberId : r.FromMemberId).ToHashSet();

    private async Task RepopulateLinkExistingCandidatesAsync(LinkExistingViewModel model, CancellationToken ct)
    {
        var allInTree = await _db.FamilyMembers.AsNoTracking().Where(m => m.FamilyTreeId == model.FamilyTreeId && m.Id != model.ContextMemberId).OrderBy(m => m.Name).ToListAsync(ct);
        var rels = await _db.FamilyMemberRelationships.AsNoTracking().Where(r => r.FamilyTreeId == model.FamilyTreeId).ToListAsync(ct);
        HashSet<long> existingIds = model.RelationshipType!.Value switch
        {
            RelationshipType.Parent => GetExistingParentIds(model.ContextMemberId, rels),
            RelationshipType.Couple => GetExistingPartnerIds(model.ContextMemberId, rels),
            _ => new HashSet<long>()
        };
        if (model.RelationshipType == RelationshipType.Parent && model.IsChild)
            existingIds = GetExistingChildIds(model.ContextMemberId, rels);
        model.Candidates = allInTree.Where(m => !existingIds.Contains(m.Id)).Select(m => new LinkExistingCandidateViewModel { Id = m.Id, DisplayName = string.IsNullOrEmpty(m.NickName) ? m.Name : $"{m.Name} ({m.NickName})" }).ToList();
    }

    private async Task<bool> RelationshipExistsAsync(long treeId, long contextId, long existingId, RelationshipType type, bool isChild, CancellationToken ct)
    {
        if (type == RelationshipType.Couple)
            return await _db.FamilyMemberRelationships.AnyAsync(r => r.FamilyTreeId == treeId && r.RelationshipType == RelationshipType.Couple && ((r.FromMemberId == contextId && r.ToMemberId == existingId) || (r.FromMemberId == existingId && r.ToMemberId == contextId)), ct);

        if (type != RelationshipType.Parent)
            return false;

        return isChild
            ? await _db.FamilyMemberRelationships.AnyAsync(r => r.FamilyTreeId == treeId && r.FromMemberId == contextId && r.ToMemberId == existingId && r.RelationshipType == RelationshipType.Parent, ct)
            : await _db.FamilyMemberRelationships.AnyAsync(r => r.FamilyTreeId == treeId && r.FromMemberId == existingId && r.ToMemberId == contextId && r.RelationshipType == RelationshipType.Parent, ct);
    }
}
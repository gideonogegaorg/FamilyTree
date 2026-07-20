using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Controllers;

[Authorize]
public sealed class FamilyTreeController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ICurrentFamilyTreeService _currentFamilyTree;
    private readonly IFamilyTreeDeletionService _familyTreeDeletion;
    private readonly IFamilyTreeAccessService _access;

    public FamilyTreeController(
        AppDbContext db,
        UserManager<IdentityUser> userManager,
        ICurrentFamilyTreeService currentFamilyTree,
        IFamilyTreeDeletionService familyTreeDeletion,
        IFamilyTreeAccessService access)
    {
        _db = db;
        _userManager = userManager;
        _currentFamilyTree = currentFamilyTree;
        _familyTreeDeletion = familyTreeDeletion;
        _access = access;
    }

    private string? OwnerId { get => field ??= _userManager.GetUserId(User); }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (OwnerId == null) return NotFound();
        var list = await _access.GetAccessibleTreesAsync(OwnerId, cancellationToken);
        return View(list);
    }

    public IActionResult Create()
    {
        return View(new Data.FamilyTree { Uid = Guid.NewGuid() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Data.FamilyTree model, CancellationToken cancellationToken)
    {
        if (OwnerId == null) return NotFound();
        ModelState.Remove(nameof(Data.FamilyTree.Id));
        ModelState.Remove(nameof(Data.FamilyTree.OwnerId));
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(Data.FamilyTree.Name), "Name is required.");
        if (ModelState.IsValid)
        {
            model.Uid = Guid.NewGuid();
            model.OwnerId = OwnerId;
            _db.FamilyTrees.Add(model);
            await _db.SaveChangesAsync(cancellationToken);
            await _currentFamilyTree.SetCurrentFamilyTreeIdAsync(model.Id, cancellationToken);
            return Redirect("/Home/Index");
        }
        return View(model);
    }

    public async Task<IActionResult> Edit(long? id, CancellationToken cancellationToken)
    {
        return !ModelState.IsValid
            ? BadRequest()
            : await ShowOwnedTreeFormAsync(id, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, Data.FamilyTree model, CancellationToken cancellationToken)
    {
        if (OwnerId == null) return NotFound();
        if (id != model.Id) return NotFound();
        ModelState.Remove(nameof(Data.FamilyTree.OwnerId));
        ModelState.Remove(nameof(Data.FamilyTree.Uid));
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(Data.FamilyTree.Name), "Name is required.");
        if (ModelState.IsValid)
        {
            var entity = await _db.FamilyTrees.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null || entity.OwnerId != OwnerId) return NotFound();
            entity.Name = model.Name;
            await _db.SaveChangesAsync(cancellationToken);
            return Redirect("/Home/Index");
        }
        return View(model);
    }

    public async Task<IActionResult> Delete(long? id, CancellationToken cancellationToken)
    {
        return !ModelState.IsValid
            ? BadRequest()
            : await ShowOwnedTreeFormAsync(id, cancellationToken, viewName: "Delete");
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        if (OwnerId == null) return NotFound();

        var result = await _familyTreeDeletion.DeleteAsync(OwnerId, id, cancellationToken);
        return result == FamilyTreeDeleteResult.NotFound
            ? NotFound()
            : Redirect("/Home/Index");
    }

    private async Task<IActionResult> ShowOwnedTreeFormAsync(long? id, CancellationToken cancellationToken, string? viewName = null)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        if (id == null || OwnerId == null) return NotFound();
        var entity = await _db.FamilyTrees.FindAsync(new object[] { id.Value }, cancellationToken);
        if (entity == null || entity.OwnerId != OwnerId)
            return NotFound();
        if (viewName == null)
            return View(entity);
        return View(viewName, entity);
    }
}
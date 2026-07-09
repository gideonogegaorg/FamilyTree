using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Controllers;

[Authorize]
public class FamilyTreeController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ICurrentFamilyTreeService _currentFamilyTree;
    private readonly IFamilyTreeDeletionService _familyTreeDeletion;

    public FamilyTreeController(AppDbContext db, UserManager<IdentityUser> userManager, ICurrentFamilyTreeService currentFamilyTree, IFamilyTreeDeletionService familyTreeDeletion)
    {
        _db = db;
        _userManager = userManager;
        _currentFamilyTree = currentFamilyTree;
        _familyTreeDeletion = familyTreeDeletion;
    }

    private string? _ownerId;
    private string? OwnerId => _ownerId ??= _userManager.GetUserId(User);

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (OwnerId == null) return NotFound();
        var list = await _db.FamilyTrees
            .Where(x => x.OwnerId == OwnerId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
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
        if (id == null || OwnerId == null) return NotFound();
        var entity = await _db.FamilyTrees.FindAsync(new object[] { id.Value }, cancellationToken);
        if (entity == null || entity.OwnerId != OwnerId) return NotFound();
        return View(entity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, Data.FamilyTree model, CancellationToken cancellationToken)
    {
        if (OwnerId == null) return NotFound();
        if (id != model.Id) return NotFound();
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
        if (id == null || OwnerId == null) return NotFound();
        var entity = await _db.FamilyTrees.FindAsync(new object[] { id.Value }, cancellationToken);
        if (entity == null || entity.OwnerId != OwnerId) return NotFound();
        return View(entity);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id, CancellationToken cancellationToken)
    {
        if (OwnerId == null) return NotFound();

        var result = await _familyTreeDeletion.DeleteAsync(OwnerId, id, cancellationToken);
        return result == FamilyTreeDeleteResult.NotFound
            ? NotFound()
            : Redirect("/Home/Index");
    }
}
using GMO.Family.Web.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.Controllers;

public class FamilyTreeController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public FamilyTreeController(AppDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
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

    public async Task<IActionResult> Details(long? id, CancellationToken cancellationToken)
    {
        if (id == null || OwnerId == null) return NotFound();
        var entity = await _db.FamilyTrees.FindAsync(new object[] { id.Value }, cancellationToken);
        if (entity == null || entity.OwnerId != OwnerId) return NotFound();
        return View(entity);
    }

    public IActionResult Create()
    {
        return View(new FamilyTree { Uid = Guid.NewGuid() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FamilyTree model, CancellationToken cancellationToken)
    {
        if (OwnerId == null) return NotFound();
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(FamilyTree.Name), "Name is required.");
        if (ModelState.IsValid)
        {
            model.Uid = Guid.NewGuid();
            model.OwnerId = OwnerId;
            _db.FamilyTrees.Add(model);
            await _db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> Edit(long id, FamilyTree model, CancellationToken cancellationToken)
    {
        if (OwnerId == null) return NotFound();
        if (id != model.Id) return NotFound();
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(FamilyTree.Name), "Name is required.");
        if (ModelState.IsValid)
        {
            var entity = await _db.FamilyTrees.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null || entity.OwnerId != OwnerId) return NotFound();
            entity.Name = model.Name;
            await _db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
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
        var entity = await _db.FamilyTrees.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null || entity.OwnerId != OwnerId) return NotFound();
        _db.FamilyTrees.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
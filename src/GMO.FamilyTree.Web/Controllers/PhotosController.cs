using System.Security.Claims;

using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Controllers;

[Authorize]
[Route("photos")]
public sealed class PhotosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPhotoStorageService _photos;
    private readonly IFamilyTreeAccessService _access;
    private readonly IWebHostEnvironment _env;
    private readonly PathsOptions _paths;

    public PhotosController(
        AppDbContext db,
        IPhotoStorageService photos,
        IFamilyTreeAccessService access,
        IWebHostEnvironment env,
        IOptions<PathsOptions> paths)
    {
        _db = db;
        _photos = photos;
        _access = access;
        _env = env;
        _paths = paths.Value;
    }

    [HttpGet("members/{memberId:long}")]
    public async Task<IActionResult> MemberPhoto(long memberId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var level = await _access.GetAccessLevelForMemberAsync(userId, memberId, cancellationToken);
        if (level < TreeAccessLevel.Readonly)
            return NotFound();

        var member = await _db.FamilyMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member == null || string.IsNullOrEmpty(member.PhotoKey))
            return NotFound();

        var result = await _photos.GetAsync(member.PhotoKey, cancellationToken);
        return result is null
            ? NotFound()
            : File(result.Stream, result.ContentType);
    }

    [HttpGet("profiles/me")]
    public Task<IActionResult> MyProfilePhoto(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null or ""
            ? Task.FromResult<IActionResult>(Unauthorized())
            : ProfilePhoto(userId, cancellationToken);
    }

    [HttpGet("profiles/{userId}")]
    public async Task<IActionResult> ProfilePhoto(string userId, CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
            return Unauthorized();

        if (!string.Equals(currentUserId, userId, StringComparison.Ordinal))
            return NotFound();

        var profile = await _db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null)
            return NotFound();

        if (!string.IsNullOrEmpty(profile.PhotoKey))
        {
            var result = await _photos.GetAsync(profile.PhotoKey, cancellationToken);
            if (result != null)
                return File(result.Stream, result.ContentType);
        }

        if (!string.IsNullOrEmpty(profile.PhotoUrl) && profile.PhotoUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var legacy = TryOpenLegacyUpload(profile.PhotoUrl);
            if (legacy != null)
                return legacy;
        }

        return NotFound();
    }

    private PhysicalFileResult? TryOpenLegacyUpload(string photoUrl)
    {
        var subPath = photoUrl["/uploads/".Length..];
        var uploadsBase = string.IsNullOrWhiteSpace(_paths.Uploads)
            ? Path.Combine(_env.WebRootPath, "uploads")
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, _paths.Uploads));
        var fullPath = Path.GetFullPath(Path.Combine(uploadsBase, subPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(uploadsBase, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            return null;

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        return PhysicalFile(fullPath, PhotoStorageKeys.ContentTypeForExtension(ext));
    }
}
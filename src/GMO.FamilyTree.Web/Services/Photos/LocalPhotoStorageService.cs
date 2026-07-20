using GMO.FamilyTree.Web.Options;

using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Services.Photos;

public sealed class LocalPhotoStorageService : IPhotoStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly PhotosOptions _options;

    public LocalPhotoStorageService(IWebHostEnvironment env, IOptions<PhotosOptions> options)
    {
        _env = env;
        _options = options.Value;
    }

    private string BaseDirectory =>
        Path.GetFullPath(Path.Combine(_env.ContentRootPath, _options.LocalBasePath));

    public Task<PhotoStreamResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetFullPath(PhotoStoragePaths.ToStorageKey(_options.StoragePrefix, key));
        if (!File.Exists(path))
            return Task.FromResult<PhotoStreamResult?>(null);

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var contentType = PhotoStorageKeys.ContentTypeForExtension(ext);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<PhotoStreamResult?>(new PhotoStreamResult(stream, contentType));
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = GetFullPath(PhotoStoragePaths.ToStorageKey(_options.StoragePrefix, key));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(stream, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetFullPath(PhotoStoragePaths.ToStorageKey(_options.StoragePrefix, key));
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetFullPath(string key)
    {
        var normalized = key.Replace('\\', '/').TrimStart('/');
        var combined = Path.GetFullPath(Path.Combine(BaseDirectory, normalized.Replace('/', Path.DirectorySeparatorChar)));
        return !combined.StartsWith(BaseDirectory, StringComparison.OrdinalIgnoreCase)
            ? throw new InvalidOperationException("Invalid photo key.")
            : combined;
    }
}
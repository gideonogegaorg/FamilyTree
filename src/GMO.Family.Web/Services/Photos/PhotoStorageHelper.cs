using Amazon.S3;

namespace GMO.Family.Web.Services.Photos;

public static class PhotoStorageHelper
{
    public const string StorageUnavailableMessage =
        "Photo storage is unavailable. If you use MinIO locally, run docker compose up -d.";

    public static bool IsStorageException(Exception ex) =>
        ex is HttpRequestException or AmazonS3Exception or System.Net.Sockets.SocketException;

    public static async Task SaveAsync(
        IPhotoStorageService storage,
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await storage.SaveAsync(key, content, contentType, cancellationToken);
    }

    /// <summary>Deletes a stored object after the database no longer references it. Failures are ignored.</summary>
    public static async Task TryDeleteAsync(
        IPhotoStorageService storage,
        string? key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            return;

        try
        {
            await storage.DeleteAsync(key, cancellationToken);
        }
        catch (Exception ex) when (IsStorageException(ex))
        {
            // Best effort: DB is already updated; orphaned blob is acceptable.
        }
    }

    public static async Task DeleteManyAsync(
        IPhotoStorageService storage,
        IEnumerable<string?> keys,
        CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            await TryDeleteAsync(storage, key, cancellationToken);
    }
}

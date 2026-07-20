namespace GMO.FamilyTree.Web.Services.Photos;

/// <summary>
/// Maps logical photo keys (stored in the database) to storage paths.
/// A shared S3 bucket can isolate apps and environments via <see cref="ToStorageKey"/>.
/// </summary>
public static class PhotoStoragePaths
{
    public static string NormalizePrefix(string? prefix)
    {
        return string.IsNullOrWhiteSpace(prefix)
            ? string.Empty
            : prefix.Trim().Trim('/').Replace('\\', '/') + "/";
    }

    public static string ToStorageKey(string? prefix, string logicalKey)
    {
        var normalizedKey = logicalKey.Trim().TrimStart('/').Replace('\\', '/');
        var normalizedPrefix = NormalizePrefix(prefix);
        return string.IsNullOrEmpty(normalizedPrefix) ? normalizedKey : normalizedPrefix + normalizedKey;
    }
}
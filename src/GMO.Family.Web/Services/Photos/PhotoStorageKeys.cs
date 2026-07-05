namespace GMO.Family.Web.Services.Photos;

public static class PhotoStorageKeys
{
    public static string Member(long treeId, long memberId, string extension) =>
        $"members/{treeId}/{memberId}{extension}";

    public static string Profile(string userId, string extension) =>
        $"profiles/{userId}{extension}";

    public static string? NormalizeExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => ext,
            _ => null
        };
    }

    public static string ContentTypeForExtension(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
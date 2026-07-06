namespace GMO.FamilyTree.Web.Options;

public sealed class PhotosOptions
{
    /// <summary>Storage backend: Local (dev/CI) or S3 (production EC2).</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>S3 bucket name when Provider is S3.</summary>
    public string? S3Bucket { get; set; }

    /// <summary>Optional custom S3 endpoint (e.g. MinIO at http://localhost:9000 for local docker compose).</summary>
    public string? S3ServiceUrl { get; set; }

    /// <summary>Access key when using a custom S3 endpoint. Ignored on EC2 (instance profile).</summary>
    public string? S3AccessKey { get; set; }

    /// <summary>Secret key when using a custom S3 endpoint. Ignored on EC2 (instance profile).</summary>
    public string? S3SecretKey { get; set; }

    /// <summary>AWS region for signing when using a custom endpoint (default us-east-1).</summary>
    public string S3Region { get; set; } = "us-east-1";

    /// <summary>
    /// Optional path prefix inside the bucket or local base directory.
    /// Use <c>{application}/{environment}/</c> (e.g. <c>familytree/dev/</c>) to share one bucket across apps and envs.
    /// </summary>
    public string? StoragePrefix { get; set; }

    /// <summary>Relative path under content root for local photo files (defaults to uploads/photos).</summary>
    public string LocalBasePath { get; set; } = "uploads/photos";
}
using Amazon.S3;
using Amazon.S3.Model;

using GMO.FamilyTree.Web.Options;

using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Services.Photos;

public sealed class S3PhotoStorageService : IPhotoStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly PhotosOptions _options;

    public S3PhotoStorageService(IAmazonS3 s3, IOptions<PhotosOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    private string Bucket => _options.S3Bucket
        ?? throw new InvalidOperationException("Photos:S3Bucket is required when Provider is S3.");

    private string StorageKey(string logicalKey) =>
        PhotoStoragePaths.ToStorageKey(_options.StoragePrefix, logicalKey);

    public async Task<PhotoStreamResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = Bucket,
                Key = StorageKey(key)
            }, cancellationToken);

            var contentType = response.Headers.ContentType ?? PhotoStorageKeys.ContentTypeForExtension(Path.GetExtension(key));
            return new PhotoStreamResult(response.ResponseStream, contentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = Bucket,
            Key = StorageKey(key),
            InputStream = content,
            ContentType = contentType
        };
        await _s3.PutObjectAsync(request, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        _s3.DeleteObjectAsync(Bucket, StorageKey(key), cancellationToken);
}
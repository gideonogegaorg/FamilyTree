namespace GMO.FamilyTree.Web.Services.Photos;

public interface IPhotoStorageService
{
    Task<PhotoStreamResult?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
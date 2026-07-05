using Microsoft.AspNetCore.Http;

namespace GMO.Family.Web.UnitTests.Helpers;

internal sealed class TestFormFile : IFormFile
{
    private readonly byte[] _bytes;

    public TestFormFile(string fileName, byte[] bytes)
    {
        FileName = fileName;
        _bytes = bytes;
    }

    public string ContentType => "image/png";
    public string ContentDisposition => string.Empty;
    public IHeaderDictionary Headers { get; } = new HeaderDictionary();
    public long Length => _bytes.Length;
    public string Name => "photo";
    public string FileName { get; }

    public Stream OpenReadStream() => new MemoryStream(_bytes);

    public void CopyTo(Stream target) => OpenReadStream().CopyTo(target);

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
        OpenReadStream().CopyToAsync(target, cancellationToken);
}
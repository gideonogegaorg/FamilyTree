namespace GMO.Family.Web.Services.Photos;

public sealed class PhotoStreamResult : IDisposable
{
    public Stream Stream { get; }
    public string ContentType { get; }

    public PhotoStreamResult(Stream stream, string contentType)
    {
        Stream = stream;
        ContentType = contentType;
    }

    public void Dispose() => Stream.Dispose();
}
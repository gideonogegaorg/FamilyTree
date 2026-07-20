using System.Security.Cryptography;
using System.Text;

namespace GMO.FamilyTree.Web.Services;

/// <summary>SHA-256 hex digests for log/OTel correlators (never reverse to plaintext).</summary>
public static class LogValueHasher
{
    public static string Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
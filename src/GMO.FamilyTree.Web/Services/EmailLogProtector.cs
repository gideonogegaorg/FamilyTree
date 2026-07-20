using Microsoft.AspNetCore.DataProtection;

namespace GMO.FamilyTree.Web.Services;

/// <summary>
/// Protects mailbox identifiers written to local email diagnostic logs.
/// Ciphertext is reversible with the same ASP.NET Data Protection keys (typically machine-local).
/// </summary>
public interface IEmailLogProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedPayload);
}

public sealed class EmailLogProtector : IEmailLogProtector
{
    public const string ProtectorPurpose = "GMO.FamilyTree.Web.EmailLog.v1";

    private readonly IDataProtector _protector;

    public EmailLogProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedPayload);
        return _protector.Unprotect(protectedPayload);
    }
}
using Microsoft.AspNetCore.DataProtection;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Security;

/// <summary>Encrypts secrets with ASP.NET Core Data Protection. Keys are stored in the database.</summary>
internal sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Orbs.Secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

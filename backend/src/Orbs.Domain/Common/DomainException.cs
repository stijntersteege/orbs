namespace Orbs.Domain.Common;

/// <summary>Thrown when an operation would break a business rule.</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException) : base(message, innerException) { }

    public DomainException() { }
}

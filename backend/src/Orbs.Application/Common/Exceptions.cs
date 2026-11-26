namespace Orbs.Application.Common;

/// <summary>The requested resource does not exist.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, Guid id) : base($"{resource} '{id}' was not found.") { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException() { }

    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>An orb cannot answer because no usable model is connected.</summary>
public sealed class ModelUnavailableException : Exception
{
    public ModelUnavailableException()
        : base("No model is available. Connect a provider and set a default model first.") { }

    public ModelUnavailableException(string message) : base(message) { }

    public ModelUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>A provider or connector failed while Orbs was calling it.</summary>
public sealed class ExternalServiceException : Exception
{
    public ExternalServiceException(string message) : base(message) { }

    public ExternalServiceException(string message, Exception innerException) : base(message, innerException) { }

    public ExternalServiceException() { }
}

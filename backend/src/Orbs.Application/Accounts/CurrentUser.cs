namespace Orbs.Application.Accounts;

/// <summary>
/// Who the current unit of work acts for. One per request or background job (scoped).
/// The database only returns data owned by <see cref="UserId"/>, unless the scope acts as the system.
/// </summary>
public sealed class CurrentUser
{
    /// <summary>The signed-in user, or <c>null</c> when nobody is signed in.</summary>
    public Guid? UserId { get; private set; }

    public bool IsAdmin { get; private set; }

    /// <summary>True for start-up work and maintenance that may see every user's data.</summary>
    public bool IsSystem { get; private set; }

    public bool IsSignedIn => UserId is not null;

    public void SignIn(Guid userId, bool isAdmin)
    {
        UserId = userId;
        IsAdmin = isAdmin;
        IsSystem = false;
    }

    public void ActAsSystem()
    {
        UserId = null;
        IsAdmin = false;
        IsSystem = true;
    }

    /// <summary>The signed-in user's id, or an <see cref="UnauthorizedException"/>.</summary>
    public Guid RequireUserId() => UserId ?? throw new UnauthorizedException();

    public void RequireAdmin()
    {
        if (!IsAdmin)
        {
            throw new ForbiddenException("Only admins can do this.");
        }
    }
}

/// <summary>Nobody is signed in, or the credentials were wrong.</summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("Sign in first.") { }

    public UnauthorizedException(string message) : base(message) { }

    public UnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>The signed-in user may not do this.</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException() : base("You may not do this.") { }

    public ForbiddenException(string message) : base(message) { }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException) { }
}

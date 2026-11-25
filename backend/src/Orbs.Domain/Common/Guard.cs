namespace Orbs.Domain.Common;

/// <summary>Small helpers that keep invariants readable at the top of a method.</summary>
public static class Guard
{
    public static void NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException($"{name} must not be empty.");
        }
    }

    public static string Text(string? value, string name, int maxLength, bool required = true)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (required && trimmed.Length == 0)
        {
            throw new DomainException($"{name} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static Uri? AbsoluteHttpUri(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException($"{name} must be an absolute http(s) URL.");
        }

        return uri;
    }
}

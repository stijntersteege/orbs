using System.Globalization;
using System.Text.Json;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Tools;

/// <summary>The date and time, optionally in a given time zone. Models do not know what time it is.</summary>
internal sealed class CurrentTimeTool(TimeProvider clock) : ITool
{
    public string Kind => "current_time";

    public string DisplayName => "Current time";

    public string Description => "Get the current date and time, optionally in an IANA time zone such as Europe/Amsterdam.";

    public string ParametersSchema => """
        {"type":"object","properties":{"timeZone":{"type":"string","description":"IANA time zone, for example Europe/Amsterdam. Defaults to UTC."}}}
        """;

    public string Summary => "Knows the current date and time, in any time zone.";

    public ToolRisk Risk => ToolRisk.Read;

    public Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var zoneId = ToolArguments.String(invocation.Arguments, "timeZone");
        var now = clock.GetUtcNow();
        if (string.IsNullOrWhiteSpace(zoneId))
        {
            return Task.FromResult(now.ToString("dddd yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone))
        {
            throw new ArgumentException($"Unknown time zone '{zoneId}'.");
        }

        var local = TimeZoneInfo.ConvertTime(now, zone);
        return Task.FromResult(local.ToString("dddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + $" ({zone.Id}, UTC{local:zzz})");
    }
}

/// <summary>Exact arithmetic, so orbs do not have to guess sums.</summary>
internal sealed class CalculatorTool : ITool
{
    public string Kind => "calculator";

    public string DisplayName => "Calculator";

    public string Description => "Evaluate an arithmetic expression with + - * / ^ % and parentheses, for example (12.5 * 4) / 3.";

    public string ParametersSchema => """
        {"type":"object","properties":{"expression":{"type":"string","description":"The expression to evaluate."}},"required":["expression"]}
        """;

    public string Summary => "Works out sums exactly instead of guessing.";

    public ToolRisk Risk => ToolRisk.Read;

    public Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var expression = ToolArguments.String(invocation.Arguments, "expression") ?? throw new ArgumentException("Give an expression.");
        var value = new Parser(expression).Parse();
        return Task.FromResult(value.ToString("G15", CultureInfo.InvariantCulture));
    }

    /// <summary>A small recursive-descent parser: no eval, no surprises.</summary>
    internal sealed class Parser(string text)
    {
        private const int MaxLength = 500;
        private int _pos;

        public double Parse()
        {
            if (text.Length > MaxLength)
            {
                throw new ArgumentException("The expression is too long.");
            }

            var value = Sum();
            Skip();
            if (_pos < text.Length)
            {
                throw new ArgumentException($"Unexpected '{text[_pos]}' at position {_pos + 1}.");
            }

            return double.IsFinite(value) ? value : throw new ArgumentException("The result is not a finite number.");
        }

        private double Sum()
        {
            var value = Product();
            while (true)
            {
                if (Accept('+'))
                {
                    value += Product();
                }
                else if (Accept('-'))
                {
                    value -= Product();
                }
                else
                {
                    return value;
                }
            }
        }

        private double Product()
        {
            var value = Power();
            while (true)
            {
                if (Accept('*'))
                {
                    value *= Power();
                }
                else if (Accept('/'))
                {
                    value /= Power();
                }
                else if (Accept('%'))
                {
                    value %= Power();
                }
                else
                {
                    return value;
                }
            }
        }

        private double Power()
        {
            var value = Unary();
            return Accept('^') ? Math.Pow(value, Power()) : value;
        }

        private double Unary()
        {
            if (Accept('-'))
            {
                return -Unary();
            }

            if (Accept('+'))
            {
                return Unary();
            }

            if (Accept('('))
            {
                var inner = Sum();
                return Accept(')') ? inner : throw new ArgumentException("A closing parenthesis is missing.");
            }

            Skip();
            var start = _pos;
            while (_pos < text.Length && (char.IsDigit(text[_pos]) || text[_pos] == '.'))
            {
                _pos++;
            }

            return start < _pos && double.TryParse(text.AsSpan(start, _pos - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new ArgumentException($"Expected a number at position {start + 1}.");
        }

        private bool Accept(char c)
        {
            Skip();
            if (_pos < text.Length && text[_pos] == c)
            {
                _pos++;
                return true;
            }

            return false;
        }

        private void Skip()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }
    }
}

/// <summary>Reads simple values from a tool's JSON arguments.</summary>
internal static class ToolArguments
{
    public static string? String(string json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty(name, out var value)
                   && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The arguments are not valid JSON: {ex.Message}", ex);
        }
    }
}

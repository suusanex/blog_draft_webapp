using System;

namespace BlogDraftWebApp.Core.Logging;

public static class SensitiveDataFilter
{
    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || key.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("prompt", StringComparison.OrdinalIgnoreCase)
            || key.Contains("stylecard", StringComparison.OrdinalIgnoreCase);
    }

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return "[REDACTED]";
    }
}

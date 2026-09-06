using System.Text.Json;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public sealed record OutlineParseResult(
    string Outline,
    EditorialMemo? EditorialMemo,
    bool FromJson,
    bool IsMalformedJson);

public sealed record DraftParseResult(
    string Draft,
    IReadOnlyList<string> OpenQuestions,
    bool FromJson,
    bool IsValid,
    string? ErrorMessage);

public static class GeneratedContentParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static OutlineParseResult ParseOutline(string raw)
    {
        if (TryDeserialize<OutlineLlmResponse>(raw, out var parsed) && parsed is not null
            && !string.IsNullOrWhiteSpace(parsed.Outline))
        {
            return new OutlineParseResult(parsed.Outline.Trim(), parsed.EditorialMemo, FromJson: true, IsMalformedJson: false);
        }

        return new OutlineParseResult(
            raw ?? string.Empty,
            EditorialMemo: null,
            FromJson: false,
            IsMalformedJson: LooksLikeJsonResponse(raw));
    }

    public static DraftParseResult ParseDraft(string raw)
    {
        if (TryReadDraftResponse(raw, out var draftContent, out var questions, out var failureMessage))
        {
            return new DraftParseResult(
                draftContent,
                questions,
                FromJson: true,
                IsValid: true,
                ErrorMessage: null);
        }

        return new DraftParseResult(
            Draft: string.Empty,
            OpenQuestions: Array.Empty<string>(),
            FromJson: false,
            IsValid: false,
            ErrorMessage: failureMessage ?? "LLM出力がJSON形式ではないか、draftが空です。");
    }

    public static string? SerializeEditorialMemo(EditorialMemo? memo)
    {
        if (memo is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(memo, JsonOptions);
    }

    private static bool TryDeserialize<T>(string raw, out T? value)
    {
        value = default;
        var json = ExtractJson(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadDraftResponse(
        string? raw,
        out string draft,
        out List<string> openQuestions,
        out string? failureMessage)
    {
        draft = string.Empty;
        openQuestions = new List<string>();
        failureMessage = null;
        var json = ExtractStrictJson(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            failureMessage = "JSON全体を入力してください。";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<DraftLlmResponse>(json, JsonOptions);
            if (parsed is null
                || string.IsNullOrWhiteSpace(parsed.Draft)
                || parsed.OpenQuestions is null
                || parsed.OpenQuestions.Any(question => question is null))
            {
                failureMessage = "JSONに空でないdraftとopenQuestions配列が必要です。";
                return false;
            }

            draft = parsed.Draft.Trim();
            openQuestions = parsed.OpenQuestions
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Select(question => question!.Trim())
                .ToList();

            return true;
        }
        catch (JsonException)
        {
            failureMessage = "JSON構文が不正です。";
            return false;
        }
    }

    private static string? ExtractJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        var fenceStart = trimmed.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var afterFence = trimmed[(fenceStart + 3)..];
            if (afterFence.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                afterFence = afterFence[4..];
            }

            var fenceEnd = afterFence.IndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0)
            {
                trimmed = afterFence[..fenceEnd].Trim();
            }
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace)
        {
            return null;
        }

        return trimmed[firstBrace..(lastBrace + 1)];
    }

    private static string? ExtractStrictJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal)
            && trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            if (firstLineEnd < 0)
            {
                return null;
            }

            trimmed = trimmed[(firstLineEnd + 1)..^3].Trim();
        }

        return trimmed.StartsWith("{", StringComparison.Ordinal)
            && trimmed.EndsWith("}", StringComparison.Ordinal)
            ? trimmed
            : null;
    }

    private static bool LooksLikeJsonResponse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OutlineLlmResponse
    {
        public string? Outline { get; set; }
        public EditorialMemo? EditorialMemo { get; set; }
    }

    private sealed class DraftLlmResponse
    {
        public string? Draft { get; set; }
        public List<string?>? OpenQuestions { get; set; }
    }

}

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
            for (var depth = 0; depth < 3; depth++)
            {
                if (!TryReadDraftResponse(draftContent, out var nestedDraft, out var nestedQuestions, out _))
                {
                    break;
                }

                draftContent = nestedDraft;
                if (nestedQuestions.Count > 0)
                {
                    questions = nestedQuestions;
                }
            }

            return new DraftParseResult(
                draftContent,
                questions,
                FromJson: true,
                IsValid: true,
                ErrorMessage: null);
        }

        if (TryRecoverEmbeddedDraft(raw, out var embeddedDraft, out var embeddedQuestions))
        {
            return new DraftParseResult(
                embeddedDraft,
                embeddedQuestions,
                FromJson: false,
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

    public static bool TryRecoverMarkdownDraft(string raw, out DraftParseResult recovered)
    {
        recovered = new DraftParseResult(
            Draft: string.Empty,
            OpenQuestions: Array.Empty<string>(),
            FromJson: false,
            IsValid: false,
            ErrorMessage: null);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var content = raw.Trim();
        if (!LooksLikeMarkdownDraft(content))
        {
            return false;
        }

        if (content.StartsWith("```", StringComparison.Ordinal)
            && content.EndsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = content.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                content = content[(firstLineEnd + 1)..].TrimEnd('`', '\r', '\n', ' ', '\t').Trim();
            }
        }

        recovered = new DraftParseResult(
            Draft: content,
            OpenQuestions: Array.Empty<string>(),
            FromJson: false,
            IsValid: true,
            ErrorMessage: null);
        return true;
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
        var json = ExtractJson(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            failureMessage = "JSON候補がありません。";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryGetProperty(document.RootElement, "draft", out var draftProperty)
                || draftProperty.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(draftProperty.GetString()))
            {
                failureMessage = "JSONのdraftフィールドが文字列ではないか、空です。";
                return false;
            }

            draft = draftProperty.GetString()!.Trim();
            if (TryGetProperty(document.RootElement, "openQuestions", out var questionsProperty)
                && questionsProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in questionsProperty.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        openQuestions.Add(item.GetString()!.Trim());
                    }
                }
            }

            return true;
        }
        catch (JsonException)
        {
            failureMessage = "JSON構文が不正です。";
            return false;
        }
    }

    private static bool TryRecoverEmbeddedDraft(
        string? raw,
        out string draft,
        out List<string> openQuestions)
    {
        draft = string.Empty;
        openQuestions = new List<string>();
        var candidate = ExtractDraftString(raw);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        for (var depth = 0; depth < 3; depth++)
        {
            if (!TryReadDraftResponse(candidate, out var nestedDraft, out var nestedQuestions, out _))
            {
                break;
            }

            candidate = nestedDraft;
            if (nestedQuestions.Count > 0)
            {
                openQuestions = nestedQuestions;
            }
        }

        if (!LooksLikeMarkdownDraft(candidate))
        {
            return false;
        }

        draft = candidate;
        return true;
    }

    private static string? ExtractDraftString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var searchStart = 0;
        while (searchStart < raw.Length)
        {
            var propertyIndex = raw.IndexOf("\"draft\"", searchStart, StringComparison.OrdinalIgnoreCase);
            if (propertyIndex < 0)
            {
                return null;
            }

            var colonIndex = raw.IndexOf(':', propertyIndex + 7);
            if (colonIndex < 0)
            {
                return null;
            }

            var valueStart = colonIndex + 1;
            while (valueStart < raw.Length && char.IsWhiteSpace(raw[valueStart]))
            {
                valueStart++;
            }

            if (valueStart < raw.Length && raw[valueStart] == '\"')
            {
                var valueEnd = valueStart + 1;
                while (valueEnd < raw.Length)
                {
                    if (raw[valueEnd] == '\\')
                    {
                        valueEnd += 2;
                        continue;
                    }

                    if (raw[valueEnd] == '\"')
                    {
                        var jsonString = raw[valueStart..(valueEnd + 1)];
                        try
                        {
                            return JsonSerializer.Deserialize<string>(jsonString, JsonOptions);
                        }
                        catch (JsonException)
                        {
                            searchStart = valueEnd + 1;
                            break;
                        }
                    }

                    valueEnd++;
                }
            }

            searchStart = propertyIndex + 7;
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
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

    private static bool LooksLikeMarkdownDraft(string content)
    {
        if (LooksLikeJsonResponse(content))
        {
            return false;
        }

        return content.Length >= 40
            || content.StartsWith("#", StringComparison.Ordinal)
            || content.Contains("\n#", StringComparison.Ordinal)
            || content.Contains("\n- ", StringComparison.Ordinal)
            || content.Contains("https://", StringComparison.Ordinal)
            || content.Contains("```", StringComparison.Ordinal);
    }

    private sealed class OutlineLlmResponse
    {
        public string? Outline { get; set; }
        public EditorialMemo? EditorialMemo { get; set; }
    }

}

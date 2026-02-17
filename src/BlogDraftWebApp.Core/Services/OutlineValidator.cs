using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;

namespace BlogDraftWebApp.Core.Services;

public sealed class OutlineValidator
{
    public void ValidateOrThrow(string content, WorkflowOptions options)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new OutlineConstraintViolationException("アウトラインを入力してください");
        }

        if (content.Length > options.OutlineMaxTotalChars)
        {
            throw new OutlineConstraintViolationException($"アウトラインの総文字数は {options.OutlineMaxTotalChars} 文字以内で入力してください");
        }

        var normalizedLines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .ToList();

        if (normalizedLines.Count < options.OutlineMinLines || normalizedLines.Count > options.OutlineMaxLines)
        {
            throw new OutlineConstraintViolationException($"アウトラインの行数は {options.OutlineMinLines}〜{options.OutlineMaxLines} 行で入力してください");
        }

        foreach (var line in normalizedLines)
        {
            ValidateLine(line, options);
        }
    }

    private static void ValidateLine(string line, WorkflowOptions options)
    {
        if (line.Length > options.OutlineMaxLineLength)
        {
            throw new OutlineConstraintViolationException($"アウトラインの1行は {options.OutlineMaxLineLength} 文字以内で入力してください");
        }

        var leadingSpaces = CountLeadingSpaces(line);
        var depth = leadingSpaces / 2;
        if (depth > options.OutlineMaxDepth)
        {
            throw new OutlineConstraintViolationException($"アウトラインの階層は最大 {options.OutlineMaxDepth} までです");
        }

        var trimmedStart = line.TrimStart();
        if (!trimmedStart.StartsWith("- ", StringComparison.Ordinal))
        {
            throw new OutlineConstraintViolationException("アウトラインは `-` で始まる箇条書きのみ使用できます");
        }

        if (trimmedStart.StartsWith("#", StringComparison.Ordinal)
            || IsNumberedList(trimmedStart)
            || trimmedStart.StartsWith(">", StringComparison.Ordinal)
            || trimmedStart.StartsWith("```", StringComparison.Ordinal)
            || trimmedStart.StartsWith("* ", StringComparison.Ordinal)
            || trimmedStart.StartsWith("+ ", StringComparison.Ordinal))
        {
            throw new OutlineConstraintViolationException("見出し・番号付きリスト・引用・コードブロック等は使用できません");
        }

        var bulletText = trimmedStart[2..].Trim();
        if (string.IsNullOrWhiteSpace(bulletText))
        {
            throw new OutlineConstraintViolationException("箇条書きの本文を入力してください");
        }
    }

    private static int CountLeadingSpaces(string line)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ')
            {
                count++;
                continue;
            }

            break;
        }

        return count;
    }

    private static bool IsNumberedList(string text)
    {
        if (text.Length < 3)
        {
            return false;
        }

        var index = 0;
        while (index < text.Length && char.IsDigit(text[index]))
        {
            index++;
        }

        return index > 0
            && index + 1 < text.Length
            && text[index] == '.'
            && text[index + 1] == ' ';
    }
}
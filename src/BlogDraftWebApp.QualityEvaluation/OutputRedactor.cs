using BlogDraftWebApp.Core.Models;
using System.Text;

namespace BlogDraftWebApp.QualityEvaluation;

public static class OutputRedactor
{
    private const int MinimumPartialMatchLength = 16;
    private const string Replacement = "[REDACTED_SENSITIVE_CONTENT]";

    public static string Redact(string output, IReadOnlyList<RAGChunk> chunks, StyleCard styleCard)
    {
        var sensitiveValues = chunks
            .SelectMany(x => new[] { x.Text, x.SourceTitle, x.SourceUrl })
            .Append(styleCard.SystemPrompt)
            .Append(styleCard.Content)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal);

        var outputRunes = GetIndexedRunes(output);
        var ranges = new List<TextRange>();
        foreach (var sensitiveValue in sensitiveValues)
        {
            AddExactMatchRanges(output, sensitiveValue, ranges);
            AddPartialMatchRanges(outputRunes, sensitiveValue, ranges);
        }

        return ReplaceRanges(output, ranges);
    }

    private static void AddExactMatchRanges(string output, string sensitiveValue, List<TextRange> ranges)
    {
        var searchStart = 0;
        while (searchStart < output.Length)
        {
            var matchStart = output.IndexOf(sensitiveValue, searchStart, StringComparison.Ordinal);
            if (matchStart < 0)
            {
                return;
            }

            ranges.Add(new TextRange(matchStart, matchStart + sensitiveValue.Length));
            searchStart = matchStart + sensitiveValue.Length;
        }
    }

    private static void AddPartialMatchRanges(
        IndexedRune[] outputRunes,
        string sensitiveValue,
        List<TextRange> ranges)
    {
        var sensitiveRunes = sensitiveValue.EnumerateRunes().ToArray();
        if (outputRunes.Length < MinimumPartialMatchLength || sensitiveRunes.Length < MinimumPartialMatchLength)
        {
            return;
        }

        var previous = new int[sensitiveRunes.Length + 1];
        var current = new int[sensitiveRunes.Length + 1];
        for (var outputIndex = 0; outputIndex < outputRunes.Length; outputIndex++)
        {
            for (var sensitiveIndex = 0; sensitiveIndex < sensitiveRunes.Length; sensitiveIndex++)
            {
                if (outputRunes[outputIndex].Value != sensitiveRunes[sensitiveIndex])
                {
                    current[sensitiveIndex + 1] = 0;
                    continue;
                }

                var matchLength = previous[sensitiveIndex] + 1;
                current[sensitiveIndex + 1] = matchLength;
                var matchEndsHere = outputIndex == outputRunes.Length - 1
                    || sensitiveIndex == sensitiveRunes.Length - 1
                    || outputRunes[outputIndex + 1].Value != sensitiveRunes[sensitiveIndex + 1];
                if (matchLength >= MinimumPartialMatchLength && matchEndsHere)
                {
                    var startRune = outputRunes[outputIndex - matchLength + 1];
                    var endRune = outputRunes[outputIndex];
                    ranges.Add(new TextRange(startRune.Utf16Start, endRune.Utf16Start + endRune.Utf16Length));
                }
            }

            (previous, current) = (current, previous);
            Array.Clear(current);
        }
    }

    private static IndexedRune[] GetIndexedRunes(string value)
    {
        var runes = new List<IndexedRune>();
        var utf16Start = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            runes.Add(new IndexedRune(rune, utf16Start, rune.Utf16SequenceLength));
            utf16Start += rune.Utf16SequenceLength;
        }

        return runes.ToArray();
    }

    private static string ReplaceRanges(string output, List<TextRange> ranges)
    {
        if (ranges.Count == 0)
        {
            return output;
        }

        var merged = new List<TextRange>();
        foreach (var range in ranges.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            if (merged.Count == 0 || range.Start > merged[^1].End)
            {
                merged.Add(range);
                continue;
            }

            merged[^1] = new TextRange(merged[^1].Start, Math.Max(merged[^1].End, range.End));
        }

        var builder = new StringBuilder(output.Length);
        var position = 0;
        foreach (var range in merged)
        {
            builder.Append(output, position, range.Start - position);
            builder.Append(Replacement);
            position = range.End;
        }

        builder.Append(output, position, output.Length - position);
        return builder.ToString();
    }

    private readonly record struct IndexedRune(Rune Value, int Utf16Start, int Utf16Length);

    private readonly record struct TextRange(int Start, int End);
}

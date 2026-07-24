using System.Security.Cryptography;
using System.Text;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.QualityEvaluation;

public static class SourceIdentity
{
    public static string Create(RAGChunk chunk)
    {
        var source = !string.IsNullOrWhiteSpace(chunk.SourceUrl)
            ? $"url:{chunk.SourceUrl}"
            : !string.IsNullOrWhiteSpace(chunk.SourceTitle)
                ? $"title:{chunk.SourceTitle}"
                : $"text:{chunk.Text}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

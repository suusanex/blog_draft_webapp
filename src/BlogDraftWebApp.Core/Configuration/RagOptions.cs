namespace BlogDraftWebApp.Core.Configuration;

public sealed class RagOptions
{
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string TextFieldName { get; set; } = string.Empty;
    public string VectorFieldName { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public double MinimumScore { get; set; } = 0.7;
}

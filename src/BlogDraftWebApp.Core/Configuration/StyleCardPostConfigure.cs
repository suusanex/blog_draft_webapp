using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Core.Configuration;

/// <summary>
/// StyleCardOptions のファイルパス処理を行う PostConfigure。
/// FilePath が指定されている場合、ファイルから Content を読み込む。
/// </summary>
public sealed class StyleCardPostConfigure : IPostConfigureOptions<StyleCardOptions>
{
    private readonly ILogger<StyleCardPostConfigure> logger;

    public StyleCardPostConfigure(ILogger<StyleCardPostConfigure> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void PostConfigure(string? name, StyleCardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // FilePath が指定されている場合、ファイルから Content を読み込む
        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            try
            {
                logger.LogInformation("Loading StyleCard from file: {FilePath}", options.FilePath);

                if (!File.Exists(options.FilePath))
                {
                    throw new FileNotFoundException($"StyleCard file not found: {options.FilePath}");
                }

                var fileContent = File.ReadAllText(options.FilePath);
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    throw new InvalidOperationException($"StyleCard file is empty: {options.FilePath}");
                }

                // ファイル内容を Content に設定（既存の Content を上書き）
                options.Content = fileContent;

                logger.LogDebug("StyleCard loaded from file successfully. Content length: {Length}", fileContent.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load StyleCard from file: {FilePath}", options.FilePath);
                throw;
            }
        }
    }
}

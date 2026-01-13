using BlogDraftWebApp.Core.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace BlogDraftWebApp.Core.UnitTests.Configuration;

[TestFixture]
public sealed class StyleCardPostConfigureTests
{
    private ILogger<StyleCardPostConfigure> logger = null!;
    private StyleCardPostConfigure postConfigure = null!;

    [SetUp]
    public void SetUp()
    {
        // Moq またはシンプルな実装を使用
        logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<StyleCardPostConfigure>.Instance;
        postConfigure = new StyleCardPostConfigure(logger);
    }

    [Test]
    public void PostConfigure_WithFilePath_LoadsContentFromFile()
    {
        // Arrange: テンポラリファイルを作成
        var tempFilePath = Path.GetTempFileName();
        var expectedContent = "## 執筆方針\n- テックブログです\n- わかりやすく";
        File.WriteAllText(tempFilePath, expectedContent);

        try
        {
            var options = new StyleCardOptions
            {
                FilePath = tempFilePath,
                Content = "old content",
                SystemPrompt = "system"
            };

            // Act
            postConfigure.PostConfigure(null, options);

            // Assert: ファイルから読み込まれた内容で Content が上書きされたこと
            Assert.That(options.Content, Is.EqualTo(expectedContent));
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Test]
    public void PostConfigure_WithoutFilePath_DoesNotModifyContent()
    {
        // Arrange
        var originalContent = "direct content";
        var options = new StyleCardOptions
        {
            FilePath = null,
            Content = originalContent,
            SystemPrompt = "system"
        };

        // Act
        postConfigure.PostConfigure(null, options);

        // Assert: Content が変更されていないこと
        Assert.That(options.Content, Is.EqualTo(originalContent));
    }

    [Test]
    public void PostConfigure_WithFilePathNonExistent_ThrowsFileNotFoundException()
    {
        // Arrange
        var options = new StyleCardOptions
        {
            FilePath = "C:\\nonexistent\\path\\stylecard.md",
            Content = string.Empty,
            SystemPrompt = "system"
        };

        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(() => postConfigure.PostConfigure(null, options));
        Assert.That(ex?.Message, Does.Contain("StyleCard file not found"));
    }

    [Test]
    public void PostConfigure_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange: 空のファイルを作成
        var tempFilePath = Path.GetTempFileName();
        File.WriteAllText(tempFilePath, string.Empty);

        try
        {
            var options = new StyleCardOptions
            {
                FilePath = tempFilePath,
                Content = string.Empty,
                SystemPrompt = "system"
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => postConfigure.PostConfigure(null, options));
            Assert.That(ex?.Message, Does.Contain("StyleCard file is empty"));
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Test]
    public void PostConfigure_WithFilePathAndContent_FilePathTakesPriority()
    {
        // Arrange: ファイルを作成
        var tempFilePath = Path.GetTempFileName();
        var fileContent = "from file";
        File.WriteAllText(tempFilePath, fileContent);

        try
        {
            var options = new StyleCardOptions
            {
                FilePath = tempFilePath,
                Content = "from content field",
                SystemPrompt = "system"
            };

            // Act
            postConfigure.PostConfigure(null, options);

            // Assert: ファイルの内容が優先されること
            Assert.That(options.Content, Is.EqualTo(fileContent));
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }
}

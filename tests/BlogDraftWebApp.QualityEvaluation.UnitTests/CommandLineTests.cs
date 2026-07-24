namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class CommandLineTests
{
    [Test]
    public void ParseRun_ConsumesVerboseWithoutRemovingConfigurationArguments()
    {
        var options = CommandLine.ParseRun(
            ["--mode", "live", "--prompt-version", "test", "--verbose", "--OpenAI:Model", "gpt-test"]);

        Assert.Multiple(() =>
        {
            Assert.That(options.Verbose, Is.True);
            Assert.That(options.ConfigurationArguments, Is.EqualTo(new[] { "--OpenAI:Model", "gpt-test" }));
        });
    }

    [Test]
    public void ParseCompare_ConsumesVerbose()
    {
        var options = CommandLine.ParseCompare(
            ["--baseline", "before.json", "--verbose", "--candidate", "after.json"]);

        Assert.That(options.Verbose, Is.True);
    }

    [Test]
    [NonParallelizable]
    public async Task Main_UsesConciseErrorByDefaultAndDetailedErrorWhenVerbose()
    {
        var originalError = Console.Error;
        try
        {
            using var conciseWriter = new StringWriter();
            Console.SetError(conciseWriter);
            var conciseExitCode = await Program.Main(["unknown"]);

            using var verboseWriter = new StringWriter();
            Console.SetError(verboseWriter);
            var verboseExitCode = await Program.Main(["unknown", "--verbose"]);

            Assert.Multiple(() =>
            {
                Assert.That(conciseExitCode, Is.EqualTo(2));
                Assert.That(conciseWriter.ToString(), Does.Contain("Unknown command: unknown"));
                Assert.That(conciseWriter.ToString(), Does.Not.Contain("System.ArgumentException"));
                Assert.That(verboseExitCode, Is.EqualTo(2));
                Assert.That(verboseWriter.ToString(), Does.Contain("System.ArgumentException"));
                Assert.That(verboseWriter.ToString(), Does.Contain("Unknown command: unknown"));
            });
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}

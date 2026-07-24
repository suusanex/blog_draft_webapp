namespace BlogDraftWebApp.QualityEvaluation.UnitTests;

public sealed class CaseSetLoaderTests
{
    [Test]
    public async Task CommittedCaseSet_IsValidAndCoversRequiredCategories()
    {
        var root = RepositoryPaths.FindRoot();
        var caseSet = await CaseSetLoader.LoadAsync(Path.Combine(root, "evaluation", "cases.json"), CancellationToken.None);

        Assert.That(caseSet.Cases, Has.Count.EqualTo(12));
        Assert.That(caseSet.Cases.Select(x => x.Category).Distinct().ToArray(), Has.Length.EqualTo(6));
        Assert.That(caseSet.Cases.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), Has.Length.EqualTo(12));
    }

    [Test]
    public void Validate_RejectsDuplicateIds()
    {
        var caseSet = ValidCaseSet();
        caseSet.Cases.Add(ValidCase("same", "troubleshooting"));

        Assert.That(
            () => CaseSetLoader.Validate(caseSet),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("unique"));
    }

    private static EvaluationCaseSet ValidCaseSet()
    {
        var categories = new[]
        {
            "experiment-results", "troubleshooting", "comparison-decision",
            "very-short-input", "supporting-context-needed", "rag-broad-match-risk",
        };
        var cases = Enumerable.Range(0, 10)
            .Select(index => ValidCase(index == 0 ? "same" : $"case-{index}", categories[index % categories.Length]))
            .ToList();
        return new EvaluationCaseSet { SchemaVersion = 1, Version = "test", Cases = cases };
    }

    private static EvaluationCase ValidCase(string id, string category) => new()
    {
        Id = id,
        Category = category,
        Input = "十分な長さを持つ架空の入力です。",
        ExpectedFocalPoints = [new ExpectedTopic { Name = "焦点", AnyOf = ["架空"] }],
        RequiredSupportingTopics = [new ExpectedTopic { Name = "補足", AnyOf = ["入力"] }],
        ForbiddenScopes = [new ForbiddenScope { Name = "禁止", Patterns = ["禁止語"] }],
        MaxHeadingCount = 3,
        TargetLengthRange = new LengthRange { Min = 1, Max = 1000 },
    };
}

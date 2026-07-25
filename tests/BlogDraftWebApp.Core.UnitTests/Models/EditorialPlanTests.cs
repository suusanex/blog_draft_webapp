using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;

namespace BlogDraftWebApp.Core.UnitTests.Models;

public sealed class EditorialPlanTests
{
    [Test]
    public void Validate_AcceptsInputBackedPlan()
    {
        var plan = ValidPlan();

        Assert.DoesNotThrow(() => EditorialPlanValidator.Validate(plan, "0123456789"));
    }

    [Test]
    public void Validate_RejectsMissingSourceExcerpt()
    {
        var plan = ValidPlan();
        plan.FocalPoints[0].SourceExcerpt = "not in input";

        var ex = Assert.Throws<EditorialPlanValidationException>(() => EditorialPlanValidator.Validate(plan, "0123456789"));

        Assert.That(ex!.Message, Does.Contain("根拠断片"));
    }

    [Test]
    public void Validate_RejectsInferredFactualItem()
    {
        var plan = ValidPlan();
        plan.TriedOrObserved.Add(new BriefItem
        {
            Id = "observed-1",
            Text = "推測した観測",
            Origin = BriefItemOrigin.InferredEditorialConstraint,
        });

        var ex = Assert.Throws<EditorialPlanValidationException>(() => EditorialPlanValidator.Validate(plan, "0123456789"));

        Assert.That(ex!.Message, Does.Contain("事実項目"));
    }

    [Test]
    public void Validate_RejectsSectionWithoutBodyMaterial()
    {
        var plan = ValidPlan();
        plan.Sections[0].SourceItemIds.Clear();

        var ex = Assert.Throws<EditorialPlanValidationException>(() => EditorialPlanValidator.Validate(plan, "0123456789"));

        Assert.That(ex!.Message, Does.Contain("本文材料"));
    }

    [Test]
    public void Validate_RejectsReaderAssumptionAsBodyMaterial()
    {
        var plan = ValidPlan();
        plan.Sections[0].SourceItemIds = ["assumption-1"];

        var ex = Assert.Throws<EditorialPlanValidationException>(() => EditorialPlanValidator.Validate(plan, "0123456789"));

        Assert.That(ex!.Message, Does.Contain("本文材料"));
    }

    [Test]
    public void Validate_RejectsNullItemEntry()
    {
        var plan = ValidPlan();
        plan.FocalPoints = [null!];

        var ex = Assert.Throws<EditorialPlanValidationException>(() => EditorialPlanValidator.Validate(plan, "0123456789"));

        Assert.That(ex!.Message, Does.Contain("null要素"));
    }

    [Test]
    public void NormalizeAndValidate_TrimsReferenceIdsForWriterUse()
    {
        var plan = ValidPlan();
        plan.FocalPoints[0].Id = " focus-1 ";
        plan.Sections[0].SourceItemIds = [" focus-1 "];

        var normalized = EditorialPlanValidator.NormalizeAndValidate(plan, "0123456789");

        Assert.That(normalized.FocalPoints[0].Id, Is.EqualTo("focus-1"));
        Assert.That(normalized.Sections[0].SourceItemIds, Is.EqualTo(new[] { "focus-1" }));
    }

    [Test]
    public async Task ComposeApprovedAsync_DoesNotIncludeOriginalOverview()
    {
        var plan = ValidPlan();
        var prompt = await new PromptComposer().ComposeApprovedAsync(
            new BlogOverview("0123456789 with deleted material"),
            plan,
            [],
            new StyleCard { SystemPrompt = "system", Content = "style" },
            CancellationToken.None);

        Assert.That(prompt.UserOverview, Does.Contain("承認済み編集計画"));
        Assert.That(prompt.UserOverview, Does.Contain("0123456789"));
        Assert.That(prompt.UserOverview, Does.Not.Contain("deleted material"));
        Assert.That(prompt.StructuredOutput, Is.Null);
    }

    private static EditorialPlan ValidPlan() => new()
    {
        Thesis = new BriefItem
        {
            Id = "thesis-1",
            Text = "中心",
            Origin = BriefItemOrigin.Input,
            SourceExcerpt = "0123456789",
        },
        FocalPoints =
        [
            new BriefItem
            {
                Id = "focus-1",
                Text = "再整理した中心",
                Origin = BriefItemOrigin.Reorganized,
                SourceExcerpt = "0123456789",
            },
        ],
        ReaderAssumptions =
        [
            new BriefItem
            {
                Id = "assumption-1",
                Text = "読者は基本用語を知っている",
                Origin = BriefItemOrigin.InferredEditorialConstraint,
            },
        ],
        Sections =
        [
            new PlannedSection
            {
                Id = "section-1",
                Heading = "中心",
                Purpose = "中心を伝える",
                SourceItemIds = ["focus-1"],
            },
        ],
    };
}

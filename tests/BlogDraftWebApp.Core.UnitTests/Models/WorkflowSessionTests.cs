using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.UnitTests.Models;

public sealed class WorkflowSessionTests
{
    [Test]
    public void CanGenerateStep_RespectsOutlineAndDraftConfirmations()
    {
        var session = new WorkflowSession
        {
            SessionId = "s1",
            InitialInput = new BlogOverview("0123456789"),
        };

        Assert.That(session.CanGenerateStep(WorkflowStep.Step1_Outline), Is.True);
        Assert.That(session.CanGenerateStep(WorkflowStep.Step2_Draft), Is.False);
        Assert.That(session.CanGenerateStep(WorkflowStep.Step3_TitleHook), Is.False);

        session.OutlineConfirmed = "outline";
        Assert.That(session.CanGenerateStep(WorkflowStep.Step2_Draft), Is.True);

        session.DraftConfirmed = "draft";
        Assert.That(session.CanGenerateStep(WorkflowStep.Step3_TitleHook), Is.False);
    }

    [Test]
    public void TransitionToStep_Throws_WhenInvalid()
    {
        var session = new WorkflowSession
        {
            SessionId = "s1",
            InitialInput = new BlogOverview("0123456789"),
            CurrentStep = WorkflowStep.Step1_Outline,
        };

        Assert.That(() => session.TransitionToStep(WorkflowStep.Step2_Draft, 30), Throws.TypeOf<InvalidStateTransitionException>());
    }
}



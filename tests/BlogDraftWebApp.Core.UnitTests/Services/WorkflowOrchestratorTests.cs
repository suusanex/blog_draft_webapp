using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BlogDraftWebApp.Core.UnitTests.Services;

public sealed class WorkflowOrchestratorTests
{
    [Test]
    public async Task GenerateStepAsync_ThrowsSessionBusy_WhenLocked()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var orchestrator = CreateOrchestrator(repository, lockService);

        var session = new WorkflowSession
        {
            SessionId = "s1",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };

        await repository.CreateSessionAsync(session, CancellationToken.None);
        using var handle = await lockService.TryAcquireAsync(session.SessionId, CancellationToken.None);

        var ex = Assert.ThrowsAsync<SessionBusyException>(() =>
            orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public void GetSessionAsync_ThrowsExpired_WhenDeleteAtPassed()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var orchestrator = CreateOrchestrator(repository, lockService);

        var session = new WorkflowSession
        {
            SessionId = "expired",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-31),
            LastAccessedAt = DateTimeOffset.UtcNow.AddDays(-31),
            DeleteAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        repository.CreateSessionAsync(session, CancellationToken.None).GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<SessionNotFoundException>(() =>
            orchestrator.GetSessionAsync(session.SessionId, CancellationToken.None));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.IsExpired, Is.True);
    }

    [Test]
    public async Task GenerateStepAsync_ReturnsGeneratedOutline()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var retrievalMock = new Mock<IRetrievalService>(MockBehavior.Strict);
        var composerMock = new Mock<IPromptComposer>();

        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, retrievalMock, composerMock);

        var session = new WorkflowSession
        {
            SessionId = "s2",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };

        await repository.CreateSessionAsync(session, CancellationToken.None);

        composerMock
            .Setup(x => x.ComposeAsync(
                WorkflowStep.Step1_Outline,
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { UserOverview = "prompt" });

        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論",
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None);

        Assert.That(result.Content, Is.EqualTo("- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論"));
        Assert.That(result.Model, Is.EqualTo("m1"));
        Assert.That(result.RagHitCount, Is.EqualTo(0));
        Assert.That(repository.Sessions[session.SessionId].OutlineGenerated, Is.EqualTo("- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論"));
        retrievalMock.Verify(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GenerateStepAsync_前置きとコードフェンス混入時_正規化して保存する()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var retrievalMock = new Mock<IRetrievalService>(MockBehavior.Strict);
        var composerMock = new Mock<IPromptComposer>();

        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, retrievalMock, composerMock);

        var session = new WorkflowSession
        {
            SessionId = "outline-normalize",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };

        await repository.CreateSessionAsync(session, CancellationToken.None);

        composerMock
            .Setup(x => x.ComposeAsync(
                WorkflowStep.Step1_Outline,
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { UserOverview = "prompt" });

        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "以下のアウトラインです\n```markdown\n- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論\n```",
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None);

        Assert.That(result.Content, Is.EqualTo("- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論"));
        llmMock.Verify(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Once);
    }

    [Test]
    public async Task GenerateStepAsync_散文のみの初回出力は_再整形で救済する()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var retrievalMock = new Mock<IRetrievalService>(MockBehavior.Strict);
        var composerMock = new Mock<IPromptComposer>();

        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, retrievalMock, composerMock);

        var session = new WorkflowSession
        {
            SessionId = "outline-repair",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };

        await repository.CreateSessionAsync(session, CancellationToken.None);

        composerMock
            .Setup(x => x.ComposeAsync(
                WorkflowStep.Step1_Outline,
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { UserOverview = "prompt" });

        composerMock
            .Setup(x => x.ComposeOutlineRepair(
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<WorkflowOptions>()))
            .Returns(new Prompt { UserOverview = PromptComposer.OutlineRepairHeading });

        llmMock
            .SetupSequence(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "これはアウトラインではありません。説明文だけです。",
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            })
            .ReturnsAsync(new Draft
            {
                Content = "- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論",
                Model = "m1-repair",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None);

        Assert.That(result.Content, Is.EqualTo("- 背景\n  - 課題\n- 目的\n  - 対象読者\n- 結論"));
        Assert.That(result.Model, Is.EqualTo("m1-repair"));
        llmMock.Verify(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Exactly(2));
        composerMock.Verify(
            x => x.ComposeOutlineRepair(
                It.Is<BlogOverview>(o => o.Content == "0123456789"),
                It.IsAny<StyleCard>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<WorkflowOptions>()),
            Times.Once);
    }

    [Test]
    public async Task GenerateStepAsync_未知の行が混在するアウトラインは原文を保持して再整形に渡す()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var retrievalMock = new Mock<IRetrievalService>(MockBehavior.Strict);
        var composerMock = new Mock<IPromptComposer>();
        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, retrievalMock, composerMock);

        var session = new WorkflowSession
        {
            SessionId = "outline-preserve",
            InitialInput = new BlogOverview("入力の具体例"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };
        await repository.CreateSessionAsync(session, CancellationToken.None);

        composerMock
            .Setup(x => x.ComposeAsync(
                WorkflowStep.Step1_Outline,
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { UserOverview = "prompt" });

        string? repairInput = null;
        composerMock
            .Setup(x => x.ComposeOutlineRepair(
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<WorkflowOptions>()))
            .Callback<BlogOverview, StyleCard, string, string, WorkflowOptions>((_, _, raw, _, _) => repairInput = raw)
            .Returns(new Prompt { UserOverview = PromptComposer.OutlineRepairHeading });

        llmMock
            .SetupSequence(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "- 残すべき項目\n    - 深すぎる項目\nこの説明も保持対象",
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            })
            .ReturnsAsync(new Draft
            {
                Content = "{\"outline\":\"- 残すべき項目\",\"editorialMemo\":{}}",
                Model = "m1-repair",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None);

        Assert.That(result.Content, Is.EqualTo("- 残すべき項目"));
        Assert.That(repairInput, Does.Contain("- 残すべき項目"));
        Assert.That(repairInput, Does.Contain("    - 深すぎる項目"));
        Assert.That(repairInput, Does.Contain("この説明も保持対象"));
    }

    [Test]
    public async Task GenerateStepAsync_壊れた本文JSONは保存せず再試行可能エラーにする()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, composer: new PromptComposer());

        var session = new WorkflowSession
        {
            SessionId = "invalid-draft-json",
            CurrentStep = WorkflowStep.Step2_Draft,
            InitialInput = new BlogOverview("短い入力"),
            OutlineConfirmed = "- 結論",
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };
        await repository.CreateSessionAsync(session, CancellationToken.None);

        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "{\"draft\":\"本文\",\"openQuestions\":[}",
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var ex = Assert.ThrowsAsync<LlmException>(() =>
            orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step2_Draft, false, CancellationToken.None));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.ErrorCode, Is.EqualTo("LLM_OUTPUT_INVALID"));
        Assert.That(ex.IsRetryable, Is.True);
        Assert.That(repository.Sessions[session.SessionId].DraftGenerated, Is.Null);
        Assert.That(repository.Sessions[session.SessionId].OpenQuestions, Is.Null);
        llmMock.Verify(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Exactly(2));
    }

    [Test]
    public async Task GenerateStepAsync_壊れた本文JSONを一度だけ再整形して保存する()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, composer: new PromptComposer());

        var session = new WorkflowSession
        {
            SessionId = "recover-draft-json",
            CurrentStep = WorkflowStep.Step2_Draft,
            InitialInput = new BlogOverview("短い入力"),
            OutlineConfirmed = "- 結論",
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };
        await repository.CreateSessionAsync(session, CancellationToken.None);

        llmMock
            .SetupSequence(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "{\"draft\":\"本文\",\"openQuestions\":[}",
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            })
            .ReturnsAsync(new Draft
            {
                Content = "{\"draft\":\"修復後の本文\",\"openQuestions\":[\"確認事項\"]}",
                Model = "m1-repair",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step2_Draft, false, CancellationToken.None);

        Assert.That(result.Content, Is.EqualTo("修復後の本文"));
        Assert.That(result.Model, Is.EqualTo("m1-repair"));
        Assert.That(result.OpenQuestions, Is.EqualTo(new[] { "確認事項" }));
        Assert.That(repository.Sessions[session.SessionId].DraftGenerated, Is.EqualTo("修復後の本文"));
        Assert.That(repository.Sessions[session.SessionId].OpenQuestions, Is.EqualTo(new[] { "確認事項" }));
        llmMock.Verify(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Exactly(2));
    }

    [Test]
    public async Task GenerateStepAsync_Outline違反時_OutlineConstraintViolationExceptionを投げる()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var retrievalMock = new Mock<IRetrievalService>(MockBehavior.Strict);
        var composerMock = new Mock<IPromptComposer>();

        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, retrievalMock, composerMock);

        var session = new WorkflowSession
        {
            SessionId = "invalid-outline",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };

        await repository.CreateSessionAsync(session, CancellationToken.None);

        composerMock
            .Setup(x => x.ComposeAsync(
                WorkflowStep.Step1_Outline,
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { UserOverview = "prompt" });

        composerMock
            .Setup(x => x.ComposeOutlineRepair(
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<WorkflowOptions>()))
            .Returns(new Prompt { UserOverview = PromptComposer.OutlineRepairHeading });

        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = "これはアウトラインではありません。説明文だけです。",
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var ex = Assert.ThrowsAsync<OutlineConstraintViolationException>(() =>
            orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.SourceKind, Is.EqualTo(OutlineViolationSource.LlmGenerated));
    }

    [Test]
    public async Task GenerateStepAsync_1項目のアウトラインを受け入れる()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var composerMock = new Mock<IPromptComposer>();
        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, composerMock: composerMock);

        var session = new WorkflowSession
        {
            SessionId = "one-line",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };
        await repository.CreateSessionAsync(session, CancellationToken.None);

        composerMock
            .Setup(x => x.ComposeAsync(
                WorkflowStep.Step1_Outline,
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { UserOverview = "prompt" });

        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft { Content = "- 一点だけ", Model = "m1", GeneratedAt = DateTimeOffset.UtcNow });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None);

        Assert.That(result.Content, Is.EqualTo("- 一点だけ"));
    }

    [Test]
    public async Task GenerateStepAsync_JSON応答から編集メモを保存する()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var composerMock = new Mock<IPromptComposer>();
        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, composerMock: composerMock);

        var session = new WorkflowSession
        {
            SessionId = "json-outline",
            InitialInput = new BlogOverview("0123456789"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };
        await repository.CreateSessionAsync(session, CancellationToken.None);

        composerMock
            .Setup(x => x.ComposeAsync(
                WorkflowStep.Step1_Outline,
                It.IsAny<BlogOverview>(),
                It.IsAny<StyleCard>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { UserOverview = "prompt" });

        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(new Draft
            {
                Content = """
                    {
                      "outline": "- 背景\n- 結論",
                      "editorialMemo": { "articleQuestion": "なぜこの操作が必要か" }
                    }
                    """,
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None);

        Assert.That(result.Content, Is.EqualTo("- 背景\n- 結論"));
        Assert.That(result.EditorialMemoJson, Is.Not.Null.And.Not.Empty);
        var memo = System.Text.Json.JsonSerializer.Deserialize<EditorialMemo>(
            result.EditorialMemoJson!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(memo, Is.Not.Null);
        Assert.That(memo!.ArticleQuestion, Is.EqualTo("なぜこの操作が必要か"));
        Assert.That(repository.Sessions[session.SessionId].EditorialMemoJson, Is.EqualTo(result.EditorialMemoJson));
    }

    [Test]
    public async Task GenerateStepAsync_旧RAGスナップショットがあってもプロンプトに過去記事を入れない()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var retrievalMock = new Mock<IRetrievalService>(MockBehavior.Strict);
        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, retrievalMock, composer: new PromptComposer());

        var snapshot = new RagSnapshot
        {
            SnapshotId = "snap-old",
            SearchQuery = "query",
            SearchExecutedAt = DateTimeOffset.UtcNow,
            Chunks =
            [
                new RAGChunk
                {
                    Text = "SECRET_RAG_CHUNK_SHOULD_NOT_APPEAR",
                    Score = 0.9,
                    SourceTitle = "old",
                    SourceUrl = "https://example.com/old",
                },
            ],
            ChunkCount = 1,
        };
        await repository.UpsertSnapshotAsync(snapshot, CancellationToken.None);

        var session = new WorkflowSession
        {
            SessionId = "legacy-rag",
            InitialInput = new BlogOverview("0123456789 残すべきURL https://example.com/keep"),
            RagSnapshotId = snapshot.SnapshotId,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };
        await repository.CreateSessionAsync(session, CancellationToken.None);

        Prompt? sent = null;
        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .Callback<Prompt, CancellationToken, int?>((prompt, _, _) => sent = prompt)
            .ReturnsAsync(new Draft { Content = "- 一点だけ", Model = "m1", GeneratedAt = DateTimeOffset.UtcNow });

        await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step1_Outline, false, CancellationToken.None);

        Assert.That(sent, Is.Not.Null);
        Assert.That(sent!.FullPrompt, Does.Not.Contain("SECRET_RAG_CHUNK_SHOULD_NOT_APPEAR"));
        Assert.That(sent.FullPrompt, Does.Not.Contain("過去記事からの関連情報"));
        Assert.That(sent.FullPrompt, Does.Contain("https://example.com/keep"));
        retrievalMock.Verify(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GenerateStepAsync_メモ無し旧セッションでも本文生成できる()
    {
        var repository = new InMemoryWorkflowRepository();
        var lockService = new WorkflowSessionLock();
        var llmMock = new Mock<ILlmClient>();
        var retrievalMock = new Mock<IRetrievalService>(MockBehavior.Strict);
        var orchestrator = CreateOrchestrator(repository, lockService, llmMock, retrievalMock, composer: new PromptComposer());

        var session = new WorkflowSession
        {
            SessionId = "legacy-draft",
            CurrentStep = WorkflowStep.Step2_Draft,
            InitialInput = new BlogOverview("0123456789 入力の具体例"),
            OutlineConfirmed = "- 結論",
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            DeleteAt = DateTimeOffset.UtcNow.AddDays(1),
        };
        await repository.CreateSessionAsync(session, CancellationToken.None);

        Prompt? sent = null;
        llmMock
            .Setup(x => x.GenerateAsync(It.IsAny<Prompt>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .Callback<Prompt, CancellationToken, int?>((prompt, _, _) => sent = prompt)
            .ReturnsAsync(new Draft
            {
                Content = """{"draft":"# 本文\n\n入力の具体例を残す。","openQuestions":["要確認"]}""",
                Model = "m1",
                GeneratedAt = DateTimeOffset.UtcNow,
            });

        var result = await orchestrator.GenerateStepAsync(session.SessionId, WorkflowStep.Step2_Draft, false, CancellationToken.None);

        Assert.That(result.Content, Does.Contain("入力の具体例を残す"));
        Assert.That(result.OpenQuestions, Does.Contain("要確認"));
        Assert.That(sent, Is.Not.Null);
        Assert.That(sent!.UserOverview, Does.Contain("入力の具体例"));
        Assert.That(sent.UserOverview, Does.Contain("未設定。元入力全文と確定アウトラインを根拠にする"));
        retrievalMock.Verify(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static WorkflowOrchestrator CreateOrchestrator(
        InMemoryWorkflowRepository repository,
        WorkflowSessionLock lockService,
        Mock<ILlmClient>? llmMock = null,
        Mock<IRetrievalService>? retrievalMock = null,
        Mock<IPromptComposer>? composerMock = null,
        IPromptComposer? composer = null)
    {
        var options = Options.Create(new WorkflowOptions
        {
            DatabasePath = "./data/test.db",
            SessionRetentionDays = 30,
        });

        var llm = llmMock?.Object ?? new Mock<ILlmClient>().Object;
        var retrieval = retrievalMock?.Object ?? new Mock<IRetrievalService>().Object;
        composer ??= composerMock?.Object ?? new Mock<IPromptComposer>().Object;
        var outlineValidator = new OutlineValidator();
        var styleCard = new StyleCard { Title = "t", Content = "c", SystemPrompt = "s" };

        return new WorkflowOrchestrator(
            repository,
            retrieval,
            composer,
            llm,
            outlineValidator,
            styleCard,
            lockService,
            options,
            NullLogger<WorkflowOrchestrator>.Instance);
    }

    private sealed class InMemoryWorkflowRepository : IWorkflowRepository
    {
        public Dictionary<string, WorkflowSession> Sessions { get; } = new();
        public Dictionary<string, RagSnapshot> Snapshots { get; } = new();

        public Task CreateSessionAsync(WorkflowSession session, CancellationToken cancellationToken)
        {
            Sessions[session.SessionId] = session;
            return Task.CompletedTask;
        }

        public Task<WorkflowSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            if (!Sessions.TryGetValue(sessionId, out var session))
            {
                throw new SessionNotFoundException(sessionId);
            }

            return Task.FromResult(session);
        }

        public Task UpdateSessionAsync(WorkflowSession session, CancellationToken cancellationToken)
        {
            Sessions[session.SessionId] = session;
            return Task.CompletedTask;
        }

        public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            Sessions.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task<List<WorkflowSession>> ListSessionsAsync(int skip, int take, CancellationToken cancellationToken)
        {
            return Task.FromResult(Sessions.Values.Skip(skip).Take(take).ToList());
        }

        public Task<List<WorkflowSession>> ListExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            return Task.FromResult(Sessions.Values.Where(x => x.DeleteAt <= now).ToList());
        }

        public Task<int> DeleteExpiredSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            var expired = Sessions.Values.Where(x => x.DeleteAt <= now).Select(x => x.SessionId).ToList();
            foreach (var id in expired)
            {
                Sessions.Remove(id);
            }

            return Task.FromResult(expired.Count);
        }

        public Task UpsertSnapshotAsync(RagSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshots[snapshot.SnapshotId] = snapshot;
            return Task.CompletedTask;
        }

        public Task<RagSnapshot> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken)
        {
            if (!Snapshots.TryGetValue(snapshotId, out var snapshot))
            {
                throw new WorkflowStorageException("スナップショットが見つかりません", new KeyNotFoundException(snapshotId));
            }

            return Task.FromResult(snapshot);
        }
    }
}

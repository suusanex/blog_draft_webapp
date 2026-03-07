using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Components.Pages;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using BlogDraftWebApp.Tests.Common.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using BunitTestContext = Bunit.TestContext;

namespace BlogDraftWebApp.Components.Tests.Pages;

public sealed class GenerateDraftTests
{
    private BunitTestContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _context = new BunitTestContext();
        _context.Services.AddSingleton<NavigationManager>(new StubNavigationManager("http://localhost/"));
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public void OverviewEmpty_DisablesProposalButton()
    {
        ConfigureHttpClient((_, _) => Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, PlanResponse())));
        var cut = _context.RenderComponent<GenerateDraft>();

        Assert.That(cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void InputGuide_DescribesCentralPointsAndPlanReview()
    {
        ConfigureHttpClient((_, _) => Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, PlanResponse())));
        var cut = _context.RenderComponent<GenerateDraft>();

        Assert.That(cut.Markup, Does.Contain("この記事で伝えたいポイント"));
        Assert.That(cut.Markup, Does.Contain("完全な目次ではなく"));
        Assert.That(cut.Markup, Does.Contain("編集計画を提案"));
    }

    [Test]
    public void ProposalFlow_ShowsReviewAndOrigin()
    {
        ConfigureHttpClient((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal)
            ? CreateJsonResponse(HttpStatusCode.OK, PlanResponse())
            : new HttpResponseMessage(HttpStatusCode.NotFound)));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Markup, Does.Contain("編集計画をレビュー"));
            Assert.That(cut.Markup, Does.Contain("入力由来"));
            Assert.That(cut.Markup, Does.Contain("中心ポイント"));
        });
    }

    [Test]
    public void SectionMaterials_ShowIdTextAndOriginForBodyAndExcludedItems()
    {
        ConfigureHttpClient((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal)
            ? CreateJsonResponse(HttpStatusCode.OK, PlanResponseWithExcludedScope())
            : new HttpResponseMessage(HttpStatusCode.NotFound)));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Markup, Does.Contain("focus-1: 重要ポイント"));
            Assert.That(cut.Markup, Does.Contain("excluded-1: 今回は扱わない範囲"));
            Assert.That(cut.Markup, Does.Contain("再整理"));
            Assert.That(cut.Markup, Does.Contain("編集上の推論"));
        });
    }

    [Test]
    public void ApprovedPlanFlow_GeneratesDraft()
    {
        ConfigureHttpClient((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, PlanResponse()));
            }

            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new GenerateDraftResponse
            {
                Draft = "# Title\n\nThis is a draft.",
            }));
        });

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("編集計画をレビュー")));
        cut.FindAll("button").Single(x => x.TextContent.Contains("この計画で下書きを生成")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Markup, Does.Contain("生成結果（Markdown）"));
            Assert.That(cut.Markup, Does.Contain("This is a draft."));
        });
    }

    [Test]
    public void ApprovedPlanPreview_ShowsWriterPrompt()
    {
        ConfigureHttpClient((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, PlanResponse()));
            }

            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new PreviewPromptResponse
            {
                Prompt = "approved writer prompt",
            }));
        });

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("編集計画をレビュー")));
        cut.FindAll("button").Single(x => x.TextContent.Contains("Writer入力をプレビュー")).Click();

        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("approved writer prompt")));
    }

    [Test]
    public void ResultCanReturnToPlan()
    {
        ConfigureHttpClient((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal)
            ? CreateJsonResponse(HttpStatusCode.OK, PlanResponse())
            : CreateJsonResponse(HttpStatusCode.OK, new GenerateDraftResponse { Draft = "# Title\n\nDraft" })));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("編集計画をレビュー")));
        cut.FindAll("button").Single(x => x.TextContent.Contains("この計画で下書きを生成")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("生成結果（Markdown）")));
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画へ戻る")).Click();

        Assert.That(cut.Markup, Does.Contain("編集計画をレビュー"));
    }

    [Test]
    public void ErrorResponse_ShowsRetryButton()
    {
        ConfigureHttpClient((_, _) => Task.FromResult(CreateJsonResponse(HttpStatusCode.BadGateway, new ErrorResponse
        {
            ErrorCode = "LLM_ERROR",
            Message = "LLM サービスでエラーが発生しました",
            IsRetryable = true,
        })));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();

        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("再試行")));
    }

    [Test]
    public void ProposalRetry_RepeatsPlanProposalInsteadOfLegacyGeneration()
    {
        var planCalls = 0;
        var draftCalls = 0;
        ConfigureHttpClient((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal))
            {
                planCalls++;
                return Task.FromResult(planCalls == 1
                    ? CreateJsonResponse(HttpStatusCode.BadGateway, new ErrorResponse { ErrorCode = "LLM_ERROR", Message = "retry", IsRetryable = true })
                    : CreateJsonResponse(HttpStatusCode.OK, PlanResponse()));
            }

            draftCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("再試行")));
        cut.FindAll("button").Single(x => x.TextContent.Contains("再試行")).Click();

        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("編集計画をレビュー")));
        Assert.That(planCalls, Is.EqualTo(2));
        Assert.That(draftCalls, Is.EqualTo(0));
    }

    [Test]
    public void PreviewRetry_RepeatsPreviewWithoutGeneratingDraft()
    {
        var previewCalls = 0;
        var draftCalls = 0;
        ConfigureHttpClient((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, PlanResponse()));
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/draft/preview", StringComparison.Ordinal))
            {
                previewCalls++;
                return Task.FromResult(previewCalls == 1
                    ? CreateJsonResponse(HttpStatusCode.BadGateway, new ErrorResponse { ErrorCode = "LLM_ERROR", Message = "retry", IsRetryable = true })
                    : CreateJsonResponse(HttpStatusCode.OK, new PreviewPromptResponse { Prompt = "writer prompt" }));
            }

            draftCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("編集計画をレビュー")));
        cut.FindAll("button").Single(x => x.TextContent.Contains("Writer入力をプレビュー")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("再試行")));
        cut.FindAll("button").Single(x => x.TextContent.Contains("再試行")).Click();

        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("writer prompt")));
        Assert.That(previewCalls, Is.EqualTo(2));
        Assert.That(draftCalls, Is.EqualTo(0));
    }

    [Test]
    public void DeletingMaterial_ClearsSectionReferenceAndShowsValidation()
    {
        ConfigureHttpClient((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal)
            ? CreateJsonResponse(HttpStatusCode.OK, PlanResponse())
            : new HttpResponseMessage(HttpStatusCode.NotFound)));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("編集計画をレビュー")));
        Assert.That(cut.FindAll("input[type='checkbox']"), Has.Count.EqualTo(2));

        cut.FindAll("button.btn-outline-danger")[1].Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.FindAll("input[type='checkbox']"), Has.Count.EqualTo(1));
            Assert.That(cut.Markup, Does.Contain("本文材料を1件以上選択してください"));
        });
    }

    [Test]
    public async Task CopyDraftAsync_CallsClipboardAndShowsMessage()
    {
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        ConfigureHttpClient((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/draft/plan", StringComparison.Ordinal)
            ? CreateJsonResponse(HttpStatusCode.OK, PlanResponse())
            : CreateJsonResponse(HttpStatusCode.OK, new GenerateDraftResponse { Draft = "# Title\n\nDraft" })));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.FindAll("button").Single(x => x.TextContent.Contains("編集計画を提案")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("編集計画をレビュー")));
        cut.FindAll("button").Single(x => x.TextContent.Contains("この計画で下書きを生成")).Click();
        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("生成結果（Markdown）")));
        await cut.InvokeAsync(() => cut.Find("button.btn-secondary").Click());

        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("コピーしました")));
    }

    private void ConfigureHttpClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _context.Services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(handler))));
    }

    private static EditorialPlanResponse PlanResponse() => new()
    {
        Plan = new EditorialPlan
        {
            Thesis = new BriefItem
            {
                Id = "thesis-1",
                Text = "中心ポイント",
                Origin = BriefItemOrigin.Input,
                SourceExcerpt = "0123456789",
            },
            FocalPoints =
            [
                new BriefItem
                {
                    Id = "focus-1",
                    Text = "重要ポイント",
                    Origin = BriefItemOrigin.Reorganized,
                    SourceExcerpt = "0123456789",
                },
            ],
            Sections =
            [
                new PlannedSection
                {
                    Id = "section-1",
                    Heading = "中心ポイント",
                    Purpose = "中心を説明する",
                    SourceItemIds = ["focus-1"],
                },
            ],
        },
        Model = "stub-model",
    };

    private static EditorialPlanResponse PlanResponseWithExcludedScope()
    {
        var response = PlanResponse();
        response.Plan.ExcludedScope =
        [
            new BriefItem
            {
                Id = "excluded-1",
                Text = "今回は扱わない範囲",
                Origin = BriefItemOrigin.InferredEditorialConstraint,
            },
        ];
        return response;
    }

    private static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return StubHttpMessageHandler.Json(statusCode, json);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager(string baseUri) => Initialize(baseUri, baseUri);
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
}


using System.Net;
using System.Text.Json;
using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Components.Pages;
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
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public void OverviewEmpty_KeepsGenerateButtonEnabled()
    {
        ConfigureHttpClient(CreateJsonResponse(HttpStatusCode.OK, new GenerateDraftResponse { Draft = "# Title" }));

        var cut = _context.RenderComponent<GenerateDraft>();

        var button = cut.Find("button");
        Assert.That(button.HasAttribute("disabled"), Is.False);
    }

    [Test]
    public void OverviewInput_KeepsGenerateButtonEnabled()
    {
        ConfigureHttpClient(CreateJsonResponse(HttpStatusCode.OK, new GenerateDraftResponse { Draft = "# Title" }));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");

        var button = cut.Find("button");
        Assert.That(button.HasAttribute("disabled"), Is.False);
    }

    [Test]
    public void GenerateAsync_Success_ShowsDraftAndPreview()
    {
        ConfigureHttpClient(CreateJsonResponse(HttpStatusCode.OK, new GenerateDraftResponse
        {
            Draft = "# Title\n\nThis is a draft.",
            Warning = null,
        }));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");

        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Markup, Does.Contain("生成結果（Markdown）"));
            Assert.That(cut.Markup, Does.Contain("This is a draft."));
        });
    }

    [Test]
    public void GenerateAsync_Error_ShowsErrorMessage()
    {
        ConfigureHttpClient(CreateJsonResponse(HttpStatusCode.BadRequest, new ErrorResponse
        {
            ErrorCode = "INVALID_REQUEST",
            Message = "エラーが発生しました",
            IsRetryable = false,
        }));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Markup, Does.Contain("エラーが発生しました"));
            Assert.That(cut.Markup, Does.Not.Contain("再試行"));
        });
    }

    [Test]
    public void GenerateAsync_RetryableError_ShowsRetryButton()
    {
        ConfigureHttpClient(CreateJsonResponse(HttpStatusCode.InternalServerError, new ErrorResponse
        {
            ErrorCode = "TIMEOUT",
            Message = "生成に時間がかかりすぎています。もう一度お試しください",
            IsRetryable = true,
        }));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Markup, Does.Contain("再試行"));
        });
    }

    [Test]
    public void GenerateAsync_DisablesHtmlInMarkdown()
    {
        ConfigureHttpClient(CreateJsonResponse(HttpStatusCode.OK, new GenerateDraftResponse
        {
            Draft = "# Title\n\n<script>alert('x')</script>",
        }));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Markup, Does.Not.Contain("<script>"));
        });
    }

    [Test]
    public async Task CopyDraftAsync_CallsClipboardAndShowsMessage()
    {
        var jsInterop = _context.JSInterop;
        jsInterop.Mode = JSRuntimeMode.Loose;

        ConfigureHttpClient(CreateJsonResponse(HttpStatusCode.OK, new GenerateDraftResponse
        {
            Draft = "# Title\n\nThis is a draft.",
        }));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("生成結果（Markdown）")));

        await cut.InvokeAsync(() => cut.Find("button.btn-secondary").Click());

        cut.WaitForAssertion(() => Assert.That(cut.Markup, Does.Contain("コピーしました")));
    }

    [Test]
    public void PreviewAsync_ShowsPromptAndWarning()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri is not null && request.RequestUri.AbsolutePath.EndsWith("/draft/preview", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new PreviewPromptResponse
                {
                    Prompt = "[システムプロンプト]\n...",
                    Warning = null,
                }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        ConfigureHttpClient(new HttpClient(handler));

        var cut = _context.RenderComponent<GenerateDraft>();
        cut.Find("textarea").Change("0123456789");
        cut.Find("#previewMode").Change(true);
        cut.Find("button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Markup, Does.Contain("LLM 入力内容（プレビュー）"));
            Assert.That(cut.Markup, Does.Contain("機密情報が含まれる可能性"));
        });
    }

    private void ConfigureHttpClient(HttpResponseMessage response)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(response));
        ConfigureHttpClient(new HttpClient(handler));
    }

    private void ConfigureHttpClient(HttpClient client)
    {
        _context.Services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(client));
    }

    private static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return StubHttpMessageHandler.Json(statusCode, json);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager(string baseUri)
        {
            Initialize(baseUri, baseUri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}


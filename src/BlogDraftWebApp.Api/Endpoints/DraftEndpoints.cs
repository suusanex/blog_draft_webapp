using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace BlogDraftWebApp.Api.Endpoints;

public static class DraftEndpoints
{
    private const string BrokenOutputWarning = "生成結果が空、または壊れている可能性があります";

    public static IEndpointRouteBuilder MapDraftEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/draft/preview", async (
            PreviewPromptRequest request,
            IPromptComposer promptComposer,
            StyleCard styleCard,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationError = ValidateOverview(request?.Overview, env, httpContext);
            if (validationError is not null)
            {
                return validationError;
            }

            var prompt = await promptComposer.ComposeAsync(new BlogOverview(request!.Overview), styleCard, cancellationToken);

            return Results.Ok(new PreviewPromptResponse
            {
                Prompt = prompt.FullPrompt,
                RagHitCount = 0,
            });
        });

        app.MapPost("/draft", async (
            GenerateDraftRequest request,
            IPromptComposer promptComposer,
            ILlmClient llmClient,
            StyleCard styleCard,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationError = ValidateOverview(request?.Overview, env, httpContext);
            if (validationError is not null)
            {
                return validationError;
            }

            var prompt = await promptComposer.ComposeAsync(new BlogOverview(request!.Overview), styleCard, cancellationToken);
            var generated = await llmClient.GenerateAsync(prompt, cancellationToken);
            var parsed = GeneratedContentParser.ParseDraft(generated.Content);
            if (!parsed.IsValid)
            {
                var initialGenerated = generated;
                generated = await llmClient.GenerateAsync(
                    promptComposer.ComposeDraftRepair(
                        new BlogOverview(request!.Overview),
                        styleCard,
                        generated.Content,
                        parsed.ErrorMessage ?? "下書き生成結果を解釈できませんでした。"),
                    cancellationToken);
                parsed = GeneratedContentParser.ParseDraft(generated.Content);
                if (!parsed.IsValid
                    && GeneratedContentParser.TryRecoverMarkdownDraft(generated.Content, out var recovered))
                {
                    parsed = recovered;
                }
                else if (!parsed.IsValid
                    && GeneratedContentParser.TryRecoverMarkdownDraft(initialGenerated.Content, out recovered))
                {
                    generated = initialGenerated;
                    parsed = recovered;
                }
            }

            if (!parsed.IsValid)
            {
                throw new LlmException(
                    "LLM_OUTPUT_INVALID",
                    parsed.ErrorMessage ?? "下書き生成結果を解釈できませんでした。",
                    isRetryable: true);
            }

            var warning = string.IsNullOrWhiteSpace(parsed.Draft) ? BrokenOutputWarning : null;

            return Results.Ok(new GenerateDraftResponse
            {
                Draft = parsed.Draft,
                Model = generated.Model,
                GeneratedAt = generated.GeneratedAt,
                RagHitCount = 0,
                Warning = warning,
                OpenQuestions = parsed.OpenQuestions.ToList(),
            });
        });

        return app;
    }

    private static IResult? ValidateOverview(string? overview, IHostEnvironment env, HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(overview))
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = "記事の概要を入力してください",
                Details = env.IsDevelopment() ? "Overview field is required" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        if (overview.Length > 5000)
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = "概要は 5000 文字以内で入力してください",
                Details = env.IsDevelopment() ? "Overview must be at most 5000 characters" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        return null;
    }
}

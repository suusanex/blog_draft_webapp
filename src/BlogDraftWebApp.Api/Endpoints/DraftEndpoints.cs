using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Api.Endpoints;

public static class DraftEndpoints
{
    public static IEndpointRouteBuilder MapDraftEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/draft/plan", async (
            EditorialPlanRequest request,
            IEditorialPlanService editorialPlanService,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationError = ValidateOverview(request?.Overview, env, httpContext);
            if (validationError is not null)
            {
                return validationError;
            }

            if (request!.Mode == PlanGenerationMode.SectionsOnly)
            {
                var currentPlanError = ValidateApprovedPlan(request.CurrentPlan, request.Overview, env, httpContext);
                if (currentPlanError is not null)
                {
                    return currentPlanError;
                }
            }

            var result = await editorialPlanService.ProposeAsync(
                request.Overview,
                request.Mode,
                request.CurrentPlan,
                cancellationToken);

            return Results.Ok(new EditorialPlanResponse
            {
                Plan = result.Plan,
                Model = result.Model,
                GeneratedAt = result.GeneratedAt,
            });
        });

        app.MapPost("/draft/preview", async (
            PreviewPromptRequest request,
            IRetrievalService retrievalService,
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

            var planError = ValidateApprovedPlan(request?.ApprovedPlan, request!.Overview, env, httpContext);
            if (planError is not null)
            {
                return planError;
            }

            var retrieval = await retrievalService.RetrieveAsync(request.Overview, cancellationToken);
            var prompt = request.ApprovedPlan is null
                ? await promptComposer.ComposeAsync(new BlogOverview(request.Overview), retrieval.Chunks, styleCard, cancellationToken)
                : await promptComposer.ComposeApprovedAsync(new BlogOverview(request.Overview), request.ApprovedPlan, retrieval.Chunks, styleCard, cancellationToken);

            return Results.Ok(new PreviewPromptResponse
            {
                Prompt = prompt.FullPrompt,
                RagHitCount = retrieval.Chunks.Count,
                Warning = retrieval.Warning,
            });
        });

        app.MapPost("/draft", async (
            GenerateDraftRequest request,
            IRetrievalService retrievalService,
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

            var planError = ValidateApprovedPlan(request?.ApprovedPlan, request!.Overview, env, httpContext);
            if (planError is not null)
            {
                return planError;
            }

            var retrieval = await retrievalService.RetrieveAsync(request.Overview, cancellationToken);
            var prompt = request.ApprovedPlan is null
                ? await promptComposer.ComposeAsync(new BlogOverview(request.Overview), retrieval.Chunks, styleCard, cancellationToken)
                : await promptComposer.ComposeApprovedAsync(new BlogOverview(request.Overview), request.ApprovedPlan, retrieval.Chunks, styleCard, cancellationToken);
            var draft = await llmClient.GenerateAsync(prompt, cancellationToken);

            var warning = retrieval.Warning;
            if (draft.Content.Trim().Length < 100)
            {
                warning = "生成結果が短すぎます。入力内容を詳しくするか、設定を確認してください";
            }

            return Results.Ok(new GenerateDraftResponse
            {
                Draft = draft.Content,
                Model = draft.Model,
                GeneratedAt = draft.GeneratedAt,
                RagHitCount = retrieval.Chunks.Count,
                Warning = warning,
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
                Message = BlogOverview.RequiredErrorMessage,
                Details = env.IsDevelopment() ? "Overview field is required" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        if (overview.Length < BlogOverview.MinimumLength)
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = BlogOverview.MinimumLengthErrorMessage,
                Details = env.IsDevelopment() ? "Overview must be at least 10 characters" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        if (overview.Length > BlogOverview.MaximumLength)
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = BlogOverview.MaximumLengthErrorMessage,
                Details = env.IsDevelopment() ? "Overview must be at most 5000 characters" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        return null;
    }

    private static IResult? ValidateApprovedPlan(
        EditorialPlan? plan,
        string overview,
        IHostEnvironment env,
        HttpContext httpContext)
    {
        if (plan is null)
        {
            return null;
        }

        try
        {
            EditorialPlanValidator.Validate(plan, overview);
            return null;
        }
        catch (EditorialPlanValidationException ex)
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = "編集計画が不正です",
                Details = env.IsDevelopment() ? ex.Message : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }
    }
}

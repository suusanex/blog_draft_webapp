using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BlogDraftWebApp.Api.Endpoints;

public static class WorkflowEndpoints
{
    private const int MaxDraftLength = 50000;

    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/workflow/sessions", async (
            CreateSessionRequest request,
            IWorkflowOrchestrator orchestrator,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationError = ValidateOverview(request?.Overview, env, httpContext);
            if (validationError is not null)
            {
                return validationError;
            }

            var session = await orchestrator.CreateSessionAsync(request!.Overview, cancellationToken);
            return Results.Created($"/workflow/sessions/{session.SessionId}", new CreateSessionResponse
            {
                SessionId = session.SessionId,
                CurrentStep = session.CurrentStep.ToString(),
                CreatedAt = session.CreatedAt,
                DeleteAt = session.DeleteAt,
            });
        });

        app.MapGet("/workflow/sessions/{sessionId}", async (
            string sessionId,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var session = await orchestrator.GetSessionAsync(sessionId, cancellationToken);
            return Results.Ok(new GetSessionResponse
            {
                SessionId = session.SessionId,
                CurrentStep = session.CurrentStep.ToString(),
                CreatedAt = session.CreatedAt,
                LastAccessedAt = session.LastAccessedAt,
                DeleteAt = session.DeleteAt,
                InitialInput = session.InitialInput.Content,
                RagSnapshotId = session.RagSnapshotId,
                OutlineGenerated = session.OutlineGenerated,
                OutlineEdited = session.OutlineEdited,
                OutlineConfirmed = session.OutlineConfirmed,
                EditorialMemoJson = session.EditorialMemoJson,
                DraftGenerated = session.DraftGenerated,
                DraftEdited = session.DraftEdited,
                DraftConfirmed = session.DraftConfirmed,
                OpenQuestions = session.OpenQuestions,
                TitleHookOptions = session.TitleHookOptions,
                TitleHookSelected = session.TitleHookSelected,
                TitleHookConfirmed = session.TitleHookConfirmed,
            });
        });

        app.MapGet("/workflow/sessions", async (
            int? page,
            int? pageSize,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var currentPage = page.GetValueOrDefault(1);
            var currentPageSize = pageSize.GetValueOrDefault(20);
            var sessions = await orchestrator.ListSessionsAsync(currentPage, currentPageSize, cancellationToken);

            return Results.Ok(new SessionListResponse
            {
                Page = currentPage,
                PageSize = currentPageSize,
                Sessions = sessions.Select(x => new SessionSummaryItem
                {
                    SessionId = x.SessionId,
                    CurrentStep = x.CurrentStep.ToString(),
                    CreatedAt = x.CreatedAt,
                    LastAccessedAt = x.LastAccessedAt,
                    DeleteAt = x.DeleteAt,
                }).ToList(),
            });
        });

        app.MapDelete("/workflow/sessions/{sessionId}", async (
            string sessionId,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            await orchestrator.DeleteSessionAsync(sessionId, cancellationToken);
            return Results.NoContent();
        });

        app.MapPost("/workflow/sessions/{sessionId}/rag/refresh", async (
            string sessionId,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var result = await orchestrator.RefreshRagSnapshotAsync(sessionId, cancellationToken);
            return Results.Ok(new RefreshRagSnapshotResponse
            {
                SessionId = sessionId,
                PreviousSnapshotId = result.PreviousSnapshotId,
                NewSnapshotId = result.NewSnapshotId,
                AddedChunkCount = result.Diff.AddedChunkCount,
                RemovedChunkCount = result.Diff.RemovedChunkCount,
                ChangeRate = result.Diff.ChangeRate,
                Confirmed = result.Confirmed,
            });
        });

        app.MapPost("/workflow/sessions/{sessionId}/rag/refresh/confirm", async (
            string sessionId,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var result = await orchestrator.ConfirmRagSnapshotRefreshAsync(sessionId, cancellationToken);
            return Results.Ok(new RefreshRagSnapshotResponse
            {
                SessionId = sessionId,
                PreviousSnapshotId = result.PreviousSnapshotId,
                NewSnapshotId = result.NewSnapshotId,
                AddedChunkCount = result.Diff.AddedChunkCount,
                RemovedChunkCount = result.Diff.RemovedChunkCount,
                ChangeRate = result.Diff.ChangeRate,
                Confirmed = result.Confirmed,
            });
        });

        app.MapPost("/workflow/sessions/{sessionId}/steps/outline/generate", async (
            string sessionId,
            GenerateStepRequest request,
            IWorkflowOrchestrator orchestrator,
            IOptions<WorkflowOptions> options,
            CancellationToken cancellationToken) =>
        {
            var result = await orchestrator.GenerateStepAsync(
                sessionId,
                WorkflowStep.Step1_Outline,
                request?.Regenerate ?? false,
                cancellationToken);

            return Results.Ok(new GenerateStepResponse
            {
                SessionId = sessionId,
                Step = "outline",
                Generated = result.Content,
                RagHitCount = result.RagHitCount,
                Model = result.Model,
                GeneratedAt = result.GeneratedAt,
                Warning = result.Warning,
                OutlineMaxOutputTokens = options.Value.OutlineMaxOutputTokens,
                OpenQuestions = result.OpenQuestions.ToList(),
                EditorialMemoJson = result.EditorialMemoJson,
            });
        });

        app.MapPost("/workflow/sessions/{sessionId}/steps/outline/save", async (
            string sessionId,
            SaveStepRequest request,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            await orchestrator.SaveStepAsync(sessionId, WorkflowStep.Step1_Outline, request?.EditedContent ?? string.Empty, cancellationToken);
            return Results.Ok(new { sessionId, step = "outline", saved = true });
        });

        app.MapPost("/workflow/sessions/{sessionId}/steps/outline/confirm", async (
            string sessionId,
            ConfirmStepRequest request,
            IWorkflowOrchestrator orchestrator,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationError = ValidateOutline(request?.ConfirmedContent, env, httpContext);
            if (validationError is not null)
            {
                return validationError;
            }

            var result = await orchestrator.ConfirmStepAsync(sessionId, WorkflowStep.Step1_Outline, request!.ConfirmedContent, cancellationToken);
            return Results.Ok(new
            {
                sessionId,
                step = "outline",
                confirmed = true,
                nextStep = MapStepName(result.NextStep),
            });
        });

        app.MapPost("/workflow/sessions/{sessionId}/steps/outline/preview", async (
            string sessionId,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var preview = await orchestrator.PreviewStepAsync(sessionId, WorkflowStep.Step1_Outline, cancellationToken);
            return Results.Ok(new PreviewPromptResponse
            {
                SessionId = sessionId,
                Step = "outline",
                Prompt = preview.Prompt,
                RagHitCount = preview.RagHitCount,
                RagSnapshotId = preview.RagSnapshotId,
                Warning = preview.Warning,
            });
        });

        app.MapPost("/workflow/sessions/{sessionId}/steps/draft/generate", async (
            string sessionId,
            GenerateStepRequest request,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var result = await orchestrator.GenerateStepAsync(
                sessionId,
                WorkflowStep.Step2_Draft,
                request?.Regenerate ?? false,
                cancellationToken);

            return Results.Ok(new GenerateStepResponse
            {
                SessionId = sessionId,
                Step = "draft",
                Generated = result.Content,
                RagHitCount = result.RagHitCount,
                Model = result.Model,
                GeneratedAt = result.GeneratedAt,
                Warning = result.Warning,
                OpenQuestions = result.OpenQuestions.ToList(),
                EditorialMemoJson = result.EditorialMemoJson,
            });
        });

        app.MapPost("/workflow/sessions/{sessionId}/steps/draft/save", async (
            string sessionId,
            SaveStepRequest request,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            await orchestrator.SaveStepAsync(sessionId, WorkflowStep.Step2_Draft, request?.EditedContent ?? string.Empty, cancellationToken);
            return Results.Ok(new { sessionId, step = "draft", saved = true });
        });

        app.MapPost("/workflow/sessions/{sessionId}/steps/draft/confirm", async (
            string sessionId,
            ConfirmStepRequest request,
            IWorkflowOrchestrator orchestrator,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationError = ValidateDraft(request?.ConfirmedContent, env, httpContext);
            if (validationError is not null)
            {
                return validationError;
            }

            var result = await orchestrator.ConfirmStepAsync(sessionId, WorkflowStep.Step2_Draft, request!.ConfirmedContent, cancellationToken);
            return Results.Ok(new
            {
                sessionId,
                step = "draft",
                confirmed = true,
                nextStep = MapStepName(result.NextStep),
            });
        });

        app.MapPost("/workflow/sessions/{sessionId}/steps/draft/preview", async (
            string sessionId,
            IWorkflowOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var preview = await orchestrator.PreviewStepAsync(sessionId, WorkflowStep.Step2_Draft, cancellationToken);
            return Results.Ok(new PreviewPromptResponse
            {
                SessionId = sessionId,
                Step = "draft",
                Prompt = preview.Prompt,
                RagHitCount = preview.RagHitCount,
                RagSnapshotId = preview.RagSnapshotId,
                Warning = preview.Warning,
            });
        });

        return app;
    }

    private static string? MapStepName(WorkflowStep? step)
    {
        if (step is null)
        {
            return null;
        }

        return step switch
        {
            WorkflowStep.Step1_Outline => "outline",
            WorkflowStep.Step2_Draft => "draft",
            WorkflowStep.Step3_TitleHook => "titlehook",
            WorkflowStep.Completed => "completed",
            _ => step.Value.ToString().ToLowerInvariant(),
        };
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

    private static IResult? ValidateOutline(string? outline, IHostEnvironment env, HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(outline))
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = "アウトラインを入力してください",
                Details = env.IsDevelopment() ? "Outline field is required" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        return null;
    }

    private static IResult? ValidateDraft(string? draft, IHostEnvironment env, HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(draft))
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = "下書きを入力してください",
                Details = env.IsDevelopment() ? "Draft field is required" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        if (draft.Length > MaxDraftLength)
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = $"下書きは {MaxDraftLength} 文字以内で入力してください",
                Details = env.IsDevelopment() ? $"Draft must be at most {MaxDraftLength} characters" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        return null;
    }
}

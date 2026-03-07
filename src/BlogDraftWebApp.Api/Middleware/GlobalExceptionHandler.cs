using System.Net;
using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlogDraftWebApp.Api.Middleware;

public sealed class GlobalExceptionHandler : IMiddleware
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly LlmErrorClassifier _llmErrorClassifier;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment hostEnvironment,
        LlmErrorClassifier llmErrorClassifier)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _llmErrorClassifier = llmErrorClassifier;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var requestId = context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception. RequestId={RequestId}", requestId);
            Console.Error.WriteLine($"Unhandled exception. RequestId={requestId}\n{ex}");

            var (statusCode, response) = CreateResponse(ex, requestId);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(response);
        }
    }

    private (int StatusCode, ErrorResponse Response) CreateResponse(Exception exception, string requestId)
    {
        if (exception is ConfigurationException)
        {
            return (StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "CONFIG_ERROR",
                Message = "システムが正しく構成されていません。管理者に連絡してください",
                Details = _hostEnvironment.IsDevelopment() ? exception.Message : null,
                RequestId = requestId,
                IsRetryable = false,
            });
        }

        if (exception is LlmException)
        {
            var info = _llmErrorClassifier.Classify(exception);
            return (info.HttpStatusCode, new ErrorResponse
            {
                ErrorCode = info.ErrorCode,
                Message = info.Message,
                Details = _hostEnvironment.IsDevelopment() ? exception.Message : null,
                RequestId = requestId,
                IsRetryable = info.IsRetryable,
            });
        }

        if (exception is RagException)
        {
            return (StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "RAG_ERROR",
                Message = "関連記事の検索に失敗しました",
                Details = _hostEnvironment.IsDevelopment() ? exception.Message : null,
                RequestId = requestId,
                IsRetryable = true,
            });
        }

        if (exception is InvalidStateTransitionException)
        {
            return (StatusCodes.Status400BadRequest, new ErrorResponse
            {
                ErrorCode = "INVALID_STEP_TRANSITION",
                Message = exception.Message,
                Details = _hostEnvironment.IsDevelopment() ? exception.Message : null,
                RequestId = requestId,
                IsRetryable = false,
            });
        }

        if (exception is OutlineConstraintViolationException outlineViolation)
        {
            if (outlineViolation.SourceKind == OutlineViolationSource.LlmGenerated)
            {
                return (StatusCodes.Status502BadGateway, new ErrorResponse
                {
                    ErrorCode = "OUTLINE_GENERATION_INVALID",
                    Message = "アウトライン生成結果が制約を満たしませんでした。まず再生成を試してください。繰り返す場合は不具合の可能性があります",
                    Details = _hostEnvironment.IsDevelopment() ? outlineViolation.Message : null,
                    RequestId = requestId,
                    IsRetryable = true,
                });
            }

            return (StatusCodes.Status400BadRequest, new ErrorResponse
            {
                ErrorCode = "OUTLINE_CONSTRAINT_VIOLATION",
                Message = "アウトラインの形式が不正です。編集してから再度確定してください",
                Details = _hostEnvironment.IsDevelopment() ? outlineViolation.Message : null,
                RequestId = requestId,
                IsRetryable = false,
            });
        }

        if (exception is SessionNotFoundException sessionNotFound)
        {
            var status = sessionNotFound.IsExpired ? StatusCodes.Status410Gone : StatusCodes.Status404NotFound;
            var code = sessionNotFound.IsExpired ? "SESSION_EXPIRED" : "SESSION_NOT_FOUND";
            var message = sessionNotFound.IsExpired ? "セッションの有効期限が切れています" : "セッションが見つかりません";

            return (status, new ErrorResponse
            {
                ErrorCode = code,
                Message = message,
                Details = _hostEnvironment.IsDevelopment() ? exception.Message : null,
                RequestId = requestId,
                IsRetryable = false,
            });
        }

        if (exception is SessionBusyException)
        {
            return (StatusCodes.Status409Conflict, new ErrorResponse
            {
                ErrorCode = "SESSION_BUSY",
                Message = "生成中です。しばらくお待ちください",
                Details = _hostEnvironment.IsDevelopment() ? exception.Message : null,
                RequestId = requestId,
                IsRetryable = true,
            });
        }

        if (exception is WorkflowStorageException)
        {
            return (StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "STORAGE_ERROR",
                Message = "セッションの保存に失敗しました",
                Details = _hostEnvironment.IsDevelopment() ? exception.Message : null,
                RequestId = requestId,
                IsRetryable = true,
            });
        }

        return (StatusCodes.Status500InternalServerError, new ErrorResponse
        {
            ErrorCode = "INTERNAL_ERROR",
            Message = "サーバ内部エラーが発生しました",
            Details = null,
            RequestId = requestId,
            IsRetryable = false,
        });
    }
}

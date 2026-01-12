using System.Net;
using System.Text.Json;
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

            var (statusCode, response) = CreateResponse(ex, requestId);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
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

        return (StatusCodes.Status500InternalServerError, new ErrorResponse
        {
            ErrorCode = "INTERNAL_ERROR",
            Message = "サーバ内部エラーが発生しました",
            Details = _hostEnvironment.IsDevelopment() ? exception.Message : null,
            RequestId = requestId,
            IsRetryable = false,
        });
    }
}

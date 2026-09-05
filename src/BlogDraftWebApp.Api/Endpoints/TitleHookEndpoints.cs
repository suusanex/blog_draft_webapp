using BlogDraftWebApp.Api.Models;
using BlogDraftWebApp.Core.Models;
using BlogDraftWebApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace BlogDraftWebApp.Api.Endpoints;

public static class TitleHookEndpoints
{
    private const int MaxArticleBodyLength = 50000;

    public static IEndpointRouteBuilder MapTitleHookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/titlehook/preview", async (
            GenerateTitleHookRequest request,
            IPromptComposer promptComposer,
            StyleCard styleCard,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationError = ValidateArticleBody(request?.ArticleBody, env, httpContext);
            if (validationError is not null)
            {
                return validationError;
            }

            var prompt = await promptComposer.ComposeTitleHookAsync(request!.ArticleBody, styleCard, cancellationToken);
            return Results.Ok(new PreviewPromptResponse
            {
                Step = "titlehook",
                Prompt = prompt.FullPrompt,
                RagHitCount = 0,
            });
        });

        app.MapPost("/titlehook", async (
            GenerateTitleHookRequest request,
            IPromptComposer promptComposer,
            ILlmClient llmClient,
            StyleCard styleCard,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validationError = ValidateArticleBody(request?.ArticleBody, env, httpContext);
            if (validationError is not null)
            {
                return validationError;
            }

            var prompt = await promptComposer.ComposeTitleHookAsync(request!.ArticleBody, styleCard, cancellationToken);
            var generated = await llmClient.GenerateAsync(prompt, cancellationToken);
            var options = ParseTitleHookOptions(generated.Content);

            var warning = options.Count < 3
                ? "3案に満たないため、生成結果を補完しました"
                : null;

            return Results.Ok(new GenerateTitleHookResponse
            {
                Options = options,
                Model = generated.Model,
                GeneratedAt = generated.GeneratedAt,
                Warning = warning,
            });
        });

        return app;
    }

    private static IResult? ValidateArticleBody(string? articleBody, IHostEnvironment env, HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(articleBody))
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = "本文を入力してください",
                Details = env.IsDevelopment() ? "ArticleBody field is required" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        if (articleBody.Length > MaxArticleBodyLength)
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_REQUEST",
                Message = $"本文は {MaxArticleBodyLength} 文字以内で入力してください",
                Details = env.IsDevelopment() ? $"ArticleBody must be at most {MaxArticleBodyLength} characters" : null,
                RequestId = httpContext.TraceIdentifier,
                IsRetryable = false,
            });
        }

        return null;
    }

    private static List<TitleHook> ParseTitleHookOptions(string content)
    {
        var sections = content.Split("\n---\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var options = new List<TitleHook>();

        for (var i = 0; i < sections.Length && i < 3; i++)
        {
            var parsed = ParseTitleHook(sections[i], i + 1);
            options.Add(parsed);
        }

        if (options.Count == 0)
        {
            options.Add(ParseTitleHook(content, 1));
        }

        while (options.Count < 3)
        {
            var template = options[0];
            options.Add(new TitleHook
            {
                Title = $"{template.Title} ({options.Count + 1})",
                HookText = template.HookText,
                OptionIndex = options.Count + 1,
            });
        }

        return options;
    }

    private static TitleHook ParseTitleHook(string content, int optionIndex)
    {
        var normalized = content.Replace("\r\n", "\n").Trim();
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            return new TitleHook
            {
                Title = "タイトル案を編集してください",
                HookText = "導入文を編集してください。",
                OptionIndex = optionIndex,
            };
        }

        var title = lines[0].TrimStart('#', '-', '*', ' ').Trim();
        var hook = lines.Length > 1
            ? string.Join(Environment.NewLine, lines.Skip(1)).Trim()
            : "導入文を追加してください。";

        var model = new TitleHook
        {
            Title = title,
            HookText = hook,
            OptionIndex = optionIndex,
        };

        model.Validate();
        return model;
    }
}


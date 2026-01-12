using Azure;
using BlogDraftWebApp.Core.Exceptions;

namespace BlogDraftWebApp.Core.Services;

public sealed record LlmErrorInfo(string ErrorCode, string Message, bool IsRetryable, int HttpStatusCode);

public sealed class LlmErrorClassifier
{
    public LlmErrorInfo Classify(Exception exception)
    {
        if (exception is LlmException llmException)
        {
            if (string.Equals(llmException.ErrorCode, "LLM_TIMEOUT", StringComparison.Ordinal))
            {
                return new LlmErrorInfo("LLM_TIMEOUT", "生成に時間がかかりすぎています。もう一度お試しください", true, 504);
            }

            if (string.Equals(llmException.ErrorCode, "CONFIG_ERROR", StringComparison.Ordinal))
            {
                return new LlmErrorInfo("CONFIG_ERROR", "システムが正しく構成されていません。管理者に連絡してください", false, 500);
            }

            return new LlmErrorInfo("LLM_ERROR", "LLM サービスでエラーが発生しました", llmException.IsRetryable, 502);
        }

        if (exception is TimeoutException or TaskCanceledException)
        {
            return new LlmErrorInfo("LLM_TIMEOUT", "生成に時間がかかりすぎています。もう一度お試しください", true, 504);
        }

        if (exception is RequestFailedException requestFailedException)
        {
            if (requestFailedException.Status is 401 or 403)
            {
                return new LlmErrorInfo("CONFIG_ERROR", "システムが正しく構成されていません。管理者に連絡してください", false, 500);
            }

            if (requestFailedException.Status is 429 or 503 or 502 or 504)
            {
                return new LlmErrorInfo("LLM_ERROR", "LLM サービスでエラーが発生しました", true, 502);
            }

            return new LlmErrorInfo("LLM_ERROR", "LLM サービスでエラーが発生しました", false, 502);
        }

        return new LlmErrorInfo("LLM_ERROR", "LLM サービスでエラーが発生しました", false, 502);
    }
}


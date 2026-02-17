using Microsoft.JSInterop;

namespace BlogDraftWebApp.Services;

public sealed class ClipboardService
{
    private readonly IJSRuntime _jsRuntime;

    public ClipboardService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ValueTask CopyTextAsync(string text)
    {
        return _jsRuntime.InvokeVoidAsync("blogDraftClipboard.copyText", text);
    }
}

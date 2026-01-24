using Microsoft.Playwright;

namespace BlogDraftWebApp.E2E.Tests.Pages;

public sealed class GenerateDraftPageTests
{
    private static string BaseUrl => Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? string.Empty;

    private static void SkipIfMissingBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            Assert.Ignore("E2E_BASE_URL が未設定のため E2E テストをスキップします。");
        }
    }

    [Test]
    public async Task GenerateDraftFlow_ShowsDraftResult()
    {
        SkipIfMissingBaseUrl();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/generate");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.FillAsync("textarea", "0123456789");
        await page.Locator("textarea").BlurAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('button')?.disabled");
        await page.ClickAsync("button");
        await page.Locator("text=生成結果（Markdown）").WaitForAsync();
    }

    [Test]
    public async Task PreviewFlow_ShowsPromptPreview()
    {
        SkipIfMissingBaseUrl();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/generate");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.CheckAsync("#previewMode");
        await page.FillAsync("textarea", "0123456789");
        await page.Locator("textarea").BlurAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('button')?.disabled");
        await page.ClickAsync("button");
        await page.Locator("text=LLM 入力内容（プレビュー）").WaitForAsync();
    }
}

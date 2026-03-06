using Microsoft.Playwright;

namespace BlogDraftWebApp.E2E.Tests.Pages;

public sealed class WorkflowJourneyTests
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
    public async Task OutlineWorkflow_CanCreateSession()
    {
        SkipIfMissingBaseUrl();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/workflow/outline");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.FillAsync("textarea", "0123456789");
        await page.ClickAsync("button:has-text('セッションを作成')");
        await page.Locator("text=セッションID").WaitForAsync();
    }

    [Test]
    public async Task CompleteWorkflowJourney_OutlineToDraft_Completes()
    {
        SkipIfMissingBaseUrl();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        await CreateSessionAsync(page);
        await page.FillAsync("textarea", new string('O', 80));
        await page.ClickAsync("button:has-text('確定')");

        await page.WaitForURLAsync("**/workflow/draft/**");
        await page.FillAsync("textarea", new string('D', 150));
        await page.ClickAsync("button:has-text('確定')");

        await page.Locator("text=現在のステップ: Completed").WaitForAsync();
    }

    [Test]
    public async Task SessionResume_AfterReload_KeepsOutlineContent()
    {
        SkipIfMissingBaseUrl();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        await CreateSessionAsync(page);
        var outline = new string('R', 80);
        await page.FillAsync("textarea", outline);
        await page.ClickAsync("button:has-text('保存')");

        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var value = await page.InputValueAsync("textarea");
        Assert.That(value, Is.EqualTo(outline));
    }

    [Test]
    public async Task PreviewMode_ShowsWarningBanner()
    {
        SkipIfMissingBaseUrl();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        await CreateSessionAsync(page);
        await page.ClickAsync("button:has-text('プロンプトを確認')");
        await page.Locator("text=この内容は機微情報を含みます").WaitForAsync();
    }

    [Test]
    public async Task SessionDeletion_FromAdminPage_Works()
    {
        SkipIfMissingBaseUrl();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        await CreateSessionAsync(page);
        var sessionText = await page.Locator("text=セッションID:").InnerTextAsync();
        var sessionId = sessionText.Replace("セッションID:", string.Empty).Trim();

        await page.GotoAsync($"{BaseUrl}/workflow/admin");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.ClickAsync($"tr:has-text('{sessionId}') button:has-text('削除')");
        await page.ClickAsync($"tr:has-text('{sessionId}') button:has-text('削除を確定')");

        await page.WaitForTimeoutAsync(500);
        var rowCount = await page.Locator($"tr:has-text('{sessionId}')").CountAsync();
        Assert.That(rowCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CompletedWorkflow_ShowsLinkToTitleHookFeature()
    {
        SkipIfMissingBaseUrl();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        await CreateSessionAsync(page);
        await page.FillAsync("textarea", new string('O', 80));
        await page.ClickAsync("button:has-text('確定')");
        await page.WaitForURLAsync("**/workflow/draft/**");

        await page.FillAsync("textarea", new string('D', 150));
        await page.ClickAsync("button:has-text('確定')");

        await page.Locator("a:has-text('別機能: タイトル・導入文案へ')").WaitForAsync();
    }

    private static async Task CreateSessionAsync(IPage page)
    {
        await page.GotoAsync($"{BaseUrl}/workflow/outline");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.FillAsync("textarea", "0123456789");
        await page.ClickAsync("button:has-text('セッションを作成')");
        await page.Locator("text=セッションID").WaitForAsync();
    }
}

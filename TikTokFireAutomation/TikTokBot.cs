using Microsoft.Playwright;
using Serilog;

namespace TikTokFireAutomation;

public class TikTokBot
{
    private readonly AppConfig _config;
    private readonly Random _random = new();
    private readonly CookieManager _cookieManager = new();  

    public TikTokBot(AppConfig config)
    {
        _config = config;
    }

    public async Task RunAsync()
    {
        using var playwright = await Playwright.CreateAsync();
        string profilePath = Path.Combine(Directory.GetCurrentDirectory(), "user_profile");

        var contextOptions = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = _config.Headless,
            SlowMo = 500,
            UserAgent = !string.IsNullOrEmpty(_config.CustomUserAgent) 
                ? _config.CustomUserAgent 
                : "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:152.0) Gecko/20100101 Firefox/152.0"
        };

        var browserType = PlaywrightHelper.ResolveBrowserType(playwright, _config.CustomUserAgent);
        
        Log.Information("Launching browser with persistent profile...");
        await using var context = await browserType.LaunchPersistentContextAsync(profilePath, contextOptions);
        var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();

        await _cookieManager.ImportCookiesAsync(context, "cookies.json");

        Log.Information("Go to TikTok Messages...");
        await page.GotoAsync("https://www.tiktok.com/messages");
        
        if (!await CheckSessionAsync(page)) return;

        var genericChatLocator = page.Locator("[data-e2e='dm-new-conversation-item']").First;
        await ProcessFriendsAsync(page, genericChatLocator);
    }
    
    private async Task<bool> CheckSessionAsync(IPage page)
    {
        var chatLocator = page.Locator("[data-e2e='dm-new-conversation-item']").First;

        try
        {
            Log.Information("Checking active session...");
            await chatLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 7000 });
            Log.Information("Session active! Moving forward.");
            return true;
        }
        catch (TimeoutException)
        {
            if (_config.Headless)
            {
                Log.Fatal("Session not found! Cannot login manually in Headless mode on server. Run locally first.");
                return false;
            }

            Log.Warning("Session expired or empty. Redirecting to login page...");
            await page.GotoAsync("https://www.tiktok.com/login");
            Log.Information("Please log in to your account. Waiting up to 3 minutes...");

            try
            {
                await page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 180000 });
                Log.Information("Login detected! Redirecting back to messages...");

                await page.GotoAsync("https://www.tiktok.com/messages");
            
                await chatLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 25000 });
                Log.Information("Session successfully updated and active!");
                return true;
            }
            catch (TimeoutException)
            {
                Log.Fatal("Inactivity timeout. User failed to login within 3 minutes.");
                return false;
            }
        }
    }
    private async Task ProcessFriendsAsync(IPage page, ILocator genericChatLocator)
    {
        foreach (var friend in _config.NameStreaks)
        {
            Log.Information("Start target searching : {Friend}", friend);
            bool found = await ScrollToFriendAsync(page, genericChatLocator, friend);

            if (!found)
            {
                Log.Warning("Could not find the target {Friend} after reaching the maximum number of scrolls. Skipping", friend);
                continue;
            }

            await SendMessageToFriendAsync(page, friend);
            await Task.Delay(_random.Next(3000, 6000)); 
        }
        Log.Information("All contacts in the list have been processed.");
    }

    private async Task<bool> ScrollToFriendAsync(IPage page, ILocator genericChatLocator, string friend)
    {
        var targetChatLocator = page.Locator("[data-e2e='dm-new-conversation-item']")
            .Filter(new()
            {
                Has = page.Locator("[data-e2e='dm-new-conversation-nickname']").Filter(new() { HasText = friend })
            });

        for (int i = 0; i < 40; i++)
        {
            if (await targetChatLocator.CountAsync() > 0 && await targetChatLocator.First.IsVisibleAsync())
            {
                Log.Debug("Target {Friend} founded on screen.", friend);
                return true;
            }

            Log.Debug("Scroll chat panel... Attempt {Attempt}", i + 1);
            if (await genericChatLocator.CountAsync() > 0)
            {
                await genericChatLocator.HoverAsync();
                await page.Keyboard.PressAsync("PageDown");
            }
            await Task.Delay(3000);
        }
        return false;
    }

    private async Task SendMessageToFriendAsync(IPage page, string friend)
    {
        try
        {
            var targetChatLocator = page.Locator("[data-e2e='dm-new-conversation-item']")
                .Filter(new() { Has = page.Locator("[data-e2e='dm-new-conversation-nickname']").Filter(new() { HasText = friend }) });

            await targetChatLocator.First.ClickAsync();
            Log.Information("Chat with {Friend} opened. Waiting for an input field...", friend);

            var inputArea = page.Locator("[data-e2e='dm-new-input-editor'] [contenteditable='true']");
            await inputArea.WaitForAsync(new() { Timeout = 5000 });
            await inputArea.ClickAsync();

            await inputArea.TypeAsync(_config.DefaultText, new() { Delay = 100 });
            await Task.Delay(1000);
            await page.Keyboard.PressAsync("Enter");

            Log.Information("The message was successfully sent to {Friend}!", friend);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while sending a message to the target {Friend}", friend);
            await page.ScreenshotAsync(new() { Path = $"logs/error_{friend}.png" });
        }
    }
}
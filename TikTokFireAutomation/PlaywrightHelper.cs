using Microsoft.Playwright;
using Serilog;

namespace TikTokFireAutomation;

public static class PlaywrightHelper
{
    public static IBrowserType ResolveBrowserType(IPlaywright playwright, string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            Log.Information("User-Agent не задан. По умолчанию используем Firefox.");
            return playwright.Firefox;
        }

        var uaLower = userAgent.ToLower();

        if (uaLower.Contains("firefox"))
        {
            Log.Information("Browser engine: Firefox");
            return playwright.Firefox;
        }
        if (uaLower.Contains("chrome") || uaLower.Contains("chromium") || uaLower.Contains("edge"))
        {
            Log.Information("Browser engine: Chromium");
            return playwright.Chromium;
        }
        if (uaLower.Contains("safari") && !uaLower.Contains("chrome"))
        {
            Log.Information("Browser engine: WebKit (Safari)");
            return playwright.Webkit;
        }

        Log.Warning("Unable to accurately identify the browser based on the User-Agent. Fallback to default Firefox.");
        return playwright.Firefox;
    }
}
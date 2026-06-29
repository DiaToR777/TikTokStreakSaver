using System.Text.Json;
using Microsoft.Playwright;
using Serilog;

namespace TikTokFireAutomation;

public class CookieManager
{
    public async Task ImportCookiesAsync(IBrowserContext context, string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.Warning("Cookie file {FilePath} not found. Skip import.", filePath);
            return;
        }

        Log.Information("Founded cookies.json. Import cookies into profile...");
        try
        {
            var cookiesJson = await File.ReadAllTextAsync(filePath);
            using var doc = JsonDocument.Parse(cookiesJson);
            var cookiesList = new List<Cookie>();
            JsonElement cookiesArray;

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                cookiesArray = doc.RootElement;
            else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("cookies", out var cookiesProp) && cookiesProp.ValueKind == JsonValueKind.Array)
                cookiesArray = cookiesProp;
            else
            {
                Log.Error("Invalid format cookies.json. File must be an array or an object with a key 'cookies'.");
                return;
            }

            foreach (var element in cookiesArray.EnumerateArray())
            {
                cookiesList.Add(new Cookie
                {
                    Name = element.GetProperty("name").GetString()!,
                    Value = element.GetProperty("value").GetString()!,
                    Domain = element.GetProperty("domain").GetString()!,
                    Path = element.GetProperty("path").GetString()!,
                    Secure = element.TryGetProperty("secure", out var s) && s.GetBoolean(),
                    HttpOnly = element.TryGetProperty("httpOnly", out var h) && h.GetBoolean()
                });
            }

            if (cookiesList.Count > 0)
            {
                await context.AddCookiesAsync(cookiesList);
                Log.Information("Success imported {Count} cookies.", cookiesList.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading or importing manual cookies from cookies.json");
        }
    }
}
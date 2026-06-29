using Serilog;
using System.Text.Json;
using TikTokFireAutomation;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/bot-log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("==========================================");
    Log.Information("Launching the program...");
    
    string configPath = "config.json";
    if (!File.Exists(configPath))
    {
        Log.Error("Config file config.json not found!");
        return;
    }
    
    var jsonText = await File.ReadAllTextAsync(configPath);
    var config = JsonSerializer.Deserialize<AppConfig>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    
    
    if (config == null || config.NameStreaks.Count == 0)
    {
        Log.Warning("List NameStreaks empty or config file invalid.");
        return;
    }
    
    if (string.IsNullOrWhiteSpace(config.DefaultText))
        config.DefaultText = "+";

    Log.Information("Config success loaded. Found targets: {Count}", config.NameStreaks.Count);

    var bot = new TikTokBot(config);
    await bot.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Critical error while the bot is running");
}
finally
{
    Log.CloseAndFlush(); 
}
namespace PluginsAutoUpdate;

public sealed record AutoUpdateConfig
{
  public int CheckIntervalMinutes { get; set; } = 30;
  public string? DailyUpdateTimeUtc { get; set; }
  public string? DiscordWebhookUrl { get; set; }
  // Key: PluginName (folder/dll name), Value: Repo string (owner/repo or URL)
  public Dictionary<string, string> Repositories { get; set; } = new();
}

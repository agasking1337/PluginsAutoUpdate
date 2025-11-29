namespace PluginsAutoUpdate;

public sealed record AutoUpdateConfig
{
  public int CheckIntervalMinutes { get; set; } = 30;
  // Key: PluginName (folder/dll name), Value: Repo string (owner/repo or URL)
  public Dictionary<string, string> Repositories { get; set; } = new();
}

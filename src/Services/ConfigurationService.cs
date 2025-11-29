using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tomlyn.Extensions.Configuration;

namespace PluginsAutoUpdate;

/// <summary>
/// Service implementation for configuration management.
/// </summary>
public sealed class ConfigurationService : IConfigurationService
{
  private readonly ILogger _logger;

  public ConfigurationService(ILogger logger)
  {
    _logger = logger;
  }

  public AutoUpdateConfig? LoadConfig()
  {
    try
    {
      var swiftlyRoot = GetSwiftlyRoot();
      var configDir = Path.Combine(swiftlyRoot, "configs", "plugins", "PluginsAutoUpdate");

      Directory.CreateDirectory(configDir);

      var configPath = Path.Combine(configDir, "config.toml");
      var exists = File.Exists(configPath);
      _logger.LogInformation("Using config path {Path} (exists: {Exists}).", configPath, exists);

      if (!exists)
      {
        CreateDefaultConfig(configPath);
      }

      var builder = new ConfigurationBuilder()
        .SetBasePath(configDir)
        .AddTomlFile("config.toml", optional: false, reloadOnChange: true);

      var root = builder.Build();
      var section = root.GetSection("PluginsAutoUpdate");
      var loaded = new AutoUpdateConfig();
      section.Bind(loaded);
      loaded.Repositories ??= new System.Collections.Generic.Dictionary<string, string>();

      return loaded;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to load config.");
      return null;
    }
  }

  public string GetSwiftlyRoot()
  {
    var managedDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
    return Path.GetFullPath(Path.Combine(managedDir, "..", "..", ".."));
  }

  public string GetPluginsDirectory()
  {
    return Path.Combine(GetSwiftlyRoot(), "plugins");
  }

  private void CreateDefaultConfig(string configPath)
  {
    var defaultToml = "[PluginsAutoUpdate]\n" +
                      "CheckIntervalMinutes = 30\n" +
                      "\n" +
                      "# Repositories are defined as PluginName = Repo string under this table:\n" +
                      "[PluginsAutoUpdate.Repositories]\n" +
                      "# Example1 = \"owner1/repo1\"\n" +
                      "# Example2 = \"https://github.com/owner2/repo2\"\n";

    File.WriteAllText(configPath, defaultToml);
    _logger.LogInformation("Created default TOML config at {Path}.", configPath);
  }
}

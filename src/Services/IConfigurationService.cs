namespace PluginsAutoUpdate;

/// <summary>
/// Service for managing plugin configuration.
/// </summary>
public interface IConfigurationService
{
  /// <summary>
  /// Loads the plugin configuration from disk.
  /// </summary>
  /// <returns>Configuration or null if failed to load</returns>
  AutoUpdateConfig? LoadConfig();

  /// <summary>
  /// Gets the Swiftly root directory path.
  /// </summary>
  /// <returns>Absolute path to Swiftly root</returns>
  string GetSwiftlyRoot();

  /// <summary>
  /// Gets the plugins directory path.
  /// </summary>
  /// <returns>Absolute path to plugins directory</returns>
  string GetPluginsDirectory();
}

namespace PluginsAutoUpdate;

/// <summary>
/// Service for managing plugin version detection and persistence.
/// </summary>
public interface IVersionService
{
  /// <summary>
  /// Gets the version of a plugin from its DLL file on disk.
  /// </summary>
  /// <param name="dllPath">Path to the plugin DLL</param>
  /// <returns>Version or null if not found</returns>
  Version? GetLocalVersion(string dllPath);

  /// <summary>
  /// Gets the version of a currently loaded plugin.
  /// </summary>
  /// <param name="pluginName">Plugin name/folder</param>
  /// <returns>Version or null if not found</returns>
  Version? GetLiveVersion(string pluginName);

  /// <summary>
  /// Persists the version information to a sidecar file.
  /// </summary>
  /// <param name="dllPath">Path to the plugin DLL</param>
  /// <param name="version">Version to persist</param>
  void PersistVersion(string dllPath, Version version);
}

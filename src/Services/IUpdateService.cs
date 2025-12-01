using SwiftlyS2.Shared.Commands;

namespace PluginsAutoUpdate;

/// <summary>
/// Service for managing plugin updates.
/// </summary>
public interface IUpdateService
{
  /// <summary>
  /// Checks all configured repositories for updates and applies them.
  /// </summary>
  /// <param name="context">Optional command context for replying to user</param>
  /// <param name="token">Cancellation token</param>
  Task CheckAllRepositoriesAsync(ICommandContext? context, CancellationToken token);

  Task CheckAllRepositoriesForUpdatesOnlyAsync(ICommandContext? context, CancellationToken token);

  /// <summary>
  /// Downloads and replaces a plugin with a new version.
  /// </summary>
  /// <param name="localPath">Local plugin DLL path</param>
  /// <param name="assetUrl">Download URL for the new version</param>
  /// <param name="newVersion">New version being installed</param>
  /// <param name="token">Cancellation token</param>
  Task DownloadAndReplaceAsync(string localPath, string assetUrl, Version? newVersion, CancellationToken token);
}

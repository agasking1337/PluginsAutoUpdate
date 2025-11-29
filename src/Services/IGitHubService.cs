namespace PluginsAutoUpdate;

/// <summary>
/// Service for interacting with GitHub API to fetch release information.
/// </summary>
public interface IGitHubService
{
  /// <summary>
  /// Gets the latest release information for a GitHub repository.
  /// </summary>
  /// <param name="owner">Repository owner</param>
  /// <param name="repo">Repository name</param>
  /// <param name="token">Cancellation token</param>
  /// <returns>Release information or null if not found</returns>
  Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string owner, string repo, CancellationToken token);
}

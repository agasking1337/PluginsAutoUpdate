using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PluginsAutoUpdate;

/// <summary>
/// Service implementation for GitHub API interactions.
/// </summary>
public sealed class GitHubService : IGitHubService
{
  private readonly HttpClient _httpClient;
  private readonly ILogger _logger;

  public GitHubService(HttpClient httpClient, ILogger logger)
  {
    _httpClient = httpClient;
    _logger = logger;
  }

  public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string owner, string repo, CancellationToken token)
  {
    var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
  
    try
    {
      using var response = await _httpClient.GetAsync(url, token);
      if (!response.IsSuccessStatusCode)
      {
        _logger.LogWarning("GitHub API returned {StatusCode} for {Owner}/{Repo}", response.StatusCode, owner, repo);
        return null;
      }

      await using var stream = await response.Content.ReadAsStreamAsync(token);
      using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);

      var root = doc.RootElement;
      var tagName = root.GetProperty("tag_name").GetString();
      Version? version = null;
      if (!string.IsNullOrEmpty(tagName))
      {
        var cleaned = tagName.TrimStart('v', 'V');
        Version.TryParse(cleaned, out version);
      }

      string? assetUrl = null;
      if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
      {
        foreach (var asset in assets.EnumerateArray())
        {
          var name = asset.GetProperty("name").GetString();
          if (string.IsNullOrEmpty(name))
            continue;

          if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
              name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
          {
            assetUrl = asset.GetProperty("browser_download_url").GetString();
            if (!string.IsNullOrEmpty(assetUrl))
              break;
          }
        }
      }

      return new GitHubReleaseInfo(version, assetUrl);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error fetching latest release for {Owner}/{Repo}", owner, repo);
      return null;
    }
  }
}

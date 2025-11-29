using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;

namespace PluginsAutoUpdate;

/// <summary>
/// Service implementation for plugin update operations.
/// </summary>
public sealed class UpdateService : IUpdateService
{
  private readonly IConfigurationService _configService;
  private readonly IVersionService _versionService;
  private readonly IGitHubService _gitHubService;
  private readonly ILogger _logger;
  private readonly HttpClient _httpClient;

  public UpdateService(
    IConfigurationService configService,
    IVersionService versionService,
    IGitHubService gitHubService,
    HttpClient httpClient,
    ILogger logger)
  {
    _configService = configService;
    _versionService = versionService;
    _gitHubService = gitHubService;
    _httpClient = httpClient;
    _logger = logger;
  }

  public async Task CheckAllRepositoriesAsync(ICommandContext? context, CancellationToken token)
  {
    var config = _configService.LoadConfig();
    if (config == null)
    {
      _logger.LogWarning("No config loaded.");
      return;
    }

    var pluginsRoot = _configService.GetPluginsDirectory();
    if (string.IsNullOrEmpty(pluginsRoot) || !Directory.Exists(pluginsRoot))
    {
      _logger.LogWarning("Plugins root directory not found at {Path}.", pluginsRoot);
      return;
    }

    foreach (var kvp in config.Repositories)
    {
      if (token.IsCancellationRequested)
        break;

      var pluginName = kvp.Key;
      var repoValue = kvp.Value;

      if (string.IsNullOrWhiteSpace(repoValue))
      {
        _logger.LogWarning("Config entry with empty Repo.");
        continue;
      }

      if (!TryParseRepo(repoValue, out var owner, out var repo))
      {
        _logger.LogWarning("Invalid repo entry '{RepoString}'", repoValue);
        continue;
      }

      var pluginDir = Path.Combine(pluginsRoot, pluginName);

      if (!Directory.Exists(pluginDir))
      {
        _logger.LogWarning("Plugin directory not found for repo {Owner}/{Repo} at {Path}", owner, repo, pluginDir);
        continue;
      }

      var dllName = pluginName + ".dll";
      var localPath = Path.Combine(pluginDir, dllName);

      if (!File.Exists(localPath))
      {
        _logger.LogWarning("Local dll not found for repo {Owner}/{Repo} at {Path}", owner, repo, localPath);
        continue;
      }

      try
      {
        await CheckAndUpdatePluginAsync(pluginName, localPath, owner, repo, context, token);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating {Owner}/{Repo}.", owner, repo);
      }
    }
  }

  public async Task DownloadAndReplaceAsync(string localPath, string assetUrl, Version? newVersion, CancellationToken token)
  {
    var pluginsDir = Path.GetDirectoryName(localPath)!;
    var tempFile = Path.Combine(pluginsDir, Path.GetRandomFileName());

    using (var response = await _httpClient.GetAsync(assetUrl, token))
    {
      response.EnsureSuccessStatusCode();
      await using var fs = File.Create(tempFile);
      await response.Content.CopyToAsync(fs, token);
    }

    string finalNewDllPath = tempFile;

    if (Path.GetExtension(assetUrl).Equals(".zip", StringComparison.OrdinalIgnoreCase))
    {
      await HandleZipUpdateAsync(localPath, tempFile, pluginsDir);
      finalNewDllPath = localPath;
    }
    else
    {
      HandleDllUpdate(localPath, tempFile);
    }

    if (newVersion != null)
    {
      _versionService.PersistVersion(localPath, newVersion);
    }
  }

  private async Task CheckAndUpdatePluginAsync(
    string pluginName,
    string localPath,
    string owner,
    string repo,
    ICommandContext? context,
    CancellationToken token)
  {
    var localVersion = _versionService.GetLocalVersion(localPath);
    var liveVersion = _versionService.GetLiveVersion(pluginName);

    var effectiveVersion = localVersion ?? liveVersion;

    var remote = await _gitHubService.GetLatestReleaseAsync(owner, repo, token);

    if (remote == null || remote.Version == null)
    {
      _logger.LogWarning("Could not get latest release for {Owner}/{Repo}", owner, repo);
      return;
    }

    if (effectiveVersion != null && remote.Version <= effectiveVersion)
    {
      var dllName = Path.GetFileName(localPath);
      if (liveVersion != null && liveVersion < effectiveVersion)
      {
        _logger.LogInformation("{Dll} update pending restart (Live: {Live}, File: {File}).", dllName, liveVersion, effectiveVersion);
      }
      else
      {
        _logger.LogInformation("{Dll} is up to date ({Version}).", dllName, effectiveVersion);
      }
      return;
    }

    if (remote.AssetUrl == null)
    {
      _logger.LogWarning("No suitable asset found for {Owner}/{Repo}", owner, repo);
      return;
    }

    var dllFileName = Path.GetFileName(localPath);
    _logger.LogInformation("Updating {Dll} from {OldVersion} to {NewVersion}.", dllFileName, localVersion, remote.Version);

    await DownloadAndReplaceAsync(localPath, remote.AssetUrl, remote.Version, token);

    context?.Reply($"[PluginsAutoUpdate] Updated {dllFileName} to {remote.Version}.");
  }

  private async Task HandleZipUpdateAsync(string localPath, string tempFile, string pluginsDir)
  {
    var extractDir = Path.Combine(pluginsDir, "PluginsAutoUpdate_tmp_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(extractDir);

    System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, extractDir);

    var targetName = Path.GetFileName(localPath);
    var foundDll = Directory.GetFiles(extractDir, targetName, SearchOption.AllDirectories).FirstOrDefault();

    if (foundDll == null)
      throw new InvalidOperationException($"DLL {targetName} not found in downloaded archive.");

    var sourceRoot = Path.GetDirectoryName(foundDll)!;

    CopyDirectory(sourceRoot, pluginsDir, true);

    // Cleanup temporary files
    try { Directory.Delete(extractDir, true); }
    catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temporary extraction directory {Dir}", extractDir); }
    
    try { File.Delete(tempFile); }
    catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temporary file {File}", tempFile); }
  }

  private void HandleDllUpdate(string localPath, string tempFile)
  {
    var backupPath = localPath + ".bak";
    try
    {
      if (File.Exists(backupPath))
        File.Delete(backupPath);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to delete old backup file {BackupPath}", backupPath);
    }

    if (File.Exists(localPath))
      File.Move(localPath, backupPath, true);

    File.Move(tempFile, localPath, true);
  }

  private static void CopyDirectory(string sourceDir, string destDir, bool recursive)
  {
    var dir = new DirectoryInfo(sourceDir);
    Directory.CreateDirectory(destDir);

    foreach (var file in dir.GetFiles())
    {
      var targetPath = Path.Combine(destDir, file.Name);

      if (IsConfigFile(targetPath) && File.Exists(targetPath))
      {
        continue;
      }

      file.CopyTo(targetPath, true);
    }

    if (recursive)
    {
      foreach (var subDir in dir.GetDirectories())
      {
        var newDestDir = Path.Combine(destDir, subDir.Name);
        CopyDirectory(subDir.FullName, newDestDir, true);
      }
    }
  }

  private static bool IsConfigFile(string path)
  {
    var normalized = path.Replace('\\', '/');
    return normalized.Contains("/configs/");
  }

  private static bool TryParseRepo(string input, out string owner, out string repo)
  {
    owner = string.Empty;
    repo = string.Empty;

    if (string.IsNullOrWhiteSpace(input))
      return false;

    input = input.Trim();

    if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
      if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        return false;

      var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
      if (segments.Length < 2)
        return false;

      owner = segments[0];
      repo = segments[1];
      return true;
    }

    var parts = input.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2)
      return false;

    owner = parts[0];
    repo = parts[1];
    return true;
  }
}

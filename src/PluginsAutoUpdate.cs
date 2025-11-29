using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Plugins;
using Tomlyn.Extensions.Configuration;

namespace PluginsAutoUpdate;

[PluginMetadata(Id = "PluginsAutoUpdate", Version = "0.0.1-beta", Name = "PluginsAutoUpdate", Author = "aga", Description = "Checks GitHub releases for plugins and updates them.")]
public partial class PluginsAutoUpdate : BasePlugin
{
  private readonly HttpClient _httpClient = new();
  private CancellationTokenSource? _cts;
  private AutoUpdateConfig? _config;

  public PluginsAutoUpdate(ISwiftlyCore core) : base(core)
  {
    _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PluginsAutoUpdate/1.0");
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void Load(bool hotReload)
  {
    _config = LoadConfig();
    if (_config == null)
    {
      Core.Logger.LogWarning("PluginsAutoUpdate: config not found or invalid, skipping scheduler.");
      return;
    }

    _cts = new CancellationTokenSource();
    var token = _cts.Token;

    Task.Run(async () =>
    {
      while (!token.IsCancellationRequested)
      {
        try
        {
          // reload config each tick so interval and repo list changes are picked up
          var cfg = LoadConfig();
          if (cfg == null)
          {
            Core.Logger.LogWarning("PluginsAutoUpdate: failed to reload config, stopping scheduler.");
            break;
          }

          _config = cfg;

          Core.Logger.LogInformation("PluginsAutoUpdate: running scheduled update check (interval {Minutes} min).", _config.CheckIntervalMinutes);
          await CheckAllRepositoriesAsync(null, token);
        }
        catch (Exception ex)
        {
          Core.Logger.LogError(ex, "PluginsAutoUpdate: error during scheduled check.");
        }

        try
        {
          var delayMinutes = Math.Max(1, _config!.CheckIntervalMinutes);
          await Task.Delay(TimeSpan.FromMinutes(delayMinutes), token);
        }
        catch (TaskCanceledException)
        {
          break;
        }
      }
    }, token);
  }

  public override void Unload()
  {
    _cts?.Cancel();
    _cts = null;
  }

  private AutoUpdateConfig? LoadConfig()
  {
    try
    {
      var managedDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
      var swiftlyRoot = Path.GetFullPath(Path.Combine(managedDir, "..", "..", ".."));
      var configDir = Path.Combine(swiftlyRoot, "configs", "plugins", "PluginsAutoUpdate");

      Directory.CreateDirectory(configDir);

      var configPath = Path.Combine(configDir, "config.toml");
      var exists = File.Exists(configPath);
      Core.Logger.LogInformation("PluginsAutoUpdate: using config path {Path} (exists: {Exists}).", configPath, exists);

      if (!exists)
      {
        var defaultToml = "[PluginsAutoUpdate]\n" +
                          "CheckIntervalMinutes = 30\n" +
                          "\n" +
                          "# Repositories are defined as PluginName = Repo string under this table:\n" +
                          "[PluginsAutoUpdate.Repositories]\n" +
                          "# Example1 = \"owner1/repo1\"\n" +
                          "# Example2 = \"https://github.com/owner2/repo2\"\n";

        File.WriteAllText(configPath, defaultToml);
        Core.Logger.LogInformation("PluginsAutoUpdate: created default TOML config at {Path}.", configPath);
      }

      var builder = new ConfigurationBuilder()
        .SetBasePath(configDir)
        .AddTomlFile("config.toml", optional: false, reloadOnChange: true);

      var root = builder.Build();
      var section = root.GetSection("PluginsAutoUpdate");
      var loaded = new AutoUpdateConfig();
      section.Bind(loaded);
      loaded.Repositories ??= new Dictionary<string, string>();

      return loaded;
    }
    catch (Exception ex)
    {
      Core.Logger.LogError(ex, "PluginsAutoUpdate: failed to load config.");
      return null;
    }
  }

  private async Task CheckAllRepositoriesAsync(ICommandContext? context, CancellationToken token)
  {
    if (_config == null)
    {
      Core.Logger.LogWarning("PluginsAutoUpdate: no config loaded.");
      return;
    }

    var managedDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
    var swiftlyRoot = Path.GetFullPath(Path.Combine(managedDir, "..", "..", ".."));
    var pluginsRoot = Path.Combine(swiftlyRoot, "plugins");
    if (string.IsNullOrEmpty(pluginsRoot) || !Directory.Exists(pluginsRoot))
    {
      Core.Logger.LogWarning("PluginsAutoUpdate: plugins root directory not found at {Path}.", pluginsRoot);
      return;
    }

    foreach (var kvp in _config.Repositories)
    {
      if (token.IsCancellationRequested)
        break;

      var pluginName = kvp.Key;
      var repoValue = kvp.Value;

      if (string.IsNullOrWhiteSpace(repoValue))
      {
        Core.Logger.LogWarning("PluginsAutoUpdate: config entry with empty Repo.");
        continue;
      }

      if (!TryParseRepo(repoValue, out var owner, out var repo))
      {
        Core.Logger.LogWarning("PluginsAutoUpdate: invalid repo entry '{RepoString}'", repoValue);
        continue;
      }

      var pluginDir = Path.Combine(pluginsRoot, pluginName);

      if (!Directory.Exists(pluginDir))
      {
        Core.Logger.LogWarning("PluginsAutoUpdate: plugin directory not found for repo {Owner}/{Repo} at {Path}", owner, repo, pluginDir);
        continue;
      }

      var dllName = pluginName + ".dll";
      var localPath = Path.Combine(pluginDir, dllName);

      if (!File.Exists(localPath))
      {
        Core.Logger.LogWarning("PluginsAutoUpdate: local dll not found for repo {Owner}/{Repo} at {Path}", owner, repo, localPath);
        continue;
      }

      try
      {
        var localVersion = GetLocalVersion(localPath);
        var liveVersion = GetLiveVersion(pluginName);

        // If we have a file version, trust it (it means we have that file on disk).
        // If not, fall back to live version (maybe DLL has no metadata but is loaded).
        var effectiveVersion = localVersion ?? liveVersion;
        
        var remote = await GetLatestReleaseAsync(owner, repo, token);

        if (remote == null || remote.Version == null)
        {
          Core.Logger.LogWarning("PluginsAutoUpdate: could not get latest release for {Owner}/{Repo}", owner, repo);
          continue;
        }

        if (effectiveVersion != null && remote.Version <= effectiveVersion)
        {
          if (liveVersion != null && liveVersion < effectiveVersion)
          {
             Core.Logger.LogInformation("PluginsAutoUpdate: {Dll} update pending restart (Live: {Live}, File: {File}).", dllName, liveVersion, effectiveVersion);
          }
          else
          {
             Core.Logger.LogInformation("PluginsAutoUpdate: {Dll} is up to date ({Version}).", dllName, effectiveVersion);
          }
          continue;
        }

        if (remote.AssetUrl == null)
        {
          Core.Logger.LogWarning("PluginsAutoUpdate: no suitable asset found for {Owner}/{Repo}", owner, repo);
          continue;
        }

        Core.Logger.LogInformation("PluginsAutoUpdate: updating {Dll} from {OldVersion} to {NewVersion}.", dllName, localVersion, remote.Version);

        await DownloadAndReplaceAsync(localPath, remote.AssetUrl, remote.Version, token);

        context?.Reply($"[PluginsAutoUpdate] Updated {dllName} to {remote.Version}.");
      }
      catch (Exception ex)
      {
        Core.Logger.LogError(ex, "PluginsAutoUpdate: error updating {Owner}/{Repo}.", owner, repo);
      }
    }
  }

  private Version? GetLiveVersion(string pluginName)
  {
    try
    {
      var coreType = Core.GetType();
      var spField = coreType.GetField("serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance);
      if (spField == null) return null;

      var sp = spField.GetValue(Core);
      if (sp == null) return null;

      var pluginManagerType = GetAllTypes()
          .FirstOrDefault(t => t != null && t.FullName == "SwiftlyS2.Core.Plugins.PluginManager");

      if (pluginManagerType == null) return null;

      var getServiceMethod = sp.GetType().GetMethod("GetService");
      var pluginManager = getServiceMethod?.Invoke(sp, new object[] { pluginManagerType });

      if (pluginManager == null) return null;

      var getPluginsMethod = pluginManagerType.GetMethod("GetPlugins");
      var pluginsList = getPluginsMethod?.Invoke(pluginManager, null) as System.Collections.IEnumerable;

      if (pluginsList == null) return null;

      foreach (var pluginContext in pluginsList)
      {
        var ctxType = pluginContext.GetType();
        var dirProp = ctxType.GetProperty("PluginDirectory");
        var dir = dirProp?.GetValue(pluginContext) as string;

        if (!string.IsNullOrEmpty(dir) && Path.GetFileName(dir) == pluginName)
        {
          var metaProp = ctxType.GetProperty("Metadata");
          var meta = metaProp?.GetValue(pluginContext) as PluginMetadata;
          if (meta != null && Version.TryParse(meta.Version, out var v))
          {
            return v;
          }
        }
      }
    }
    catch (Exception ex)
    {
      Core.Logger.LogWarning(ex, "PluginsAutoUpdate: Error getting live version for {Name}", pluginName);
    }
    return null;
  }

  private Version? GetLocalVersion(string path)
  {
    // Prefer sidecar version file written after successful updates
    try
    {
      var dir = Path.GetDirectoryName(path);
      var name = Path.GetFileNameWithoutExtension(path);
      if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(name))
      {
        var sidecar = Path.Combine(dir, name + ".version");
        if (File.Exists(sidecar))
        {
          var txt = File.ReadAllText(sidecar).Trim();
          var parts = txt.Split('|');

          // Only trust sidecar if it has a hash and the hash matches the current DLL
          if (parts.Length == 2)
          {
            var sidecarVerStr = parts[0];
            var sidecarHash = parts[1];
            var currentHash = ComputeHash(path);

            if (string.Equals(sidecarHash, currentHash, StringComparison.OrdinalIgnoreCase) &&
                Version.TryParse(sidecarVerStr, out var sidecarVersion))
            {
              return sidecarVersion;
            }
            else
            {
              Core.Logger.LogWarning("PluginsAutoUpdate: Sidecar hash mismatch or invalid for {Dll}. Ignoring sidecar.", Path.GetFileName(path));
            }
          }
          else
          {
            Core.Logger.LogWarning("PluginsAutoUpdate: Legacy sidecar found for {Dll}. Ignoring to ensure integrity.", Path.GetFileName(path));
          }
        }
      }
    }
    catch (Exception ex)
    {
      Core.Logger.LogWarning(ex, "PluginsAutoUpdate: error checking sidecar for {Dll}", Path.GetFileName(path));
    }

    try
    {
      var asmName = AssemblyName.GetAssemblyName(path);
      return asmName.Version;
    }
    catch
    {
      try
      {
        var info = FileVersionInfo.GetVersionInfo(path);
        return Version.TryParse(info.FileVersion, out var v) ? v : null;
      }
      catch
      {
        return null;
      }
    }
  }

  private async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string owner, string repo, CancellationToken token)
  {
    var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
    using var response = await _httpClient.GetAsync(url, token);
    if (!response.IsSuccessStatusCode)
      return null;

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

  private async Task DownloadAndReplaceAsync(string localPath, string assetUrl, Version? newVersion, CancellationToken token)
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
      var extractDir = Path.Combine(pluginsDir, "PluginsAutoUpdate_tmp_" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(extractDir);

      System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, extractDir);

      // Find the main DLL in the extracted files to identify structure
      var targetName = Path.GetFileName(localPath);
      var foundDll = Directory.GetFiles(extractDir, targetName, SearchOption.AllDirectories).FirstOrDefault();
      
      if (foundDll == null)
        throw new InvalidOperationException($"DLL {targetName} not found in downloaded archive.");

      // Determine the root of the plugin content within the zip
      // Usually it's the folder containing the DLL
      var sourceRoot = Path.GetDirectoryName(foundDll)!;

      // Copy everything from sourceRoot to pluginsDir
      // EXCLUDING configs to preserve settings
      CopyDirectory(sourceRoot, pluginsDir, true);

      // The "new" DLL is now in place (overwritten by CopyDirectory)
      finalNewDllPath = localPath; 

      try { Directory.Delete(extractDir, true); } catch { }
      try { File.Delete(tempFile); } catch { }
    }
    else 
    {
        // Single DLL download logic
        var backupPath = localPath + ".bak";
        try
        {
          if (File.Exists(backupPath))
            File.Delete(backupPath);
        }
        catch { }

        if (File.Exists(localPath))
          File.Move(localPath, backupPath, true);

        File.Move(finalNewDllPath, localPath, true);
    }

    // Persist the applied version so we don't keep updating when assembly version stays constant
    try
    {
      if (newVersion != null)
      {
        var name = Path.GetFileNameWithoutExtension(localPath);
        var sidecar = Path.Combine(pluginsDir, name + ".version");
        var hash = ComputeHash(localPath);
        File.WriteAllText(sidecar, $"{newVersion}|{hash}");
      }
    }
    catch
    {
      // non-fatal if persisting sidecar fails
    }
  }

  private static void CopyDirectory(string sourceDir, string destDir, bool recursive)
  {
      var dir = new DirectoryInfo(sourceDir);
      Directory.CreateDirectory(destDir);

      foreach (var file in dir.GetFiles())
      {
          var targetPath = Path.Combine(destDir, file.Name);
          
          // Protect configs
          // Check if we are inside a "configs" directory relative to the plugin root?
          // The simple heuristic: If the target path contains "configs", skip?
          // Better: If the file exists in "configs" folder, don't overwrite.
          
          // Check if this file is a config file that already exists
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
      // Check if path contains "configs" segment
      // Normalized check
      var normalized = path.Replace('\\', '/');
      if (normalized.Contains("/configs/")) return true;
      
      // Also check root configs folder if applicable, but here we are copying TO plugin folder.
      // Usually configs are in <PluginDir>/configs/
      return false;
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

  private static string ComputeHash(string filePath)
  {
    using var sha = SHA256.Create();
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    var bytes = sha.ComputeHash(stream);
    return Convert.ToHexString(bytes);
  }

  private static IEnumerable<Type?> GetAllTypes()
  {
      foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
      {
          Type[]? types = null;
          try
          {
              types = asm.GetTypes();
          }
          catch (ReflectionTypeLoadException ex)
          {
              types = ex.Types;
          }
          catch
          {
              // ignore
          }

          if (types != null)
          {
              foreach (var t in types)
              {
                  yield return t;
              }
          }
      }
  }
}

public sealed record GitHubReleaseInfo(Version? Version, string? AssetUrl);
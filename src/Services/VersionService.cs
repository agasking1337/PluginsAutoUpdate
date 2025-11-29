using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace PluginsAutoUpdate;

/// <summary>
/// Service implementation for version management.
/// </summary>
public sealed class VersionService : IVersionService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;

  public VersionService(ISwiftlyCore core, ILogger logger)
  {
    _core = core;
    _logger = logger;
  }

  public Version? GetLocalVersion(string dllPath)
  {
    // Prefer sidecar version file written after successful updates
    try
    {
      var dir = Path.GetDirectoryName(dllPath);
      var name = Path.GetFileNameWithoutExtension(dllPath);
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
            var currentHash = ComputeHash(dllPath);

            if (string.Equals(sidecarHash, currentHash, StringComparison.OrdinalIgnoreCase) &&
                Version.TryParse(sidecarVerStr, out var sidecarVersion))
            {
              return sidecarVersion;
            }
            else
            {
              _logger.LogWarning("Sidecar hash mismatch or invalid for {Dll}. Ignoring sidecar.", Path.GetFileName(dllPath));
            }
          }
          else
          {
            _logger.LogWarning("Legacy sidecar found for {Dll}. Ignoring to ensure integrity.", Path.GetFileName(dllPath));
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error checking sidecar for {Dll}", Path.GetFileName(dllPath));
    }

    // Try assembly version first
    try
    {
      var asmName = AssemblyName.GetAssemblyName(dllPath);
      return asmName.Version;
    }
    catch { /* Assembly metadata not available */ }

    // Fallback to file version
    try
    {
      var info = FileVersionInfo.GetVersionInfo(dllPath);
      return Version.TryParse(info.FileVersion, out var v) ? v : null;
    }
    catch { /* File version not available */ }

    return null;
  }

  public Version? GetLiveVersion(string pluginName)
  {
    try
    {
      var coreType = _core.GetType();
      var spField = coreType.GetField("serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance);
      if (spField == null) return null;

      var sp = spField.GetValue(_core);
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
      _logger.LogWarning(ex, "Error getting live version for {Name}", pluginName);
    }
    return null;
  }

  public void PersistVersion(string dllPath, Version version)
  {
    try
    {
      var dir = Path.GetDirectoryName(dllPath);
      var name = Path.GetFileNameWithoutExtension(dllPath);
      if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(name))
      {
        var sidecar = Path.Combine(dir, name + ".version");
        var hash = ComputeHash(dllPath);
        File.WriteAllText(sidecar, $"{version}|{hash}");
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to persist version for {Dll}", Path.GetFileName(dllPath));
    }
  }

  private static string ComputeHash(string filePath)
  {
    using var sha = SHA256.Create();
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    var bytes = sha.ComputeHash(stream);
    return Convert.ToHexString(bytes);
  }

  private static System.Collections.Generic.IEnumerable<Type?> GetAllTypes()
  {
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
      Type?[]? types = null;
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace PluginsAutoUpdate;

[PluginMetadata(Id = "PluginsAutoUpdate", Version = "0.0.3-beta", Name = "PluginsAutoUpdate", Author = "aga", Description = "Checks GitHub releases for plugins and updates them.")]
public partial class PluginsAutoUpdate : BasePlugin
{
  private CancellationTokenSource? _cts;
  private IServiceProvider? _serviceProvider;

  // Services resolved from DI container
  private IConfigurationService? _configService;
  private IUpdateService? _updateService;

  public PluginsAutoUpdate(ISwiftlyCore core) : base(core)
  {
    InitializeServices();
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void Load(bool hotReload)
  {
    if (_configService == null || _updateService == null)
    {
      Core.Logger.LogError("PluginsAutoUpdate: services not initialized.");
      return;
    }

    var initialConfig = _configService.LoadConfig();
    if (initialConfig == null)
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
        AutoUpdateConfig? config = null;
        try
        {
          config = _configService.LoadConfig();
          if (config == null)
          {
            Core.Logger.LogWarning("PluginsAutoUpdate: failed to reload config, stopping scheduler.");
            break;
          }

          Core.Logger.LogInformation("PluginsAutoUpdate: running scheduled update check (interval {Minutes} min).", config.CheckIntervalMinutes);
          await _updateService.CheckAllRepositoriesAsync(null, token);
        }
        catch (Exception ex)
        {
          Core.Logger.LogError(ex, "PluginsAutoUpdate: error during scheduled check.");
        }

        try
        {
          var delayMinutes = Math.Max(1, config?.CheckIntervalMinutes ?? 30);
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
  
    // Dispose service provider and all registered services
    ServiceProviderFactory.DisposeServiceProvider(_serviceProvider);
    _serviceProvider = null;
  }

  /// <summary>
  /// Initializes the dependency injection container and resolves services.
  /// </summary>
  private void InitializeServices()
  {
    try
    {
      _serviceProvider = ServiceProviderFactory.CreateServiceProvider(Core, Core.Logger);
    
      // Resolve services from DI container
      _configService = _serviceProvider.GetRequiredService<IConfigurationService>();
      _updateService = _serviceProvider.GetRequiredService<IUpdateService>();
    
      Core.Logger.LogInformation("PluginsAutoUpdate: services initialized successfully via DI.");
    }
    catch (Exception ex)
    {
      Core.Logger.LogError(ex, "PluginsAutoUpdate: failed to initialize services.");
      throw;
    }
  }
}

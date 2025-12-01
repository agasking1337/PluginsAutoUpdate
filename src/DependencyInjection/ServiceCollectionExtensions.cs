using Microsoft.Extensions.DependencyInjection;

namespace PluginsAutoUpdate;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Registers all PluginsAutoUpdate services into the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for chaining</returns>
  public static IServiceCollection AddPluginsAutoUpdateServices(this IServiceCollection services)
  {
    // Register HttpClient as singleton (reused across all requests)
    services.AddSingleton<HttpClient>(sp =>
    {
      var client = new HttpClient();
      client.DefaultRequestHeaders.UserAgent.ParseAdd("PluginsAutoUpdate/1.0");
      client.Timeout = System.TimeSpan.FromSeconds(30);
      return client;
    });

    // Register services
    services.AddSingleton<IConfigurationService, ConfigurationService>();
    services.AddSingleton<IVersionService, VersionService>();
    services.AddSingleton<IGitHubService, GitHubService>();
    services.AddSingleton<IUpdateService, UpdateService>();
    services.AddSingleton<ICronService, CronService>();
    services.AddSingleton<IDiscordService, DiscordService>();

    return services;
  }
}

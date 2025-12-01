using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PluginsAutoUpdate;

public sealed class CronService : ICronService
{
  private readonly IConfigurationService _configService;
  private readonly IUpdateService _updateService;
  private readonly ILogger _logger;

  public CronService(IConfigurationService configService, IUpdateService updateService, ILogger logger)
  {
    _configService = configService;
    _updateService = updateService;
    _logger = logger;
  }

  public async Task StartAsync(CancellationToken token)
  {
    DateTime? lastRunDate = null;

    while (!token.IsCancellationRequested)
    {
      try
      {
        var config = _configService.LoadConfig();
        if (config != null && !string.IsNullOrWhiteSpace(config.DailyUpdateTimeUtc))
        {
          if (TimeSpan.TryParse(config.DailyUpdateTimeUtc, out var targetTimeUtc))
          {
            var nowUtc = DateTime.UtcNow;

            if (nowUtc.TimeOfDay >= targetTimeUtc && (lastRunDate == null || lastRunDate.Value.Date != nowUtc.Date))
            {
              _logger.LogInformation("PluginsAutoUpdate: running scheduled plugin update at {Now} (UTC).", nowUtc);
              await _updateService.CheckAllRepositoriesAsync(null, token);
              lastRunDate = nowUtc.Date;
            }
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "PluginsAutoUpdate: error during scheduled update run.");
      }

      try
      {
        await Task.Delay(TimeSpan.FromMinutes(1), token);
      }
      catch (TaskCanceledException)
      {
        break;
      }
    }
  }
}

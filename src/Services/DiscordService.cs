using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PluginsAutoUpdate;

public sealed class DiscordService : IDiscordService
{
  private readonly IConfigurationService _configService;
  private readonly HttpClient _httpClient;
  private readonly ILogger _logger;

  public DiscordService(IConfigurationService configService, HttpClient httpClient, ILogger logger)
  {
    _configService = configService;
    _httpClient = httpClient;
    _logger = logger;
  }

  public Task SendPluginUpdateSuccessAsync(
    string pluginName,
    string owner,
    string repo,
    string? fromVersion,
    string? toVersion,
    CancellationToken token)
  {
    var nowUtc = DateTime.UtcNow;
    var embed = new
    {
      title = "✅ Plugin Update Successful",
      color = 0x57F287, // green
      fields = new[]
      {
        new { name = "Plugin",      value = pluginName,                                 inline = true  },
        new { name = "Repository",  value = $"`{owner}/{repo}`",                       inline = true  },
        new { name = "From → To",   value = $"`{fromVersion ?? "unknown"}` → `{toVersion ?? "unknown"}`", inline = false },
        new { name = "Time (UTC)",  value = nowUtc.ToString("yyyy-MM-dd HH:mm:ss"),    inline = false }
      },
      timestamp = nowUtc.ToString("o")
    };

    return SendAsync(new { embeds = new[] { embed } }, token);
  }

  public Task SendPluginUpdateFailureAsync(
    string pluginName,
    string owner,
    string repo,
    string errorMessage,
    CancellationToken token)
  {
    var nowUtc = DateTime.UtcNow;
    var embed = new
    {
      title = "❌ Plugin Update Failed",
      color = 0xED4245, // red
      fields = new[]
      {
        new { name = "Plugin",     value = pluginName,                              inline = true  },
        new { name = "Repository", value = $"`{owner}/{repo}`",                    inline = true  },
        new { name = "Error",      value = errorMessage,                            inline = false },
        new { name = "Time (UTC)", value = nowUtc.ToString("yyyy-MM-dd HH:mm:ss"), inline = false }
      },
      timestamp = nowUtc.ToString("o")
    };

    return SendAsync(new { embeds = new[] { embed } }, token);
  }

  private async Task SendAsync(object payloadObject, CancellationToken token)
  {
    var config = _configService.LoadConfig();
    if (config == null || string.IsNullOrWhiteSpace(config.DiscordWebhookUrl))
      return;

    try
    {
      var payload = JsonSerializer.Serialize(payloadObject);
      using var content = new StringContent(payload, Encoding.UTF8, "application/json");
      using var response = await _httpClient.PostAsync(config.DiscordWebhookUrl, content, token);
      response.EnsureSuccessStatusCode();
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to send Discord webhook notification.");
    }
  }
}

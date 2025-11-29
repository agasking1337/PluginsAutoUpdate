using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;

namespace PluginsAutoUpdate;

public partial class PluginsAutoUpdate
{
  [Command("checkupdate")]
  public void CheckUpdateCommand(ICommandContext context)
  {
    if (context.IsSentByPlayer && context.Sender is not null)
    {
      context.Reply("[PluginsAutoUpdate] This command can only be used from the server console.");
      return;
    }

    context.Reply("[PluginsAutoUpdate] Checking for plugin updates...");

    if (_updateService == null)
    {
      Core.Logger.LogError("PluginsAutoUpdate: update service not initialized.");
      return;
    }

    Task.Run(async () =>
    {
      try
      {
        await _updateService.CheckAllRepositoriesAsync(context, CancellationToken.None);
        context.Reply("[PluginsAutoUpdate] Manual check finished.");
      }
      catch (Exception ex)
      {
        Core.Logger.LogError(ex, "PluginsAutoUpdate: error during manual check.");
        context.Reply("[PluginsAutoUpdate] Error during manual check, see console.");
      }
    });
  }
}

using System.Threading;
using System.Threading.Tasks;

namespace PluginsAutoUpdate;

public interface IDiscordService
{
  Task SendPluginUpdateSuccessAsync(
    string pluginName,
    string owner,
    string repo,
    string? fromVersion,
    string? toVersion,
    CancellationToken token);

  Task SendPluginUpdateFailureAsync(
    string pluginName,
    string owner,
    string repo,
    string errorMessage,
    CancellationToken token);
}

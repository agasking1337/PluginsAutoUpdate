using System.Threading;
using System.Threading.Tasks;

namespace PluginsAutoUpdate;

public interface ICronService
{
  Task StartAsync(CancellationToken token);
}

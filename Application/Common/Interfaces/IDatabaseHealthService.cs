using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces;


public interface IDatabaseHealthService
{


    Task<bool> CanConnectAsync(CancellationToken ct = default);
}

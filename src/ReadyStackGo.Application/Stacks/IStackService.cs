using ReadyStackGo.Application.Stacks.DTOs;

namespace ReadyStackGo.Application.Stacks;

public interface IStackService
{
    Task<StackDto?> DeployStackAsync(string stackId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StackDto>> ListStacksAsync(CancellationToken cancellationToken = default);
    Task<StackDto?> GetStackAsync(string stackId, CancellationToken cancellationToken = default);
    Task RemoveStackAsync(string stackId, CancellationToken cancellationToken = default);
}

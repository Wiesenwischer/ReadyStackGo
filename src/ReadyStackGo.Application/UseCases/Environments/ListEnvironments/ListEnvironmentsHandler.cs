using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.ListEnvironments;

public class ListEnvironmentsHandler : IRequestHandler<ListEnvironmentsQuery, ListEnvironmentsResponse>
{
    private readonly IEnvironmentService _environmentService;

    public ListEnvironmentsHandler(IEnvironmentService environmentService)
    {
        _environmentService = environmentService;
    }

    public async Task<ListEnvironmentsResponse> Handle(ListEnvironmentsQuery request, CancellationToken cancellationToken)
    {
        return await _environmentService.GetEnvironmentsAsync();
    }
}

using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.GetEnvironment;

public class GetEnvironmentHandler : IRequestHandler<GetEnvironmentQuery, EnvironmentResponse?>
{
    private readonly IEnvironmentService _environmentService;

    public GetEnvironmentHandler(IEnvironmentService environmentService)
    {
        _environmentService = environmentService;
    }

    public async Task<EnvironmentResponse?> Handle(GetEnvironmentQuery request, CancellationToken cancellationToken)
    {
        return await _environmentService.GetEnvironmentAsync(request.EnvironmentId);
    }
}

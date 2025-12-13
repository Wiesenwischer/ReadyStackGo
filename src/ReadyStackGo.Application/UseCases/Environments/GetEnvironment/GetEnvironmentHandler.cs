using MediatR;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Environments.GetEnvironment;

public class GetEnvironmentHandler : IRequestHandler<GetEnvironmentQuery, EnvironmentResponse?>
{
    private readonly IEnvironmentRepository _environmentRepository;

    public GetEnvironmentHandler(IEnvironmentRepository environmentRepository)
    {
        _environmentRepository = environmentRepository;
    }

    public Task<EnvironmentResponse?> Handle(GetEnvironmentQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.EnvironmentId, out var guid))
        {
            return Task.FromResult<EnvironmentResponse?>(null);
        }

        var environment = _environmentRepository.Get(new EnvironmentId(guid));

        return Task.FromResult(environment != null ? EnvironmentMapper.ToResponse(environment) : null);
    }
}

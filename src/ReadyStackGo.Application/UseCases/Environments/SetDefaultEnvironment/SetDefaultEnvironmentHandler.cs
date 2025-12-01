using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.SetDefaultEnvironment;

public class SetDefaultEnvironmentHandler : IRequestHandler<SetDefaultEnvironmentCommand, SetDefaultEnvironmentResponse>
{
    private readonly IEnvironmentService _environmentService;

    public SetDefaultEnvironmentHandler(IEnvironmentService environmentService)
    {
        _environmentService = environmentService;
    }

    public async Task<SetDefaultEnvironmentResponse> Handle(SetDefaultEnvironmentCommand request, CancellationToken cancellationToken)
    {
        return await _environmentService.SetDefaultEnvironmentAsync(request.EnvironmentId);
    }
}

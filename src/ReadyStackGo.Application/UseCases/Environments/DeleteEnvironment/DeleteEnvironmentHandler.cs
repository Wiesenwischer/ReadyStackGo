using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.DeleteEnvironment;

public class DeleteEnvironmentHandler : IRequestHandler<DeleteEnvironmentCommand, DeleteEnvironmentResponse>
{
    private readonly IEnvironmentService _environmentService;

    public DeleteEnvironmentHandler(IEnvironmentService environmentService)
    {
        _environmentService = environmentService;
    }

    public async Task<DeleteEnvironmentResponse> Handle(DeleteEnvironmentCommand request, CancellationToken cancellationToken)
    {
        return await _environmentService.DeleteEnvironmentAsync(request.EnvironmentId);
    }
}

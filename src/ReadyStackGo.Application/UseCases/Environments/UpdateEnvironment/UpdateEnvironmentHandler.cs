using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.UpdateEnvironment;

public class UpdateEnvironmentHandler : IRequestHandler<UpdateEnvironmentCommand, UpdateEnvironmentResponse>
{
    private readonly IEnvironmentService _environmentService;

    public UpdateEnvironmentHandler(IEnvironmentService environmentService)
    {
        _environmentService = environmentService;
    }

    public async Task<UpdateEnvironmentResponse> Handle(UpdateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var updateRequest = new UpdateEnvironmentRequest
        {
            Name = request.Name,
            SocketPath = request.SocketPath
        };

        return await _environmentService.UpdateEnvironmentAsync(request.EnvironmentId, updateRequest);
    }
}

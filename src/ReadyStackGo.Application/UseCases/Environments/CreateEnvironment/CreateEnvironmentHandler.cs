using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.CreateEnvironment;

public class CreateEnvironmentHandler : IRequestHandler<CreateEnvironmentCommand, CreateEnvironmentResponse>
{
    private readonly IEnvironmentService _environmentService;

    public CreateEnvironmentHandler(IEnvironmentService environmentService)
    {
        _environmentService = environmentService;
    }

    public async Task<CreateEnvironmentResponse> Handle(CreateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var createRequest = new CreateEnvironmentRequest
        {
            Id = request.Id,
            Name = request.Name,
            SocketPath = request.SocketPath
        };

        return await _environmentService.CreateEnvironmentAsync(createRequest);
    }
}

using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Wizard.SetConnections;

public class SetConnectionsHandler : IRequestHandler<SetConnectionsCommand, SetConnectionsResult>
{
    private readonly IWizardService _wizardService;

    public SetConnectionsHandler(IWizardService wizardService)
    {
        _wizardService = wizardService;
    }

    public async Task<SetConnectionsResult> Handle(SetConnectionsCommand request, CancellationToken cancellationToken)
    {
        var result = await _wizardService.SetConnectionsAsync(new SetConnectionsRequest
        {
            Transport = request.Transport,
            Persistence = request.Persistence,
            EventStore = request.EventStore
        });

        return new SetConnectionsResult(result.Success, result.Message);
    }
}

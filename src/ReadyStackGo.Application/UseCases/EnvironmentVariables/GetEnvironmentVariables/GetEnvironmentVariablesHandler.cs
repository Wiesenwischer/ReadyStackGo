namespace ReadyStackGo.Application.UseCases.EnvironmentVariables.GetEnvironmentVariables;

using MediatR;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Handler for getting environment variables.
/// </summary>
public class GetEnvironmentVariablesHandler : IRequestHandler<GetEnvironmentVariablesQuery, GetEnvironmentVariablesResponse>
{
    private readonly IEnvironmentVariableRepository _repository;

    public GetEnvironmentVariablesHandler(IEnvironmentVariableRepository repository)
    {
        _repository = repository;
    }

    public Task<GetEnvironmentVariablesResponse> Handle(GetEnvironmentVariablesQuery request, CancellationToken cancellationToken)
    {
        var environmentId = EnvironmentId.FromGuid(Guid.Parse(request.EnvironmentId));
        var variables = _repository.GetByEnvironment(environmentId);

        var variablesDict = variables.ToDictionary(v => v.Key, v => v.Value);

        return Task.FromResult(new GetEnvironmentVariablesResponse(variablesDict));
    }
}

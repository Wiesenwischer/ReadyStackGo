namespace ReadyStackGo.Application.UseCases.EnvironmentVariables.SaveEnvironmentVariables;

using MediatR;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Handler for saving environment variables.
/// </summary>
public class SaveEnvironmentVariablesHandler : IRequestHandler<SaveEnvironmentVariablesCommand, SaveEnvironmentVariablesResponse>
{
    private readonly IEnvironmentVariableRepository _repository;

    public SaveEnvironmentVariablesHandler(IEnvironmentVariableRepository repository)
    {
        _repository = repository;
    }

    public Task<SaveEnvironmentVariablesResponse> Handle(SaveEnvironmentVariablesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var environmentId = EnvironmentId.FromGuid(Guid.Parse(request.EnvironmentId));

            // Get existing variables
            var existingVariables = _repository.GetByEnvironment(environmentId).ToList();

            // Update or create variables
            foreach (var kvp in request.Variables)
            {
                var existing = existingVariables.FirstOrDefault(v => v.Key == kvp.Key);

                if (existing != null)
                {
                    // Update existing
                    var isEncrypted = kvp.Key.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                                     kvp.Key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                                     kvp.Key.Contains("Token", StringComparison.OrdinalIgnoreCase);

                    existing.UpdateValue(kvp.Value, isEncrypted);
                    _repository.Update(existing);
                }
                else
                {
                    // Create new
                    var isEncrypted = kvp.Key.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                                     kvp.Key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                                     kvp.Key.Contains("Token", StringComparison.OrdinalIgnoreCase);

                    var newVariable = EnvironmentVariable.Create(
                        _repository.NextIdentity(),
                        environmentId,
                        kvp.Key,
                        kvp.Value,
                        isEncrypted);

                    _repository.Add(newVariable);
                }
            }

            _repository.SaveChanges();

            return Task.FromResult(new SaveEnvironmentVariablesResponse(true, "Variables saved successfully"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new SaveEnvironmentVariablesResponse(false, $"Failed to save variables: {ex.Message}"));
        }
    }
}

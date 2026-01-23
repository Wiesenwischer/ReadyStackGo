namespace ReadyStackGo.Application.UseCases.EnvironmentVariables.SaveEnvironmentVariables;

using MediatR;

/// <summary>
/// Command to save environment variables.
/// </summary>
public record SaveEnvironmentVariablesCommand(
    string EnvironmentId,
    Dictionary<string, string> Variables) : IRequest<SaveEnvironmentVariablesResponse>;

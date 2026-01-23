namespace ReadyStackGo.Application.UseCases.EnvironmentVariables.GetEnvironmentVariables;

using MediatR;

/// <summary>
/// Query to get all environment variables for an environment.
/// </summary>
public record GetEnvironmentVariablesQuery(string EnvironmentId) : IRequest<GetEnvironmentVariablesResponse>;

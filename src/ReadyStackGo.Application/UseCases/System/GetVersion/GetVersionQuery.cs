using MediatR;

namespace ReadyStackGo.Application.UseCases.System.GetVersion;

/// <summary>
/// Query to get system version information and check for updates.
/// </summary>
public record GetVersionQuery() : IRequest<GetVersionResponse>;

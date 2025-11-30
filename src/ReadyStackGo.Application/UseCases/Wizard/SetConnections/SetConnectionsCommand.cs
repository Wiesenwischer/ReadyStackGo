using MediatR;

namespace ReadyStackGo.Application.UseCases.Wizard.SetConnections;

public record SetConnectionsCommand(
    string Transport,
    string Persistence,
    string? EventStore
) : IRequest<SetConnectionsResult>;

public record SetConnectionsResult(
    bool Success,
    string? Message
);

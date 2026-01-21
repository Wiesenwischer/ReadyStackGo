using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.UpdateStackSource;

public record UpdateStackSourceCommand(string Id, UpdateStackSourceRequest Request) : IRequest<UpdateStackSourceResult>;

public record UpdateStackSourceRequest
{
    public string? Name { get; init; }
    public bool? Enabled { get; init; }
}

public record UpdateStackSourceResult(
    bool Success,
    string? Message = null
);

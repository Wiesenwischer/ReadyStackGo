using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.DeleteStackSource;

public record DeleteStackSourceCommand(string Id) : IRequest<DeleteStackSourceResult>;

public record DeleteStackSourceResult(
    bool Success,
    string? Message = null
);

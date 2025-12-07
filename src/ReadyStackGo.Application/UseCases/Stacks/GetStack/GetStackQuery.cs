using MediatR;
using ReadyStackGo.Application.UseCases.Stacks.ListStacks;

namespace ReadyStackGo.Application.UseCases.Stacks.GetStack;

public record GetStackQuery(string StackId) : IRequest<GetStackResult?>;

public record GetStackResult(
    string Id,
    string SourceId,
    string SourceName,
    string Name,
    string? Description,
    string YamlContent,
    List<string> Services,
    List<StackVariableItem> Variables,
    string? FilePath,
    List<string> AdditionalFiles,
    DateTime LastSyncedAt,
    string? Version
);

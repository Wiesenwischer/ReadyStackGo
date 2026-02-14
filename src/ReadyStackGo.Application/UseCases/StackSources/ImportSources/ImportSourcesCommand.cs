using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.ImportSources;

public record ImportSourcesCommand(ImportData Data) : IRequest<ImportSourcesResult>;

public record ImportData(
    string Version,
    IReadOnlyList<ImportedSource> Sources);

public record ImportedSource(
    string Name,
    string Type,
    bool Enabled,
    string? Path,
    string? FilePattern,
    string? GitUrl,
    string? GitBranch,
    bool? GitSslVerify);

public record ImportSourcesResult(
    bool Success,
    string? Message = null,
    int SourcesCreated = 0,
    int SourcesSkipped = 0);

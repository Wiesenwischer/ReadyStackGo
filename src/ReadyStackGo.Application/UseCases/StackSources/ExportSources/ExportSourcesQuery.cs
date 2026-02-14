using MediatR;

namespace ReadyStackGo.Application.UseCases.StackSources.ExportSources;

public record ExportSourcesQuery : IRequest<ExportSourcesResult>;

public record ExportSourcesResult(ExportData Data);

public record ExportData(
    string Version,
    DateTime ExportedAt,
    IReadOnlyList<ExportedSource> Sources);

public record ExportedSource(
    string Name,
    string Type,
    bool Enabled,
    string? Path,
    string? FilePattern,
    string? GitUrl,
    string? GitBranch,
    bool? GitSslVerify);

using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.ExportSources;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// GET /api/stack-sources/export - Export all stack source configurations as JSON.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class ExportSourcesEndpoint : EndpointWithoutRequest<ExportSourcesApiResponse>
{
    private readonly IMediator _mediator;

    public ExportSourcesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/stack-sources/export");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportSourcesQuery(), ct);

        Response = new ExportSourcesApiResponse
        {
            Version = result.Data.Version,
            ExportedAt = result.Data.ExportedAt,
            Sources = result.Data.Sources.Select(s => new ExportedSourceDto
            {
                Name = s.Name,
                Type = s.Type,
                Enabled = s.Enabled,
                Path = s.Path,
                FilePattern = s.FilePattern,
                GitUrl = s.GitUrl,
                GitBranch = s.GitBranch,
                GitSslVerify = s.GitSslVerify
            }).ToList()
        };
    }
}

public class ExportSourcesApiResponse
{
    public required string Version { get; init; }
    public DateTime ExportedAt { get; init; }
    public List<ExportedSourceDto> Sources { get; init; } = [];
}

public class ExportedSourceDto
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Enabled { get; init; }
    public string? Path { get; init; }
    public string? FilePattern { get; init; }
    public string? GitUrl { get; init; }
    public string? GitBranch { get; init; }
    public bool? GitSslVerify { get; init; }
}

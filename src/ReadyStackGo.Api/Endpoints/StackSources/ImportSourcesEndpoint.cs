using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.StackSources.ImportSources;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// POST /api/stack-sources/import - Import stack source configurations from JSON.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class ImportSourcesEndpoint : Endpoint<ImportSourcesApiRequest, ImportSourcesApiResponse>
{
    private readonly IMediator _mediator;

    public ImportSourcesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/stack-sources/import");
        PreProcessor<RbacPreProcessor<ImportSourcesApiRequest>>();
    }

    public override async Task HandleAsync(ImportSourcesApiRequest req, CancellationToken ct)
    {
        var importedSources = req.Sources.Select(s => new ImportedSource(
            Name: s.Name,
            Type: s.Type,
            Enabled: s.Enabled,
            Path: s.Path,
            FilePattern: s.FilePattern,
            GitUrl: s.GitUrl,
            GitBranch: s.GitBranch,
            GitSslVerify: s.GitSslVerify
        )).ToList();

        var result = await _mediator.Send(
            new ImportSourcesCommand(new ImportData(req.Version, importedSources)),
            ct);

        Response = new ImportSourcesApiResponse
        {
            Success = result.Success,
            Message = result.Message,
            SourcesCreated = result.SourcesCreated,
            SourcesSkipped = result.SourcesSkipped
        };
    }
}

public class ImportSourcesApiRequest
{
    public string Version { get; set; } = "1.0";
    public List<ImportedSourceDto> Sources { get; set; } = [];
}

public class ImportedSourceDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string? Path { get; set; }
    public string? FilePattern { get; set; }
    public string? GitUrl { get; set; }
    public string? GitBranch { get; set; }
    public bool? GitSslVerify { get; set; }
}

public class ImportSourcesApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int SourcesCreated { get; init; }
    public int SourcesSkipped { get; init; }
}

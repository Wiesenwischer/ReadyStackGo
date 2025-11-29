using FastEndpoints;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Domain.Stacks;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// List all configured stack sources
/// </summary>
public class ListSourcesEndpoint : EndpointWithoutRequest<IEnumerable<StackSourceDto>>
{
    public IStackSourceService StackSourceService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/stack-sources");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sources = await StackSourceService.GetSourcesAsync(ct);
        Response = sources.Select(s => new StackSourceDto
        {
            Id = s.Id,
            Name = s.Name,
            Type = s switch
            {
                LocalDirectoryStackSource => "local-directory",
                GitRepositoryStackSource => "git-repository",
                CompositeStackSource => "composite",
                _ => "unknown"
            },
            Enabled = s.Enabled,
            LastSyncedAt = s.LastSyncedAt,
            Details = GetSourceDetails(s)
        });
    }

    private static Dictionary<string, string> GetSourceDetails(StackSource source)
    {
        return source switch
        {
            LocalDirectoryStackSource local => new Dictionary<string, string>
            {
                ["path"] = local.Path,
                ["filePattern"] = local.FilePattern
            },
            GitRepositoryStackSource git => new Dictionary<string, string>
            {
                ["repositoryUrl"] = git.Url,
                ["branch"] = git.Branch,
                ["path"] = git.Path
            },
            CompositeStackSource composite => new Dictionary<string, string>
            {
                ["sourceCount"] = composite.Sources.Count.ToString()
            },
            _ => new Dictionary<string, string>()
        };
    }
}

public class StackSourceDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Enabled { get; init; }
    public DateTime? LastSyncedAt { get; init; }
    public Dictionary<string, string> Details { get; init; } = new();
}

using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Stacks.ListProducts;

namespace ReadyStackGo.API.Endpoints.Stacks;

/// <summary>
/// GET /api/products - List all products (grouped stacks) from all sources.
/// A product is a grouping of one or more related stacks.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Stacks", "Read")]
public class ListProductsEndpoint : Endpoint<EmptyRequest, IEnumerable<ProductDto>>
{
    private readonly IMediator _mediator;

    public ListProductsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/products");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListProductsQuery(), ct);

        Response = result.Products.Select(p => new ProductDto
        {
            Id = p.Id,
            SourceId = p.SourceId,
            SourceName = p.SourceName,
            Name = p.Name,
            Description = p.Description,
            Version = p.Version,
            IsMultiStack = p.IsMultiStack,
            TotalServices = p.TotalServices,
            TotalVariables = p.TotalVariables,
            Stacks = p.Stacks.Select(s => new ProductStackDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Services = s.Services,
                Variables = s.Variables.Select(v => new StackVariableDto
                {
                    Name = v.Name,
                    DefaultValue = v.DefaultValue,
                    IsRequired = v.IsRequired,
                    Type = v.Type.ToString(),
                    Label = v.Label,
                    Description = v.Description,
                    Placeholder = v.Placeholder,
                    Group = v.Group,
                    Order = v.Order,
                    Pattern = v.Pattern,
                    PatternError = v.PatternError,
                    Min = v.Min,
                    Max = v.Max,
                    Options = v.Options?.Select(o => new SelectOptionDto
                    {
                        Value = o.Value,
                        Label = o.Label,
                        Description = o.Description
                    }).ToList()
                }).ToList()
            }).ToList(),
            LastSyncedAt = p.LastSyncedAt
        });
    }
}

/// <summary>
/// Product DTO for API responses
/// </summary>
public class ProductDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public bool IsMultiStack { get; init; }
    public int TotalServices { get; init; }
    public int TotalVariables { get; init; }
    public List<ProductStackDto> Stacks { get; init; } = new();
    public DateTime LastSyncedAt { get; init; }
}

/// <summary>
/// Stack within a product
/// </summary>
public class ProductStackDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<string> Services { get; init; } = new();
    public List<StackVariableDto> Variables { get; init; } = new();
}

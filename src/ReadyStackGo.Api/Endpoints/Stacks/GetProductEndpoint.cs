using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Stacks.GetProduct;

namespace ReadyStackGo.API.Endpoints.Stacks;

/// <summary>
/// GET /api/products/{id} - Get a specific product by ID.
/// Returns product details including all stacks with their services and variables.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Stacks", "Read")]
public class GetProductEndpoint : Endpoint<GetProductRequest, ProductDetailDto>
{
    private readonly IMediator _mediator;

    public GetProductEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/products/{Id}");
        PreProcessor<RbacPreProcessor<GetProductRequest>>();
    }

    public override async Task HandleAsync(GetProductRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductQuery(req.Id), ct);

        if (result == null)
        {
            ThrowError("Product not found", StatusCodes.Status404NotFound);
            return;
        }

        var product = result.Product;
        Response = new ProductDetailDto
        {
            Id = product.Id,
            SourceId = product.SourceId,
            SourceName = product.SourceName,
            Name = product.Name,
            Description = product.Description,
            Version = product.Version,
            Category = product.Category,
            Tags = product.Tags,
            IsMultiStack = product.IsMultiStack,
            TotalServices = product.TotalServices,
            TotalVariables = product.TotalVariables,
            Stacks = product.Stacks.Select(s => new ProductStackDto
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
                    Type = v.Type,
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
            LastSyncedAt = product.LastSyncedAt
        };
    }
}

public class GetProductRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Detailed product DTO for product detail page
/// </summary>
public class ProductDetailDto
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Category { get; init; }
    public List<string> Tags { get; init; } = new();
    public bool IsMultiStack { get; init; }
    public int TotalServices { get; init; }
    public int TotalVariables { get; init; }
    public List<ProductStackDto> Stacks { get; init; } = new();
    public DateTime LastSyncedAt { get; init; }
}

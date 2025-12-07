using MediatR;
using ReadyStackGo.Application.UseCases.Stacks.ListStacks;

namespace ReadyStackGo.Application.UseCases.Stacks.ListProducts;

public record ListProductsQuery : IRequest<ListProductsResult>;

public record ListProductsResult(IEnumerable<ProductItem> Products);

/// <summary>
/// A product is a grouping of one or more stacks.
/// Single-stack products have exactly one stack.
/// Multi-stack products (like AMS) have multiple related stacks.
/// </summary>
public record ProductItem(
    /// <summary>
    /// Product identifier (e.g., "stacks:WordPress", "stacks:ams.project")
    /// </summary>
    string Id,

    /// <summary>
    /// Source ID where the product comes from
    /// </summary>
    string SourceId,

    /// <summary>
    /// Human-readable source name
    /// </summary>
    string SourceName,

    /// <summary>
    /// Product name (e.g., "WordPress", "ams.project")
    /// </summary>
    string Name,

    /// <summary>
    /// Product description (from the first/main stack)
    /// </summary>
    string? Description,

    /// <summary>
    /// Product version if available
    /// </summary>
    string? Version,

    /// <summary>
    /// Whether this is a multi-stack product
    /// </summary>
    bool IsMultiStack,

    /// <summary>
    /// Total number of services across all stacks
    /// </summary>
    int TotalServices,

    /// <summary>
    /// Total number of configurable variables
    /// </summary>
    int TotalVariables,

    /// <summary>
    /// Individual stacks in this product
    /// </summary>
    List<ProductStackItem> Stacks,

    /// <summary>
    /// Last sync timestamp
    /// </summary>
    DateTime LastSyncedAt
);

/// <summary>
/// A stack within a product
/// </summary>
public record ProductStackItem(
    /// <summary>
    /// Full stack ID (sourceId:stackName)
    /// </summary>
    string Id,

    /// <summary>
    /// Stack name
    /// </summary>
    string Name,

    /// <summary>
    /// Stack description
    /// </summary>
    string? Description,

    /// <summary>
    /// Services in this stack
    /// </summary>
    List<string> Services,

    /// <summary>
    /// Variables for this stack
    /// </summary>
    List<StackVariableItem> Variables
);

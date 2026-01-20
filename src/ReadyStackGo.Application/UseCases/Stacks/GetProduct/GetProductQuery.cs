using MediatR;

namespace ReadyStackGo.Application.UseCases.Stacks.GetProduct;

/// <summary>
/// Query to get a specific product by ID.
/// </summary>
public record GetProductQuery(string ProductId) : IRequest<GetProductResult?>;

/// <summary>
/// Result containing the product details if found.
/// </summary>
public record GetProductResult(ProductDetails Product);

/// <summary>
/// Detailed product information for the product detail page.
/// </summary>
public record ProductDetails(
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
    /// Product description
    /// </summary>
    string? Description,

    /// <summary>
    /// Product version if available
    /// </summary>
    string? Version,

    /// <summary>
    /// Product category (e.g., "Identity", "Database", "Web")
    /// </summary>
    string? Category,

    /// <summary>
    /// Tags for filtering
    /// </summary>
    List<string> Tags,

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
    List<ProductStackDetails> Stacks,

    /// <summary>
    /// Last sync timestamp
    /// </summary>
    DateTime LastSyncedAt,

    /// <summary>
    /// All available versions of this product (sorted newest first)
    /// </summary>
    List<ProductVersionInfo> AvailableVersions
);

/// <summary>
/// Information about a specific product version.
/// </summary>
public record ProductVersionInfo(
    /// <summary>
    /// Version string (e.g., "1.0.0", "2.0.0")
    /// </summary>
    string Version,

    /// <summary>
    /// Product ID for this specific version
    /// </summary>
    string ProductId,

    /// <summary>
    /// Default stack ID for deployment
    /// </summary>
    string DefaultStackId,

    /// <summary>
    /// Whether this is the currently displayed version
    /// </summary>
    bool IsCurrent
);

/// <summary>
/// Detailed stack information within a product.
/// </summary>
public record ProductStackDetails(
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
    List<StackVariableDetails> Variables
);

/// <summary>
/// Variable details for configuration.
/// </summary>
public record StackVariableDetails(
    string Name,
    string? DefaultValue,
    bool IsRequired,
    string Type,
    string? Label,
    string? Description,
    string? Placeholder,
    string? Group,
    int Order,
    string? Pattern,
    string? PatternError,
    double? Min,
    double? Max,
    List<SelectOptionDetails>? Options
);

/// <summary>
/// Select option for dropdown variables.
/// </summary>
public record SelectOptionDetails(
    string Value,
    string Label,
    string? Description
);

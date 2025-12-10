namespace ReadyStackGo.Domain.Catalog.Events;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Event raised when a stack definition is imported from a source.
/// Used to notify other bounded contexts about new/updated stack definitions.
/// </summary>
public class StackDefinitionImportedEvent : DomainEvent
{
    /// <summary>
    /// Unique stack identifier (format: sourceId:stackName).
    /// </summary>
    public string StackId { get; }

    /// <summary>
    /// ID of the source this stack came from.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Name of the stack.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Category for organizing stacks.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Product version from manifest.
    /// </summary>
    public string? ProductVersion { get; }

    /// <summary>
    /// Variables defined in the stack.
    /// </summary>
    public IReadOnlyList<ImportedVariable> Variables { get; }

    /// <summary>
    /// Services defined in the stack.
    /// </summary>
    public IReadOnlyList<string> Services { get; }

    /// <summary>
    /// Product information if this is a product manifest.
    /// </summary>
    public ImportedProductInfo? Product { get; }

    public StackDefinitionImportedEvent(
        string stackId,
        string sourceId,
        string name,
        string? description,
        string? category,
        string? productVersion,
        IReadOnlyList<ImportedVariable> variables,
        IReadOnlyList<string> services,
        ImportedProductInfo? product = null)
    {
        StackId = stackId;
        SourceId = sourceId;
        Name = name;
        Description = description;
        Category = category;
        ProductVersion = productVersion;
        Variables = variables;
        Services = services;
        Product = product;
    }
}

/// <summary>
/// Variable information included in import events.
/// </summary>
public record ImportedVariable(
    string Name,
    string? Label,
    string? Description,
    string? DefaultValue,
    bool IsRequired,
    string Type);

/// <summary>
/// Product information included in import events.
/// </summary>
public record ImportedProductInfo(
    string Name,
    string DisplayName,
    string? Description,
    string? Version,
    string? Category,
    IReadOnlyList<string> Tags,
    string? Icon,
    string? Documentation);

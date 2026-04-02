namespace ReadyStackGo.Domain.Deployment.Precheck;

/// <summary>
/// A single precheck result item representing one rule's outcome.
/// </summary>
/// <param name="Rule">Identifier of the rule that produced this item (e.g. "ImageAvailability").</param>
/// <param name="Severity">Severity level of the check result.</param>
/// <param name="Title">Human-readable title summarizing the check.</param>
/// <param name="Detail">Optional detail message with additional context.</param>
/// <param name="ServiceName">Optional service name if the check is service-specific.</param>
public record PrecheckItem(
    string Rule,
    PrecheckSeverity Severity,
    string Title,
    string? Detail = null,
    string? ServiceName = null);

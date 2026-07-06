namespace ReadyStackGo.Application.Services;

/// <summary>
/// Real-time progress update for a product maintenance transition
/// (entering maintenance = stopping containers, exiting = starting containers).
/// Sent per stack and per container while the transition runs, plus a terminal
/// update when it completes or fails.
/// </summary>
/// <param name="SessionId">Client-supplied session id used to target the SignalR group.</param>
/// <param name="Action">"enter" (stopping) or "exit" (starting).</param>
/// <param name="Phase">"InProgress", "Completed" or "Failed".</param>
/// <param name="CurrentStackName">Stack currently being processed (matches the deployment's stack name).</param>
/// <param name="CurrentStackDisplayName">Human-readable name of the current stack.</param>
/// <param name="StackIndex">1-based position of the current stack among the affected stacks.</param>
/// <param name="TotalStacks">Total number of affected (running) stacks.</param>
/// <param name="CurrentContainer">Container currently being processed, if any.</param>
/// <param name="ContainerIndex">1-based position of the current container within the stack (0 = stack-level update).</param>
/// <param name="TotalContainers">Total number of containers being processed for the current stack.</param>
/// <param name="Message">Human-readable status message.</param>
public record MaintenanceProgressNotification(
    string SessionId,
    string Action,
    string Phase,
    string? CurrentStackName,
    string? CurrentStackDisplayName,
    int StackIndex,
    int TotalStacks,
    string? CurrentContainer,
    int ContainerIndex,
    int TotalContainers,
    string Message);

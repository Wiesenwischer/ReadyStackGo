namespace ReadyStackGo.Domain.Stacks;

public class Stack
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required List<ContainerService> Services { get; init; }
    public StackStatus Status { get; set; }
    public DateTime? DeployedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

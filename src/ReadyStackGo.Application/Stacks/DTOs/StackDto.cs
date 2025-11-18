namespace ReadyStackGo.Application.Stacks.DTOs;

public class StackDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required List<StackServiceDto> Services { get; init; }
    public required string Status { get; init; }
    public DateTime? DeployedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

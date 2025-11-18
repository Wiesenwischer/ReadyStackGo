namespace ReadyStackGo.Application.Stacks.DTOs;

public class StackServiceDto
{
    public required string Name { get; init; }
    public required string Image { get; init; }
    public List<string>? Ports { get; init; }
    public Dictionary<string, string>? Environment { get; init; }
    public List<string>? Volumes { get; init; }
    public string? ContainerId { get; init; }
    public string? ContainerStatus { get; init; }
}

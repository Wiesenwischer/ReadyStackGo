namespace ReadyStackGo.Domain.Stacks;

public class ContainerService
{
    public required string Name { get; init; }
    public required string Image { get; init; }
    public List<string>? Ports { get; init; }
    public Dictionary<string, string>? Environment { get; init; }
    public List<string>? Volumes { get; init; }
    public string? ContainerId { get; set; }
    public string? ContainerStatus { get; set; }
}

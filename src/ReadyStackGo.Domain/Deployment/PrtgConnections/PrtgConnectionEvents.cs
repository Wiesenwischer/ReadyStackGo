namespace ReadyStackGo.Domain.Deployment.PrtgConnections;

using ReadyStackGo.Domain.SharedKernel;

public sealed class PrtgConnectionCreated : DomainEvent
{
    public PrtgConnectionId Id { get; }
    public string Name { get; }
    public string Url { get; }

    public PrtgConnectionCreated(PrtgConnectionId id, string name, string url)
    {
        Id = id;
        Name = name;
        Url = url;
    }
}

public sealed class PrtgConnectionUpdated : DomainEvent
{
    public PrtgConnectionId Id { get; }
    public string Name { get; }

    public PrtgConnectionUpdated(PrtgConnectionId id, string name)
    {
        Id = id;
        Name = name;
    }
}

public sealed class PrtgConnectionDeleted : DomainEvent
{
    public PrtgConnectionId Id { get; }
    public string Name { get; }

    public PrtgConnectionDeleted(PrtgConnectionId id, string name)
    {
        Id = id;
        Name = name;
    }
}

namespace ReadyStackGo.Domain.Deployment.PrtgConnections;

public interface IPrtgConnectionRepository
{
    PrtgConnection? Get(PrtgConnectionId id);
    IReadOnlyList<PrtgConnection> GetByOrganization(OrganizationId organizationId);
    PrtgConnection? GetByName(OrganizationId organizationId, string name);
    void Add(PrtgConnection connection);
    void Update(PrtgConnection connection);
    void Delete(PrtgConnection connection);
    void SaveChanges();
}

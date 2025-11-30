namespace ReadyStackGo.Domain.Identity.Repositories;

using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.ValueObjects;

/// <summary>
/// Repository interface for User aggregate.
/// </summary>
public interface IUserRepository
{
    UserId NextIdentity();
    void Add(User user);
    User? Get(UserId id);
    User? FindByUsername(string username);
    User? FindByEmail(EmailAddress email);
    IEnumerable<User> GetByTenant(TenantId tenantId);
    void Remove(User user);
}

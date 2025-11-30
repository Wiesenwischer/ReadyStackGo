namespace ReadyStackGo.Domain.Identity.Repositories;

using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.ValueObjects;

/// <summary>
/// Repository interface for User aggregate.
/// Users are system-wide entities. Organization membership is via RoleAssignments.
/// </summary>
public interface IUserRepository
{
    UserId NextIdentity();
    void Add(User user);
    void Update(User user);
    User? Get(UserId id);
    User? FindByUsername(string username);
    User? FindByEmail(EmailAddress email);
    IEnumerable<User> GetAll();
    void Remove(User user);
}

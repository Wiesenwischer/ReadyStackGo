namespace ReadyStackGo.Domain.IdentityAccess.Users;




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

    /// <summary>Finds the user linked to the given external identity (provider, subject), or null.</summary>
    User? FindByExternalIdentity(string provider, string subject);

    /// <summary>
    /// Removes every link to the given provider (used when the provider is removed).
    /// Returns the number of removed links.
    /// </summary>
    int RemoveExternalIdentitiesOfProvider(string provider);
    IEnumerable<User> GetAll();
    void Remove(User user);
}

namespace ReadyStackGo.DomainTests.Support;

using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// In-memory implementation of IUserRepository for testing.
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<UserId, User> _users = new();

    public UserId NextIdentity() => UserId.Create();

    public void Add(User user)
    {
        _users[user.Id] = user;
    }

    public void Update(User user)
    {
        _users[user.Id] = user;
    }

    public User? Get(UserId id)
    {
        return _users.GetValueOrDefault(id);
    }

    public User? FindByUsername(string username)
    {
        return _users.Values.FirstOrDefault(u => u.Username == username);
    }

    public User? FindByEmail(EmailAddress email)
    {
        return _users.Values.FirstOrDefault(u => u.Email == email);
    }

    public IEnumerable<User> GetAll()
    {
        return _users.Values;
    }

    public void Remove(User user)
    {
        _users.Remove(user.Id);
    }

    public void Clear()
    {
        _users.Clear();
    }
}

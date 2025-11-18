namespace ReadyStackGo.Domain.Auth;

public class User
{
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
    public required string Role { get; init; }
    public DateTime CreatedAt { get; init; }
}

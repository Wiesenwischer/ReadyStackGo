namespace ReadyStackGo.Domain.IdentityAccess.Users;

using System.Text;

/// <summary>
/// Domain service that forms a unique username for an account created through an external
/// identity provider: from the provider's username claim, otherwise from the local part of
/// the email address. Follows the username rules of the setup wizard ([a-zA-Z0-9_], 3 to 50
/// characters).
/// </summary>
public class UsernameGenerator
{
    public const int MinLength = 3;
    public const int MaxBaseLength = 40;
    public const int MaxLength = 50;
    private const string Fallback = "user";

    private readonly IUserRepository _userRepository;

    public UsernameGenerator(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    /// <summary>
    /// Returns a username that is valid and not yet taken.
    /// </summary>
    /// <param name="preferredUsername">The provider's username claim, may be null or empty.</param>
    /// <param name="email">The email address, used when the preferred username is unusable.</param>
    public string Generate(string? preferredUsername, string? email)
    {
        var baseName = Sanitize(preferredUsername);
        if (baseName == null)
        {
            var localPart = email?.Split('@')[0];
            baseName = Sanitize(localPart) ?? Fallback;
        }

        if (baseName.Length < MinLength)
        {
            baseName = baseName.PadRight(MinLength, '0');
        }
        if (baseName.Length > MaxBaseLength)
        {
            baseName = baseName[..MaxBaseLength];
        }

        var candidate = baseName;
        var suffix = 1;
        while (_userRepository.FindByUsername(candidate) != null)
        {
            var suffixText = (suffix++).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var room = MaxLength - suffixText.Length;
            candidate = (baseName.Length > room ? baseName[..room] : baseName) + suffixText;
        }
        return candidate;
    }

    /// <summary>
    /// Replaces every character outside [a-zA-Z0-9_] with an underscore. Returns null when
    /// nothing usable remains (empty, or only underscores).
    /// </summary>
    internal static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            builder.Append(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' ? c : '_');
        }

        var result = builder.ToString();
        return result.Trim('_').Length == 0 ? null : result;
    }
}

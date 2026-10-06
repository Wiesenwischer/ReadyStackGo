using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.DataAccess;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Infrastructure.CommandLine;

/// <summary>
/// Emergency access from inside the container, without a running identity provider and
/// without starting the web host:
/// <code>docker compose exec readystackgo rsgo admin set-password &lt;username&gt; [--generate]</code>
/// Hosts (also distributions with their own Program.cs) call <see cref="IsAdminCommand"/> and
/// <see cref="RunAsync"/> at the start of Main, before building the web application.
/// </summary>
public static class AdminCommandLine
{
    public const int ExitSuccess = 0;
    public const int ExitUnknownUser = 1;
    public const int ExitInvalidPassword = 2;
    public const int ExitDatabase = 3;
    public const int ExitUsage = 64;

    private const int GeneratedLength = 20;

    private const string Usage =
        "Usage: rsgo admin set-password <username> [--generate]\n" +
        "  Sets a local password for <username>. Without --generate the password is read from the\n" +
        "  terminal (twice, hidden) or as one line from standard input.";

    /// <summary>True if the arguments ask for an admin command ("admin ...").</summary>
    public static bool IsAdminCommand(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "admin", StringComparison.Ordinal);

    /// <summary>Runs an admin command against the database of this installation and returns the exit code.</summary>
    public static async Task<int> RunAsync(string[] args, IConfiguration configuration, IAdminConsole? console = null)
    {
        console ??= new SystemAdminConsole();

        var parsed = Parse(args, console);
        if (parsed == null)
        {
            return ExitUsage;
        }

        var services = new ServiceCollection();
        services.AddDataAccess(configuration);
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        try
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            return SetPassword(parsed.Value.Username, parsed.Value.Generate, users, hasher, console);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            console.Error($"Could not access the database: {ex.Message}");
            return ExitDatabase;
        }
    }

    /// <summary>Parses "admin set-password &lt;username&gt; [--generate]". Writes usage and returns null on errors.</summary>
    internal static (string Username, bool Generate)? Parse(string[] args, IAdminConsole console)
    {
        var rest = args.Skip(1).ToList();
        if (rest.Count == 0 || rest[0] != "set-password")
        {
            console.Error(Usage);
            return null;
        }

        string? username = null;
        var generate = false;
        foreach (var arg in rest.Skip(1))
        {
            if (arg == "--generate")
            {
                generate = true;
            }
            else if (arg.StartsWith('-'))
            {
                console.Error($"Unknown option '{arg}'.\n{Usage}");
                return null;
            }
            else if (username == null)
            {
                username = arg;
            }
            else
            {
                console.Error($"Unexpected argument '{arg}'.\n{Usage}");
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            console.Error($"The username is missing.\n{Usage}");
            return null;
        }

        return (username, generate);
    }

    /// <summary>Sets the password and logs the emergency access. Returns the exit code.</summary>
    internal static int SetPassword(string username, bool generate, IUserRepository users, IPasswordHasher hasher, IAdminConsole console)
    {
        var user = users.FindByUsername(username);
        if (user == null)
        {
            console.Error($"User '{username}' does not exist.");
            return ExitUnknownUser;
        }

        string password;
        if (generate)
        {
            password = GeneratePassword();
        }
        else
        {
            var entered = console.ReadPassword();
            if (entered == null)
            {
                console.Error("The passwords do not match.");
                return ExitInvalidPassword;
            }
            password = entered;
        }

        HashedPassword hashed;
        try
        {
            hashed = HashedPassword.Create(password, hasher);
        }
        catch (ArgumentException ex)
        {
            console.Error(ex.Message);
            return ExitInvalidPassword;
        }

        if (user.HasPassword)
        {
            user.ChangePassword(hashed);
        }
        else
        {
            user.SetInitialPassword(hashed);
        }
        users.Update(user);

        if (generate)
        {
            console.Out($"Generated password for '{user.Username}' (shown once): {password}");
        }
        console.Log($"Emergency access: local password set for user '{user.Username}' from the command line");
        return ExitSuccess;
    }

    /// <summary>A random password of 20 characters that meets the password rules.</summary>
    internal static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;

        var chars = new char[GeneratedLength];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}

/// <summary>Terminal access of the admin command (abstracted for tests).</summary>
public interface IAdminConsole
{
    /// <summary>Reads the new password: twice hidden on a terminal, one line from redirected input. Null if the two entries differ.</summary>
    string? ReadPassword();

    void Out(string message);

    void Error(string message);

    /// <summary>Writes a warning that should also reach the container log.</summary>
    void Log(string message);
}

/// <summary>The real console; the log line also goes to the main process output so it shows in docker logs.</summary>
public class SystemAdminConsole : IAdminConsole
{
    private const string ContainerStdout = "/proc/1/fd/1";

    public string? ReadPassword()
    {
        if (Console.IsInputRedirected)
        {
            return Console.In.ReadLine() ?? string.Empty;
        }

        var first = ReadHidden("New password: ");
        var second = ReadHidden("Repeat password: ");
        return first == second ? first : null;
    }

    public void Out(string message) => Console.Out.WriteLine(message);

    public void Error(string message) => Console.Error.WriteLine(message);

    public void Log(string message)
    {
        var line = $"{DateTime.UtcNow:O} warn: ReadyStackGo.AdminCommandLine[0] {message}";
        Console.Out.WriteLine(line);

        try
        {
            if (File.Exists(ContainerStdout))
            {
                File.AppendAllText(ContainerStdout, line + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not writable (e.g. a container without root): the line is in the command output only.
        }
    }

    private static string ReadHidden(string prompt)
    {
        Console.Out.Write(prompt);
        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }
                continue;
            }
            builder.Append(key.KeyChar);
        }
        Console.Out.WriteLine();
        return builder.ToString();
    }
}

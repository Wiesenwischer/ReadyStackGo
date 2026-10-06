using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.CommandLine;
using ReadyStackGo.Infrastructure.Security.Authentication;
using ReadyStackGo.UnitTests.TestSupport;

namespace ReadyStackGo.UnitTests.Infrastructure.CommandLine;

public class AdminCommandLineTests
{
    private readonly FakeConsole _console = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly IPasswordHasher _hasher = new PrefixPasswordHasher();

    private sealed class FakeConsole : IAdminConsole
    {
        public string? PasswordToReturn { get; set; }
        public int ReadCount { get; private set; }
        public List<string> OutLines { get; } = new();
        public List<string> ErrorLines { get; } = new();
        public List<string> LogLines { get; } = new();

        public string? ReadPassword()
        {
            ReadCount++;
            return PasswordToReturn;
        }

        public void Out(string message) => OutLines.Add(message);
        public void Error(string message) => ErrorLines.Add(message);
        public void Log(string message) => LogLines.Add(message);
    }

    private User AddExternalUser(string username = "admin")
    {
        var user = User.RegisterExternal(_users.NextIdentity(), username, new EmailAddress($"{username}@example.com"), "wysch", "sub-1");
        _users.Add(user);
        return user;
    }

    private User AddLocalUser(string username = "local", string password = "OldPass123")
    {
        var user = User.Register(_users.NextIdentity(), username, new EmailAddress($"{username}@example.com"), HashedPassword.Create(password, _hasher));
        _users.Add(user);
        return user;
    }

    #region IsAdminCommand

    [Theory]
    [InlineData(new[] { "admin" }, true)]
    [InlineData(new[] { "admin", "set-password", "x" }, true)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "Admin" }, false)]
    [InlineData(new[] { "--urls", "http://+:8080" }, false)]
    public void IsAdminCommand_OnlyForFirstArgumentAdmin(string[] args, bool expected)
    {
        AdminCommandLine.IsAdminCommand(args).Should().Be(expected);
    }

    #endregion

    #region Parse

    [Fact]
    public void Parse_UsernameOnly_ReturnsUsernameWithoutGenerate()
    {
        var result = AdminCommandLine.Parse(["admin", "set-password", "marcus"], _console);

        result.Should().Be(("marcus", false));
        _console.ErrorLines.Should().BeEmpty();
    }

    [Theory]
    [InlineData("admin", "set-password", "marcus", "--generate")]
    [InlineData("admin", "set-password", "--generate", "marcus")]
    public void Parse_WithGenerate_InAnyPosition(params string[] args)
    {
        AdminCommandLine.Parse(args, _console).Should().Be(("marcus", true));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("admin", "set-password")]
    [InlineData("admin", "set-password", "--generate")]
    [InlineData("admin", "set-password", "")]
    [InlineData("admin", "set-password", "  ")]
    public void Parse_MissingUsername_ReturnsNullAndWritesUsage(params string[] args)
    {
        AdminCommandLine.Parse(args, _console).Should().BeNull();

        _console.ErrorLines.Should().ContainSingle().Which.Should().Contain("Usage:");
    }

    [Theory]
    [InlineData("admin", "reset-password", "marcus")]
    [InlineData("admin", "SET-PASSWORD", "marcus")]
    public void Parse_UnknownSubcommand_ReturnsNull(params string[] args)
    {
        AdminCommandLine.Parse(args, _console).Should().BeNull();

        _console.ErrorLines.Should().ContainSingle().Which.Should().Contain("Usage:");
    }

    [Theory]
    [InlineData("admin", "set-password", "marcus", "--force")]
    [InlineData("admin", "set-password", "marcus", "-g")]
    [InlineData("admin", "set-password", "--password=secret", "marcus")]
    public void Parse_UnknownOption_ReturnsNullAndNamesOption(params string[] args)
    {
        AdminCommandLine.Parse(args, _console).Should().BeNull();

        _console.ErrorLines.Should().ContainSingle().Which.Should().Contain("Unknown option").And.Contain("Usage:");
    }

    [Fact]
    public void Parse_SecondPositionalArgument_ReturnsNull()
    {
        AdminCommandLine.Parse(["admin", "set-password", "marcus", "Secret123"], _console).Should().BeNull();

        _console.ErrorLines.Should().ContainSingle().Which.Should().Contain("Unexpected argument");
    }

    [Fact]
    public async Task RunAsync_InvalidArguments_ReturnsUsageExitCodeWithoutTouchingDatabase()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exitCode = await AdminCommandLine.RunAsync(["admin", "set-password"], configuration, _console);

        exitCode.Should().Be(AdminCommandLine.ExitUsage).And.Be(64);
    }

    #endregion

    #region SetPassword

    [Fact]
    public void SetPassword_UnknownUser_ReturnsOneAndDoesNotReadPassword()
    {
        var exitCode = AdminCommandLine.SetPassword("nobody", generate: false, _users, _hasher, _console);

        exitCode.Should().Be(AdminCommandLine.ExitUnknownUser).And.Be(1);
        _console.ReadCount.Should().Be(0);
        _console.ErrorLines.Should().ContainSingle().Which.Should().Contain("nobody");
        _console.LogLines.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public void SetPassword_WeakPassword_ReturnsTwoAndKeepsUserUnchanged(string weak)
    {
        var user = AddExternalUser();
        _console.PasswordToReturn = weak;

        var exitCode = AdminCommandLine.SetPassword("admin", generate: false, _users, _hasher, _console);

        exitCode.Should().Be(AdminCommandLine.ExitInvalidPassword).And.Be(2);
        user.HasPassword.Should().BeFalse();
        _users.UpdateCount.Should().Be(0);
        _console.ErrorLines.Should().ContainSingle();
        _console.LogLines.Should().BeEmpty();
    }

    [Fact]
    public void SetPassword_PasswordsDoNotMatch_ReturnsTwo()
    {
        var user = AddExternalUser();
        _console.PasswordToReturn = null;

        var exitCode = AdminCommandLine.SetPassword("admin", generate: false, _users, _hasher, _console);

        exitCode.Should().Be(AdminCommandLine.ExitInvalidPassword);
        _console.ErrorLines.Should().ContainSingle().Which.Should().Contain("do not match");
        user.HasPassword.Should().BeFalse();
        _users.UpdateCount.Should().Be(0);
    }

    [Fact]
    public void SetPassword_UserWithoutPassword_SetsVerifiablePasswordAndLogs()
    {
        var user = AddExternalUser();
        _console.PasswordToReturn = "NewPass123";

        var exitCode = AdminCommandLine.SetPassword("admin", generate: false, _users, _hasher, _console);

        exitCode.Should().Be(AdminCommandLine.ExitSuccess);
        user.HasPassword.Should().BeTrue();
        user.Password!.Verify("NewPass123", _hasher).Should().BeTrue();
        user.FindExternalIdentity("wysch").Should().NotBeNull();
        _users.UpdateCount.Should().Be(1);
        _console.LogLines.Should().ContainSingle().Which.Should().Contain("Emergency access").And.Contain("admin");
        _console.OutLines.Should().BeEmpty();
        string.Join("\n", _console.LogLines.Concat(_console.ErrorLines)).Should().NotContain("NewPass123");
    }

    [Fact]
    public void SetPassword_UserWithPassword_ChangesPassword()
    {
        var user = AddLocalUser();
        _console.PasswordToReturn = "NewPass123";

        var exitCode = AdminCommandLine.SetPassword("local", generate: false, _users, _hasher, _console);

        exitCode.Should().Be(AdminCommandLine.ExitSuccess);
        user.Password!.Verify("NewPass123", _hasher).Should().BeTrue();
        user.Password.Verify("OldPass123", _hasher).Should().BeFalse();
    }

    [Fact]
    public void SetPassword_Generate_SetsPasswordShownOnceAndDoesNotPrompt()
    {
        var user = AddExternalUser();

        var exitCode = AdminCommandLine.SetPassword("admin", generate: true, _users, _hasher, _console);

        exitCode.Should().Be(AdminCommandLine.ExitSuccess);
        _console.ReadCount.Should().Be(0);
        var line = _console.OutLines.Should().ContainSingle().Subject;
        var generated = line[(line.LastIndexOf(": ", StringComparison.Ordinal) + 2)..];
        generated.Should().HaveLength(20);
        user.Password!.Verify(generated, _hasher).Should().BeTrue();
        _console.LogLines.Should().ContainSingle().Which.Should().NotContain(generated);
    }

    [Fact]
    public void SetPassword_WithRealBCryptHasher_PasswordVerifies()
    {
        var hasher = new BCryptPasswordHasher();
        var user = AddExternalUser();
        _console.PasswordToReturn = "NewPass123";

        AdminCommandLine.SetPassword("admin", generate: false, _users, hasher, _console).Should().Be(AdminCommandLine.ExitSuccess);

        user.Password!.Verify("NewPass123", hasher).Should().BeTrue();
        user.Password.Verify("WrongPass123", hasher).Should().BeFalse();
    }

    #endregion

    #region GeneratePassword

    [Fact]
    public void GeneratePassword_AlwaysMeetsPasswordRules()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = AdminCommandLine.GeneratePassword();

            password.Should().HaveLength(20);
            password.Should().MatchRegex("[A-Z]").And.MatchRegex("[a-z]").And.MatchRegex("[0-9]");
            password.Should().MatchRegex("^[A-Za-z0-9]+$");
            var act = () => HashedPassword.Create(password, _hasher);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void GeneratePassword_AvoidsAmbiguousCharacters()
    {
        var all = string.Concat(Enumerable.Range(0, 200).Select(_ => AdminCommandLine.GeneratePassword()));

        all.Should().NotContainAny("0", "1", "O", "I", "l");
    }

    [Fact]
    public void GeneratePassword_IsRandom()
    {
        var passwords = Enumerable.Range(0, 50).Select(_ => AdminCommandLine.GeneratePassword()).ToList();

        passwords.Distinct().Should().HaveCount(50);
    }

    #endregion
}

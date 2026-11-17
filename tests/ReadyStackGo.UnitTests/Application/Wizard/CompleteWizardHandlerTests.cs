using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Wizard.CompleteWizard;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Application.Wizard;

public class CompleteWizardHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ISystemConfigService> _systemConfig = new();

    private CompleteWizardHandler CreateHandler() => new(_users.Object, _systemConfig.Object);

    private static User CreateSystemAdmin()
    {
        var user = User.Register(
            UserId.NewId(),
            "admin",
            new EmailAddress("admin@system.local"),
            HashedPassword.FromHash("hashed"));
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        return user;
    }

    [Fact]
    public async Task Handle_WithAdmin_StoresTurquoiseAsDefaultTheme()
    {
        _users.Setup(r => r.GetAll()).Returns([CreateSystemAdmin()]);

        var result = await CreateHandler().Handle(new CompleteWizardCommand(null), CancellationToken.None);

        result.Success.Should().BeTrue();
        _systemConfig.Verify(s => s.SetWizardStateAsync(WizardState.Installed), Times.Once);
        _systemConfig.Verify(s => s.SetDefaultThemeIfUnsetAsync("turquoise"), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutAdmin_StoresNoDefaultTheme()
    {
        _users.Setup(r => r.GetAll()).Returns([]);

        var result = await CreateHandler().Handle(new CompleteWizardCommand(null), CancellationToken.None);

        result.Success.Should().BeFalse();
        _systemConfig.Verify(s => s.SetDefaultThemeIfUnsetAsync(It.IsAny<string>()), Times.Never);
        _systemConfig.Verify(s => s.SetWizardStateAsync(It.IsAny<WizardState>()), Times.Never);
    }
}

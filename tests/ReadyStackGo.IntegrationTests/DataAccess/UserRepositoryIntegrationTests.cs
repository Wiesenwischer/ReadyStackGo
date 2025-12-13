namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for UserRepository with real SQLite database.
/// Users are system-wide entities - organization membership is via RoleAssignments.
/// </summary>
public class UserRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestFixture _fixture;
    private readonly UserRepository _repository;
    private readonly TestPasswordHasher _hasher;

    public UserRepositoryIntegrationTests()
    {
        _fixture = new SqliteTestFixture();
        _repository = new UserRepository(_fixture.Context);
        _hasher = new TestPasswordHasher();
    }

    public void Dispose() => _fixture.Dispose();

    private User CreateUser(string username, string email) =>
        User.Register(
            UserId.Create(),
            username,
            new EmailAddress(email),
            HashedPassword.FromHash(_hasher.Hash("SecurePass123!"))
        );

    [Fact]
    public void Add_ShouldPersistUser()
    {
        // Arrange
        var user = CreateUser("testuser", "test@example.com");

        // Act
        _repository.Add(user);

        // Assert - use fresh context
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Users.Find(user.Id);

        persisted.Should().NotBeNull();
        persisted!.Username.Should().Be("testuser");
        persisted.Email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Add_ShouldPersistUserWithRoles()
    {
        // Arrange
        var user = CreateUser("adminuser", "admin@example.com");
        var orgId = OrganizationId.Create();
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.OrganizationOwner, orgId.Value.ToString()));

        // Act
        _repository.Add(user);

        // Assert - verify roles persisted
        using var verifyContext = _fixture.CreateNewContext();
        var verifyRepository = new UserRepository(verifyContext);
        var persisted = verifyRepository.Get(user.Id);

        persisted.Should().NotBeNull();
        persisted!.RoleAssignments.Should().HaveCount(2);
        persisted.RoleAssignments.Should().Contain(r => r.RoleId.Value == "SystemAdmin");
        persisted.RoleAssignments.Should().Contain(r => r.RoleId.Value == "OrganizationOwner");
    }

    [Fact]
    public void Get_ShouldReturnUser_WhenExists()
    {
        // Arrange
        var userId = UserId.Create();
        var user = User.Register(userId, "findme", new EmailAddress("find@example.com"), HashedPassword.FromHash("hash"));
        _repository.Add(user);

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new UserRepository(queryContext);
        var result = queryRepository.Get(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("findme");
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var nonExistentId = UserId.Create();

        // Act
        var result = _repository.Get(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FindByUsername_ShouldReturnUser()
    {
        // Arrange
        var user = CreateUser("uniqueuser", "unique@example.com");
        _repository.Add(user);

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new UserRepository(queryContext);
        var result = queryRepository.FindByUsername("uniqueuser");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Value.Should().Be("unique@example.com");
    }

    [Fact]
    public void FindByEmail_ShouldReturnUser()
    {
        // Arrange
        var user = CreateUser("emailuser", "specific@example.com");
        _repository.Add(user);

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new UserRepository(queryContext);
        var result = queryRepository.FindByEmail(new EmailAddress("specific@example.com"));

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("emailuser");
    }

    [Fact]
    public void Update_ShouldPersistChanges()
    {
        // Arrange
        var user = CreateUser("updateuser", "update@example.com");
        _repository.Add(user);

        // Act - disable user
        user.Disable();
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Users.Find(user.Id);

        updated.Should().NotBeNull();
        updated!.Enablement.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldPersistNewRoleAssignments()
    {
        // Arrange
        var user = CreateUser("roleuser", "role@example.com");
        var orgId = OrganizationId.Create();
        _repository.Add(user);

        // Act - add role
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Operator, orgId.Value.ToString()));
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var verifyRepository = new UserRepository(verifyContext);
        var updated = verifyRepository.Get(user.Id);

        updated.Should().NotBeNull();
        updated!.RoleAssignments.Should().HaveCount(1);
        updated.RoleAssignments.First().RoleId.Value.Should().Be("Operator");
    }

    [Fact]
    public void GetAll_ShouldReturnAllUsers()
    {
        // Arrange
        var user1 = CreateUser("user1", "user1@example.com");
        var user2 = CreateUser("user2", "user2@example.com");
        _repository.Add(user1);
        _repository.Add(user2);

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new UserRepository(queryContext);
        var results = queryRepository.GetAll();

        // Assert
        results.Should().HaveCount(2);
        results.Select(u => u.Username).Should().Contain(["user1", "user2"]);
    }

    [Fact]
    public void Add_ShouldThrowOnDuplicateEmail()
    {
        // Arrange
        var user1 = CreateUser("user1", "duplicate@example.com");
        _repository.Add(user1);

        var user2 = CreateUser("user2", "duplicate@example.com");

        // Act & Assert
        var act = () => _repository.Add(user2);
        act.Should().Throw<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public void Add_ShouldThrowOnDuplicateUsername()
    {
        // Arrange - Usernames are now globally unique (not per organization)
        var user1 = CreateUser("sameuser", "user1a@example.com");
        _repository.Add(user1);

        var user2 = CreateUser("sameuser", "user2b@example.com");

        // Act & Assert
        var act = () => _repository.Add(user2);
        act.Should().Throw<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public void NextIdentity_ShouldReturnUniqueUserId()
    {
        // Act
        var id1 = _repository.NextIdentity();
        var id2 = _repository.NextIdentity();

        // Assert
        id1.Should().NotBeNull();
        id2.Should().NotBeNull();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void Remove_ShouldDeleteUser()
    {
        // Arrange
        var user = CreateUser("todelete", "delete@example.com");
        _repository.Add(user);

        // Act
        _repository.Remove(user);

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var deleted = verifyContext.Users.Find(user.Id);
        deleted.Should().BeNull();
    }

    /// <summary>
    /// Simple test password hasher for integration tests.
    /// </summary>
    private class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string plainTextPassword) => $"hashed:{plainTextPassword}";
        public bool Verify(string plainTextPassword, string hash) => hash == $"hashed:{plainTextPassword}";
    }
}

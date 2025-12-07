using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for Organization domain events.
/// </summary>
public class OrganizationEventTests
{
    #region OrganizationProvisioned Tests

    [Fact]
    public void OrganizationProvisioned_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var name = "Test Organization";

        // Act
        var evt = new OrganizationProvisioned(orgId, name);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.OrganizationName.Should().Be(name);
    }

    [Fact]
    public void OrganizationProvisioned_InheritsFromDomainEvent()
    {
        // Arrange & Act
        var evt = new OrganizationProvisioned(OrganizationId.NewId(), "Org");

        // Assert
        evt.Should().BeAssignableTo<ReadyStackGo.Domain.SharedKernel.DomainEvent>();
    }

    #endregion

    #region OrganizationActivated Tests

    [Fact]
    public void OrganizationActivated_Constructor_SetsOrganizationId()
    {
        // Arrange
        var orgId = OrganizationId.NewId();

        // Act
        var evt = new OrganizationActivated(orgId);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
    }

    #endregion

    #region OrganizationDeactivated Tests

    [Fact]
    public void OrganizationDeactivated_Constructor_SetsOrganizationId()
    {
        // Arrange
        var orgId = OrganizationId.NewId();

        // Act
        var evt = new OrganizationDeactivated(orgId);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
    }

    #endregion
}

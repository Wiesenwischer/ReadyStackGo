using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Infrastructure.DataAccess;

namespace ReadyStackGo.UnitTests.Infrastructure;

public class DomainEventDispatchTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DomainEventDispatchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
    [Fact]
    public void SaveChanges_WithPendingEvents_DispatchesAfterSave()
    {
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        var dispatchedEvents = new List<IDomainEvent>();

        dispatcherMock
            .Setup(d => d.DispatchEventsAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<IDomainEvent>, CancellationToken>((events, _) => dispatchedEvents.AddRange(events))
            .Returns(Task.CompletedTask);

        using var context = CreateContext(dispatcherMock.Object);
        context.Database.EnsureCreated();

        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product:stack", "test-stack", "test-project", UserId.NewId());
        context.Deployments.Add(deployment);

        context.SaveChanges();

        dispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeploymentStarted>();
    }

    [Fact]
    public void SaveChanges_WithoutEvents_DoesNotDispatch()
    {
        var dispatcherMock = new Mock<IDomainEventDispatcher>();

        using var context = CreateContext(dispatcherMock.Object);
        context.Database.EnsureCreated();

        // Add a deployment whose events are already cleared
        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product:stack", "test-stack", "test-project", UserId.NewId());
        deployment.ClearDomainEvents();
        context.Deployments.Add(deployment);
        context.SaveChanges();

        dispatcherMock.Verify(
            d => d.DispatchEventsAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SaveChanges_ClearsEventsAfterDispatch()
    {
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        dispatcherMock
            .Setup(d => d.DispatchEventsAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var context = CreateContext(dispatcherMock.Object);
        context.Database.EnsureCreated();

        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product:stack", "test-stack", "test-project", UserId.NewId());
        context.Deployments.Add(deployment);
        context.SaveChanges();

        deployment.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SaveChanges_ReentrantCall_DoesNotStackOverflow()
    {
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        ReadyStackGoDbContext? capturedContext = null;

        dispatcherMock
            .Setup(d => d.DispatchEventsAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<IDomainEvent>, CancellationToken>((_, _) =>
            {
                // Simulate handler calling SaveChanges during dispatch
                capturedContext!.SaveChanges();
            })
            .Returns(Task.CompletedTask);

        using var context = CreateContext(dispatcherMock.Object);
        capturedContext = context;
        context.Database.EnsureCreated();

        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product:stack", "test-stack", "test-project", UserId.NewId());
        context.Deployments.Add(deployment);

        // Should not throw StackOverflowException
        var act = () => context.SaveChanges();
        act.Should().NotThrow();
    }

    [Fact]
    public void SaveChanges_WithoutDispatcher_WorksNormally()
    {
        using var context = CreateContext(dispatcher: null);
        context.Database.EnsureCreated();

        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product:stack", "test-stack", "test-project", UserId.NewId());
        context.Deployments.Add(deployment);

        var act = () => context.SaveChanges();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SaveChangesAsync_WithPendingEvents_DispatchesAfterSave()
    {
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        var dispatchedEvents = new List<IDomainEvent>();

        dispatcherMock
            .Setup(d => d.DispatchEventsAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<IDomainEvent>, CancellationToken>((events, _) => dispatchedEvents.AddRange(events))
            .Returns(Task.CompletedTask);

        using var context = CreateContext(dispatcherMock.Object);
        context.Database.EnsureCreated();

        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product:stack", "test-stack", "test-project", UserId.NewId());
        context.Deployments.Add(deployment);

        await context.SaveChangesAsync();

        dispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeploymentStarted>();
    }

    private ReadyStackGoDbContext CreateContext(IDomainEventDispatcher? dispatcher)
    {
        var options = new DbContextOptionsBuilder<ReadyStackGoDbContext>()
            .UseSqlite(_connection)
            .Options;

        if (dispatcher is null)
            return new ReadyStackGoDbContext(options);

        return new ReadyStackGoDbContext(options, dispatcher);
    }
}

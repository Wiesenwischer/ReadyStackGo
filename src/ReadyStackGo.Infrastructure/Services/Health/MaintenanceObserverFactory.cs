using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Factory for creating maintenance observer instances.
/// Uses keyed DI services to resolve implementations based on ObserverType.
/// </summary>
public sealed class MaintenanceObserverFactory : IMaintenanceObserverFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _observerTypes = new(StringComparer.OrdinalIgnoreCase);

    public MaintenanceObserverFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        // Register known observer types
        // New observer types only need to be added here
        RegisterObserver<SqlExtendedPropertyObserver>(ObserverType.SqlExtendedProperty);
        RegisterObserver<SqlQueryObserver>(ObserverType.SqlQuery);
        RegisterObserver<HttpObserver>(ObserverType.Http);
        RegisterObserver<FileObserver>(ObserverType.File);
    }

    private void RegisterObserver<TObserver>(ObserverType type) where TObserver : IMaintenanceObserver
    {
        _observerTypes[type.Value] = typeof(TObserver);
    }

    public IMaintenanceObserver Create(MaintenanceObserverConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!_observerTypes.TryGetValue(config.Type.Value, out var observerType))
        {
            throw new NotSupportedException($"Observer type '{config.Type.Value}' is not supported");
        }

        // Create instance using DI, passing the config
        var observer = (IMaintenanceObserver)ActivatorUtilities.CreateInstance(
            _serviceProvider,
            observerType,
            config);

        return observer;
    }

    public bool IsSupported(ObserverType type)
    {
        return _observerTypes.ContainsKey(type.Value);
    }

    public IEnumerable<ObserverType> SupportedTypes =>
        _observerTypes.Keys.Select(ObserverType.FromValue);
}

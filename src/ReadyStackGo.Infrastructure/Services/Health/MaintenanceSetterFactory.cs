using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Factory for creating maintenance setter instances. Mirror of
/// <see cref="MaintenanceObserverFactory"/>.
/// </summary>
public sealed class MaintenanceSetterFactory : IMaintenanceSetterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<SetterType, Type> _setterTypes = new();

    public MaintenanceSetterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _setterTypes[SetterType.SqlExtendedProperty] = typeof(SqlExtendedPropertySetter);
        _setterTypes[SetterType.Webhook] = typeof(WebhookSetter);
    }

    public IMaintenanceSetter Create(MaintenanceSetterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!_setterTypes.TryGetValue(config.Type, out var setterType))
        {
            throw new NotSupportedException($"Setter type '{config.Type}' is not supported");
        }

        return (IMaintenanceSetter)ActivatorUtilities.CreateInstance(
            _serviceProvider, setterType, config);
    }

    public bool IsSupported(SetterType type) => _setterTypes.ContainsKey(type);
}

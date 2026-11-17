using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Shared behaviour of the SQL-based maintenance observers: non-pooled connections and an
/// availability gate in front of every read.
///
/// The gate exists because the maintenance flag lives inside the very database a product takes
/// exclusively while it maintains itself. During that window the flag is unreadable, so the gate
/// answers on the observer's behalf — the database being exclusive *is* maintenance — and RSGO stays
/// off the database until it is ONLINE and MULTI_USER again. That also covers the reverse direction:
/// once the product is finished, the gate opens and the next poll reads the flag normally, so the
/// automatic return to normal operation keeps working without anyone intervening.
/// </summary>
public abstract class SqlMaintenanceObserverBase : BaseMaintenanceObserver
{
    private readonly ISqlDatabaseAvailabilityProbe _availabilityProbe;
    private bool _probeUnavailableLogged;
    private bool _exclusiveLogged;

    protected SqlMaintenanceObserverBase(
        MaintenanceObserverConfig config,
        ISqlDatabaseAvailabilityProbe availabilityProbe,
        ILogger logger)
        : base(config, logger)
    {
        _availabilityProbe = availabilityProbe ?? throw new ArgumentNullException(nameof(availabilityProbe));
    }

    /// <summary>
    /// The connection string as configured in the manifest. Implementations throw when it is
    /// missing.
    /// </summary>
    protected abstract string RawConnectionString { get; }

    /// <summary>
    /// The connection string RSGO actually connects with: pooling disabled so no session outlives
    /// the read. See <see cref="SqlObserverConnection"/>.
    /// </summary>
    protected string ConnectionString => SqlObserverConnection.Normalize(RawConnectionString);

    protected override async Task<ObserverResult?> TryShortCircuitAsync(CancellationToken cancellationToken)
    {
        var availability = await _availabilityProbe.ProbeAsync(RawConnectionString, cancellationToken);

        if (availability.IsExclusive)
        {
            // Log the entry into the exclusive window once, not on every poll of a window that can
            // last well over half an hour.
            if (!_exclusiveLogged)
            {
                Logger.LogInformation(
                    "Product database is not available for reading ({Detail}) — reporting maintenance " +
                    "and leaving the database untouched until it is ONLINE/MULTI_USER again",
                    availability.Detail);
                _exclusiveLogged = true;
            }

            return ObserverResult.MaintenanceRequired($"database-exclusive ({availability.Detail})");
        }

        if (availability.IsUnknown)
        {
            // Degraded but functional: without the gate the observer behaves as it always did —
            // reading the flag directly and reporting a failed check while the database is exclusive.
            // Preferable to parking a deployment in maintenance over a permission problem.
            if (!_probeUnavailableLogged)
            {
                Logger.LogWarning(
                    "Cannot determine product database availability ({Detail}) — falling back to " +
                    "reading the maintenance flag directly. Granting the observer login access to " +
                    "master restores the protection against reading a database that is held exclusively",
                    availability.Detail);
                _probeUnavailableLogged = true;
            }

            return null;
        }

        _exclusiveLogged = false;
        return null;
    }
}

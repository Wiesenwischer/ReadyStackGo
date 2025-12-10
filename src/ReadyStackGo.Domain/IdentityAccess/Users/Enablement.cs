namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object representing user enablement status with optional time constraints.
/// </summary>
public sealed class Enablement : ValueObject
{
    public bool Enabled { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }

    // For EF Core
    private Enablement() { }

    private Enablement(bool enabled, DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            throw new ArgumentException("Enablement start date must be before end date.");
        }

        Enabled = enabled;
        StartDate = startDate;
        EndDate = endDate;
    }

    public static Enablement IndefiniteEnablement() => new(true, null, null);
    public static Enablement Disabled() => new(false, null, null);
    public static Enablement TimeLimited(DateTime startDate, DateTime endDate) => new(true, startDate, endDate);

    public bool IsEnabled
    {
        get
        {
            if (!Enabled) return false;
            if (!StartDate.HasValue && !EndDate.HasValue) return true;

            var now = SystemClock.UtcNow;
            if (StartDate.HasValue && now < StartDate.Value) return false;
            if (EndDate.HasValue && now > EndDate.Value) return false;

            return true;
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Enabled;
        yield return StartDate;
        yield return EndDate;
    }

    public override string ToString() =>
        $"Enablement [enabled={Enabled}, startDate={StartDate}, endDate={EndDate}]";
}

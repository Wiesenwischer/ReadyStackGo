namespace ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Base class for value objects implementing equality by value.
/// Based on Vaughn Vernon's IDDD implementation.
/// </summary>
public abstract class ValueObject : AssertionConcern
{
    /// <summary>
    /// Returns all components that constitute the identity of this value object.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is null) return false;
        if (GetType() != obj.GetType()) return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(17, (current, component) =>
                current * 23 + (component?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }
}

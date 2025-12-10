namespace ReadyStackGo.Domain.Catalog.Sources;


/// <summary>
/// Repository for managing stack sources.
/// </summary>
public interface IStackSourceRepository
{
    /// <summary>
    /// Gets a stack source by ID.
    /// </summary>
    Task<StackSource?> GetByIdAsync(StackSourceId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all stack sources.
    /// </summary>
    Task<IReadOnlyList<StackSource>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled stack sources.
    /// </summary>
    Task<IReadOnlyList<StackSource>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new stack source.
    /// </summary>
    Task AddAsync(StackSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing stack source.
    /// </summary>
    Task UpdateAsync(StackSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a stack source.
    /// </summary>
    Task RemoveAsync(StackSourceId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a stack source with the given ID exists.
    /// </summary>
    Task<bool> ExistsAsync(StackSourceId id, CancellationToken cancellationToken = default);
}

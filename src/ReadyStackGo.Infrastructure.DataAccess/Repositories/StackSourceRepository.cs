namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.StackManagement.Sources;

/// <summary>
/// SQLite implementation of IStackSourceRepository.
/// </summary>
public class StackSourceRepository : IStackSourceRepository
{
    private readonly ReadyStackGoDbContext _context;

    public StackSourceRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public async Task<StackSource?> GetByIdAsync(StackSourceId id, CancellationToken cancellationToken = default)
    {
        return await _context.StackSources
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<StackSource>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StackSources
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StackSource>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StackSources
            .Where(s => s.Enabled)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        await _context.StackSources.AddAsync(source, cancellationToken);
    }

    public Task UpdateAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        _context.StackSources.Update(source);
        return Task.CompletedTask;
    }

    public async Task RemoveAsync(StackSourceId id, CancellationToken cancellationToken = default)
    {
        var source = await GetByIdAsync(id, cancellationToken);
        if (source != null)
        {
            _context.StackSources.Remove(source);
        }
    }

    public async Task<bool> ExistsAsync(StackSourceId id, CancellationToken cancellationToken = default)
    {
        return await _context.StackSources
            .AnyAsync(s => s.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

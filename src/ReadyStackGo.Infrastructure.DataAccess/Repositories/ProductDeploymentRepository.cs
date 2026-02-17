namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// SQLite implementation of IProductDeploymentRepository.
/// </summary>
public class ProductDeploymentRepository : IProductDeploymentRepository
{
    private readonly ReadyStackGoDbContext _context;

    public ProductDeploymentRepository(ReadyStackGoDbContext context)
    {
        _context = context;
    }

    public ProductDeploymentId NextIdentity()
    {
        return ProductDeploymentId.Create();
    }

    public void Add(ProductDeployment productDeployment)
    {
        _context.ProductDeployments.Add(productDeployment);
    }

    public void Update(ProductDeployment productDeployment)
    {
        _context.ProductDeployments.Update(productDeployment);
    }

    public ProductDeployment? Get(ProductDeploymentId id)
    {
        // Owned entities (Stacks) are loaded automatically by EF Core
        return _context.ProductDeployments
            .FirstOrDefault(d => d.Id == id);
    }

    public ProductDeployment? GetActiveByProductGroupId(EnvironmentId environmentId, string productGroupId)
    {
        return _context.ProductDeployments
            .Where(d => d.EnvironmentId == environmentId && d.ProductGroupId == productGroupId)
            .Where(d => d.Status != ProductDeploymentStatus.Removed)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefault();
    }

    public IEnumerable<ProductDeployment> GetByEnvironment(EnvironmentId environmentId)
    {
        return _context.ProductDeployments
            .Where(d => d.EnvironmentId == environmentId)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public IEnumerable<ProductDeployment> GetAllActive()
    {
        return _context.ProductDeployments
            .Where(d => d.Status == ProductDeploymentStatus.Running ||
                        d.Status == ProductDeploymentStatus.PartiallyRunning ||
                        d.Status == ProductDeploymentStatus.Deploying ||
                        d.Status == ProductDeploymentStatus.Upgrading)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}

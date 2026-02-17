namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Repository interface for ProductDeployment aggregate persistence.
/// </summary>
public interface IProductDeploymentRepository
{
    ProductDeploymentId NextIdentity();
    void Add(ProductDeployment productDeployment);
    void Update(ProductDeployment productDeployment);
    ProductDeployment? Get(ProductDeploymentId id);
    ProductDeployment? GetActiveByProductGroupId(EnvironmentId environmentId, string productGroupId);
    IEnumerable<ProductDeployment> GetByEnvironment(EnvironmentId environmentId);
    IEnumerable<ProductDeployment> GetAllActive();
    void SaveChanges();
}

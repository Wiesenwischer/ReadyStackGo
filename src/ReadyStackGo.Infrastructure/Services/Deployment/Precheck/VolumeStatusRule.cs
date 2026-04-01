using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

/// <summary>
/// Checks the status of named volumes — warns if volumes already exist (upgrade vs. fresh install).
/// </summary>
public class VolumeStatusRule : IDeploymentPrecheckRule
{
    public Task<IReadOnlyList<PrecheckItem>> ExecuteAsync(PrecheckContext context, CancellationToken cancellationToken)
    {
        var items = new List<PrecheckItem>();

        // Collect named volume definitions from services
        var namedVolumes = context.StackDefinition.Services
            .SelectMany(s => s.Volumes)
            .Where(v => v.Type == "volume" && !string.IsNullOrEmpty(v.Source))
            .Select(v => v.Source)
            .Distinct()
            .ToList();

        // Also include top-level volume definitions
        foreach (var volumeDef in context.StackDefinition.Volumes)
        {
            if (!namedVolumes.Contains(volumeDef.Name))
                namedVolumes.Add(volumeDef.Name);
        }

        if (namedVolumes.Count == 0)
        {
            items.Add(new PrecheckItem(
                "VolumeStatus",
                PrecheckSeverity.OK,
                "No named volumes required",
                "Stack does not define any named volumes"));
            return Task.FromResult<IReadOnlyList<PrecheckItem>>(items);
        }

        var existingVolumeNames = context.ExistingVolumes
            .Select(v => v.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var volumeName in namedVolumes)
        {
            // Volumes may be prefixed with stack name during deployment
            var prefixedName = $"{context.StackName}_{volumeName}";
            var exists = existingVolumeNames.Contains(volumeName) ||
                         existingVolumeNames.Contains(prefixedName);

            if (exists)
            {
                if (context.ExistingDeployment != null)
                {
                    items.Add(new PrecheckItem(
                        "VolumeStatus",
                        PrecheckSeverity.OK,
                        $"Volume exists (upgrade): {volumeName}",
                        "Existing data will be preserved during upgrade"));
                }
                else
                {
                    items.Add(new PrecheckItem(
                        "VolumeStatus",
                        PrecheckSeverity.Warning,
                        $"Volume already exists: {volumeName}",
                        "Volume contains data from a previous installation. Data will be reused."));
                }
            }
            else
            {
                items.Add(new PrecheckItem(
                    "VolumeStatus",
                    PrecheckSeverity.OK,
                    $"Volume will be created: {volumeName}",
                    "New volume will be created during deployment"));
            }
        }

        return Task.FromResult<IReadOnlyList<PrecheckItem>>(items);
    }
}

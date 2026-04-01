using ReadyStackGo.Domain.Deployment.Precheck;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.Deployments.Precheck.Rules;

/// <summary>
/// Validates that all required variables are provided and match their constraints.
/// </summary>
public class VariableValidationRule : IDeploymentPrecheckRule
{
    public Task<IReadOnlyList<PrecheckItem>> ExecuteAsync(PrecheckContext context, CancellationToken cancellationToken)
    {
        var items = new List<PrecheckItem>();
        var resolver = new StackVariableResolver();
        var result = resolver.Resolve(context.StackDefinition, new Dictionary<string, string>(context.Variables));

        if (result.IsSuccess)
        {
            items.Add(new PrecheckItem(
                "VariableValidation",
                PrecheckSeverity.OK,
                "All variables valid",
                $"{context.StackDefinition.Variables.Count} variable(s) validated"));
        }
        else
        {
            foreach (var error in result.Errors)
            {
                items.Add(new PrecheckItem(
                    "VariableValidation",
                    PrecheckSeverity.Error,
                    error.Type == VariableResolutionErrorType.RequiredVariableMissing
                        ? $"Required variable '{error.VariableName}' is missing"
                        : $"Variable '{error.VariableName}' validation failed",
                    error.Message));
            }
        }

        return Task.FromResult<IReadOnlyList<PrecheckItem>>(items);
    }
}

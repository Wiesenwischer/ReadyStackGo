namespace ReadyStackGo.Application.UseCases.PrtgConnections;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Sets ad-hoc per-deployment PRTG credentials (Variant 2). When
/// <c>Url</c> is null/empty the inline registration is cleared instead.
/// Clears any saved-connection link as a side effect — only one PRTG target
/// is active at a time.
/// </summary>
public sealed record SetInlinePrtgRegistrationCommand(
    Guid ProductDeploymentId,
    string? Url,
    string? ApiToken,
    int? TemplateDeviceId,
    bool VerifyTls) : IRequest<PrtgConnectionResponse>;

public sealed class SetInlinePrtgRegistrationHandler
    : IRequestHandler<SetInlinePrtgRegistrationCommand, PrtgConnectionResponse>
{
    private readonly IProductDeploymentRepository _deployments;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<SetInlinePrtgRegistrationHandler> _logger;

    public SetInlinePrtgRegistrationHandler(
        IProductDeploymentRepository deployments,
        ICredentialEncryptionService encryption,
        ILogger<SetInlinePrtgRegistrationHandler> logger)
    {
        _deployments = deployments;
        _encryption = encryption;
        _logger = logger;
    }

    public Task<PrtgConnectionResponse> Handle(SetInlinePrtgRegistrationCommand cmd, CancellationToken ct)
    {
        var deployment = _deployments.Get(ProductDeploymentId.FromGuid(cmd.ProductDeploymentId));
        if (deployment is null)
            return Task.FromResult(new PrtgConnectionResponse(false, "ProductDeployment not found."));

        if (string.IsNullOrWhiteSpace(cmd.Url))
        {
            deployment.ClearInlinePrtgRegistration();
            _deployments.Update(deployment);
            _deployments.SaveChanges();
            _logger.LogInformation("Cleared inline PRTG registration on ProductDeployment {Id}", deployment.Id);
            return Task.FromResult(new PrtgConnectionResponse(true));
        }

        if (string.IsNullOrWhiteSpace(cmd.ApiToken))
            return Task.FromResult(new PrtgConnectionResponse(false, "API token is required when URL is set."));

        var encrypted = _encryption.Encrypt(cmd.ApiToken);
        deployment.SetInlinePrtgRegistration(cmd.Url, encrypted, cmd.TemplateDeviceId, cmd.VerifyTls);
        _deployments.Update(deployment);
        _deployments.SaveChanges();

        _logger.LogInformation("Set inline PRTG registration on ProductDeployment {Id} (URL={Url})",
            deployment.Id, cmd.Url);
        return Task.FromResult(new PrtgConnectionResponse(true));
    }
}

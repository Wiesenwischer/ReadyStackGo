using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.System.UpdateTlsConfig;

public class UpdateTlsConfigHandler : IRequestHandler<UpdateTlsConfigCommand, UpdateTlsConfigResponse>
{
    private readonly ITlsConfigService _tlsConfigService;
    private readonly ILogger<UpdateTlsConfigHandler> _logger;

    public UpdateTlsConfigHandler(
        ITlsConfigService tlsConfigService,
        ILogger<UpdateTlsConfigHandler> logger)
    {
        _tlsConfigService = tlsConfigService;
        _logger = logger;
    }

    public async Task<UpdateTlsConfigResponse> Handle(UpdateTlsConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Handle reset to self-signed
            if (request.ResetToSelfSigned)
            {
                _logger.LogInformation("Resetting TLS to self-signed certificate");
                var resetResult = await _tlsConfigService.ResetToSelfSignedAsync();
                return new UpdateTlsConfigResponse
                {
                    Success = resetResult.Success,
                    Message = resetResult.Message,
                    RequiresRestart = resetResult.RequiresRestart
                };
            }

            // Handle PFX certificate upload
            if (!string.IsNullOrEmpty(request.PfxBase64))
            {
                _logger.LogInformation("Uploading PFX certificate");

                byte[] pfxData;
                try
                {
                    pfxData = Convert.FromBase64String(request.PfxBase64);
                }
                catch (FormatException)
                {
                    return new UpdateTlsConfigResponse
                    {
                        Success = false,
                        Message = "Invalid Base64 encoding for PFX data"
                    };
                }

                var result = await _tlsConfigService.UploadPfxCertificateAsync(
                    pfxData, request.PfxPassword ?? string.Empty);

                return new UpdateTlsConfigResponse
                {
                    Success = result.Success,
                    Message = result.Message,
                    RequiresRestart = result.RequiresRestart
                };
            }

            // Handle PEM certificate upload
            if (!string.IsNullOrEmpty(request.CertificatePem) && !string.IsNullOrEmpty(request.PrivateKeyPem))
            {
                _logger.LogInformation("Uploading PEM certificate");

                var result = await _tlsConfigService.UploadPemCertificateAsync(
                    request.CertificatePem, request.PrivateKeyPem);

                return new UpdateTlsConfigResponse
                {
                    Success = result.Success,
                    Message = result.Message,
                    RequiresRestart = result.RequiresRestart
                };
            }

            // Handle HTTP toggle only
            if (request.HttpEnabled.HasValue)
            {
                _logger.LogInformation("Setting HTTP enabled to {Enabled}", request.HttpEnabled.Value);

                var result = await _tlsConfigService.SetHttpEnabledAsync(request.HttpEnabled.Value);

                return new UpdateTlsConfigResponse
                {
                    Success = result.Success,
                    Message = result.Message,
                    RequiresRestart = result.RequiresRestart
                };
            }

            return new UpdateTlsConfigResponse
            {
                Success = false,
                Message = "No valid TLS configuration update specified"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update TLS configuration");
            return new UpdateTlsConfigResponse
            {
                Success = false,
                Message = $"Failed to update TLS configuration: {ex.Message}"
            };
        }
    }
}

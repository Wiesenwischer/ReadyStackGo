using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.System.GetTlsConfig;

public class GetTlsConfigHandler : IRequestHandler<GetTlsConfigQuery, GetTlsConfigResponse>
{
    private readonly ITlsConfigService _tlsConfigService;

    public GetTlsConfigHandler(ITlsConfigService tlsConfigService)
    {
        _tlsConfigService = tlsConfigService;
    }

    public async Task<GetTlsConfigResponse> Handle(GetTlsConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _tlsConfigService.GetTlsConfigAsync();

        return new GetTlsConfigResponse
        {
            Mode = config.Mode,
            HttpEnabled = config.HttpEnabled,
            CertificateInfo = config.CertificateInfo != null
                ? new CertificateInfoDto
                {
                    Subject = config.CertificateInfo.Subject,
                    Issuer = config.CertificateInfo.Issuer,
                    ExpiresAt = config.CertificateInfo.ExpiresAt,
                    Thumbprint = config.CertificateInfo.Thumbprint,
                    IsSelfSigned = config.CertificateInfo.IsSelfSigned,
                    IsExpired = config.CertificateInfo.IsExpired,
                    IsExpiringSoon = config.CertificateInfo.IsExpiringSoon
                }
                : null
        };
    }
}

using MediatR;

namespace ReadyStackGo.Application.UseCases.System.GetTlsConfig;

public record GetTlsConfigQuery() : IRequest<GetTlsConfigResponse>;

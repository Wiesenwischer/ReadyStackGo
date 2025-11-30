using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.ParseCompose;

public record ParseComposeCommand(string YamlContent) : IRequest<ParseComposeResponse>;

namespace ReadyStackGo.Application.UseCases.Organizations.ProvisionOrganization;

using MediatR;

public record ProvisionOrganizationCommand(
    string Name,
    string Description) : IRequest<ProvisionOrganizationResult>;

public record ProvisionOrganizationResult(
    bool Success,
    string? OrganizationId = null,
    string? ErrorMessage = null);

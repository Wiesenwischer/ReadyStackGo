namespace ReadyStackGo.Application.UseCases.Administration.RegisterSystemAdmin;

using MediatR;

public record RegisterSystemAdminCommand(
    string Username,
    string Password) : IRequest<RegisterSystemAdminResult>;

public record RegisterSystemAdminResult(
    bool Success,
    string? UserId = null,
    string? ErrorMessage = null);

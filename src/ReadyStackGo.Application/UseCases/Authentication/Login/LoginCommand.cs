using MediatR;

namespace ReadyStackGo.Application.UseCases.Authentication.Login;

public record LoginCommand(string Username, string Password) : IRequest<LoginResult>;

public record LoginResult(bool Success, string? Token, string? Username, string? Role, string? ErrorMessage);

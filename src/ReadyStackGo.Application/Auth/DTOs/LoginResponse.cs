namespace ReadyStackGo.Application.Auth.DTOs;

public record LoginResponse(
    string Token,
    string Username,
    string Role
);

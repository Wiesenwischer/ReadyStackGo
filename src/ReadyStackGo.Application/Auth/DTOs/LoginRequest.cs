namespace ReadyStackGo.Application.Auth.DTOs;

public record LoginRequest(
    string Username,
    string Password
);

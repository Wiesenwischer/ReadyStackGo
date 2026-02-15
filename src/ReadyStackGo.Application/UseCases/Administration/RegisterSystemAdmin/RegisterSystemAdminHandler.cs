namespace ReadyStackGo.Application.UseCases.Administration.RegisterSystemAdmin;

using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

public class RegisterSystemAdminHandler : IRequestHandler<RegisterSystemAdminCommand, RegisterSystemAdminResult>
{
    private readonly SystemAdminRegistrationService _registrationService;
    private readonly ITokenService _tokenService;

    public RegisterSystemAdminHandler(
        SystemAdminRegistrationService registrationService,
        ITokenService tokenService)
    {
        _registrationService = registrationService;
        _tokenService = tokenService;
    }

    public Task<RegisterSystemAdminResult> Handle(RegisterSystemAdminCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = _registrationService.RegisterSystemAdmin(request.Username, request.Password);
            var token = _tokenService.GenerateToken(user);
            var role = user.HasRole(RoleId.SystemAdmin) ? "admin" : "user";

            return Task.FromResult(new RegisterSystemAdminResult(
                true, user.Id.ToString(), Token: token, Username: user.Username, Role: role));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(new RegisterSystemAdminResult(false, ErrorMessage: ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(new RegisterSystemAdminResult(false, ErrorMessage: ex.Message));
        }
    }
}

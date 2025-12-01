namespace ReadyStackGo.Application.UseCases.Administration.RegisterSystemAdmin;

using MediatR;
using ReadyStackGo.Domain.IdentityAccess.Services;

public class RegisterSystemAdminHandler : IRequestHandler<RegisterSystemAdminCommand, RegisterSystemAdminResult>
{
    private readonly SystemAdminRegistrationService _registrationService;

    public RegisterSystemAdminHandler(SystemAdminRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    public Task<RegisterSystemAdminResult> Handle(RegisterSystemAdminCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = _registrationService.RegisterSystemAdmin(request.Username, request.Password);
            return Task.FromResult(new RegisterSystemAdminResult(true, user.Id.ToString()));
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

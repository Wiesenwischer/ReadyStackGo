using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.TestConnection;

public class TestSshConnectionHandler : IRequestHandler<TestSshConnectionCommand, TestConnectionResult>
{
    private readonly ISshConnectionTester _sshConnectionTester;

    public TestSshConnectionHandler(ISshConnectionTester sshConnectionTester)
    {
        _sshConnectionTester = sshConnectionTester;
    }

    public async Task<TestConnectionResult> Handle(TestSshConnectionCommand request, CancellationToken cancellationToken)
    {
        var authMethod = request.AuthMethod?.ToLowerInvariant() switch
        {
            "password" => Domain.Deployment.Environments.SshAuthMethod.Password,
            _ => Domain.Deployment.Environments.SshAuthMethod.PrivateKey
        };

        return await _sshConnectionTester.TestConnectionAsync(
            request.Host,
            request.Port,
            request.Username,
            request.Secret,
            authMethod,
            request.RemoteSocketPath,
            cancellationToken);
    }
}

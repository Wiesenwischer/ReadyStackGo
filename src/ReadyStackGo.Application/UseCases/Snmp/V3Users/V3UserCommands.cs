using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Application.UseCases.Snmp.V3Users;

public record ListV3UsersQuery() : IRequest<IReadOnlyList<V3UserDto>>;

public record V3UserDto(
    Guid Id,
    string Name,
    SnmpAuthProtocol AuthProtocol,
    SnmpPrivProtocol PrivProtocol,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class ListV3UsersHandler : IRequestHandler<ListV3UsersQuery, IReadOnlyList<V3UserDto>>
{
    private readonly ISnmpV3UserRepository _users;

    public ListV3UsersHandler(ISnmpV3UserRepository users) => _users = users;

    public Task<IReadOnlyList<V3UserDto>> Handle(ListV3UsersQuery request, CancellationToken cancellationToken)
    {
        var result = _users.GetAll()
            .Select(u => new V3UserDto(u.Id, u.Name, u.AuthProtocol, u.PrivProtocol, u.CreatedAt, u.UpdatedAt))
            .ToList();
        return Task.FromResult<IReadOnlyList<V3UserDto>>(result);
    }
}

public record AddV3UserCommand(
    string Name,
    SnmpAuthProtocol AuthProtocol,
    string AuthPassphrase,
    SnmpPrivProtocol PrivProtocol,
    string PrivPassphrase) : IRequest<AddV3UserResult>;

public record AddV3UserResult(bool Success, Guid? UserId, string? ErrorMessage);

public sealed class AddV3UserHandler : IRequestHandler<AddV3UserCommand, AddV3UserResult>
{
    private readonly ISnmpV3UserRepository _users;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<AddV3UserHandler> _logger;

    public AddV3UserHandler(
        ISnmpV3UserRepository users,
        ICredentialEncryptionService encryption,
        ILogger<AddV3UserHandler> logger)
    {
        _users = users;
        _encryption = encryption;
        _logger = logger;
    }

    public Task<AddV3UserResult> Handle(AddV3UserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (_users.GetByName(request.Name) is not null)
                return Task.FromResult(new AddV3UserResult(false, null, $"User '{request.Name}' already exists."));

            var authEnc = request.AuthProtocol == SnmpAuthProtocol.None
                ? string.Empty
                : _encryption.Encrypt(request.AuthPassphrase ?? string.Empty);
            var privEnc = request.PrivProtocol == SnmpPrivProtocol.None
                ? string.Empty
                : _encryption.Encrypt(request.PrivPassphrase ?? string.Empty);

            var user = SnmpV3User.Create(
                request.Name, request.AuthProtocol, authEnc,
                request.PrivProtocol, privEnc);

            _users.Add(user);
            _users.SaveChanges();
            _logger.LogInformation("Added SNMPv3 user '{Name}'", request.Name);
            return Task.FromResult(new AddV3UserResult(true, user.Id, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add SNMPv3 user");
            return Task.FromResult(new AddV3UserResult(false, null, ex.Message));
        }
    }
}

public record UpdateV3UserCommand(
    Guid Id,
    SnmpAuthProtocol AuthProtocol,
    string? AuthPassphrase,
    SnmpPrivProtocol PrivProtocol,
    string? PrivPassphrase) : IRequest<UpdateV3UserResult>;

public record UpdateV3UserResult(bool Success, string? ErrorMessage);

public sealed class UpdateV3UserHandler : IRequestHandler<UpdateV3UserCommand, UpdateV3UserResult>
{
    private readonly ISnmpV3UserRepository _users;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<UpdateV3UserHandler> _logger;

    public UpdateV3UserHandler(
        ISnmpV3UserRepository users,
        ICredentialEncryptionService encryption,
        ILogger<UpdateV3UserHandler> logger)
    {
        _users = users;
        _encryption = encryption;
        _logger = logger;
    }

    public Task<UpdateV3UserResult> Handle(UpdateV3UserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = _users.GetById(request.Id);
            if (user is null) return Task.FromResult(new UpdateV3UserResult(false, "User not found."));

            string? authEnc = request.AuthPassphrase is null ? null : _encryption.Encrypt(request.AuthPassphrase);
            string? privEnc = request.PrivPassphrase is null ? null : _encryption.Encrypt(request.PrivPassphrase);
            user.Update(request.AuthProtocol, authEnc, request.PrivProtocol, privEnc);

            _users.Update(user);
            _users.SaveChanges();
            _logger.LogInformation("Updated SNMPv3 user '{Name}'", user.Name);
            return Task.FromResult(new UpdateV3UserResult(true, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SNMPv3 user");
            return Task.FromResult(new UpdateV3UserResult(false, ex.Message));
        }
    }
}

public record DeleteV3UserCommand(Guid Id) : IRequest<DeleteV3UserResult>;

public record DeleteV3UserResult(bool Success, string? ErrorMessage);

public sealed class DeleteV3UserHandler : IRequestHandler<DeleteV3UserCommand, DeleteV3UserResult>
{
    private readonly ISnmpV3UserRepository _users;
    private readonly ILogger<DeleteV3UserHandler> _logger;

    public DeleteV3UserHandler(ISnmpV3UserRepository users, ILogger<DeleteV3UserHandler> logger)
    {
        _users = users;
        _logger = logger;
    }

    public Task<DeleteV3UserResult> Handle(DeleteV3UserCommand request, CancellationToken cancellationToken)
    {
        var user = _users.GetById(request.Id);
        if (user is null) return Task.FromResult(new DeleteV3UserResult(false, "User not found."));

        _users.Remove(user);
        _users.SaveChanges();
        _logger.LogInformation("Removed SNMPv3 user '{Name}'", user.Name);
        return Task.FromResult(new DeleteV3UserResult(true, null));
    }
}

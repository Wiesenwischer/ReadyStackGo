# Initial Setup Security

This document describes the security concept during the initial setup of ReadyStackGo.

## Overview

During the first installation, ReadyStackGo is not yet configured and has no admin user. The wizard endpoints must therefore be accessible without authentication to enable initial setup.

## Current Security Mechanism (v0.4)

### State-based Validation

The wizard goes through defined states that prevent steps from being skipped or repeated:

| State | Description | Allowed Action |
|-------|-------------|----------------|
| `NotStarted` | No configuration present | Create admin |
| `AdminCreated` | Admin exists but no organization | Set organization |
| `OrganizationSet` | Organization configured | Complete wizard |
| `Installed` | Wizard completed | No wizard actions |

### Endpoint Protection

- **During wizard**: Endpoints are anonymously accessible (`AllowAnonymous`)
- **After completion**: Wizard endpoints return errors since the state no longer matches
- **Normal API**: Requires JWT authentication

### Limitations

- No time window limit for initial setup
- Theoretically, an attacker could create the first admin in an unsecured network environment
- No IP binding or setup token mechanism

## Recommended Best Practices (v0.4)

### Network Security

1. **Local installation**: Initially make ReadyStackGo only accessible locally (localhost/127.0.0.1)
2. **VPN/Firewall**: For remote installation, ensure only authorized clients have access
3. **Quick setup**: Complete wizard immediately after installation

### Docker Deployment

```bash
# Option 1: Bind only to localhost during setup
docker run -p 127.0.0.1:5259:5259 readystackgo

# Option 2: With VPN/firewall protected network
docker run -p 5259:5259 readystackgo
# Ensure port 5259 is only reachable from trusted networks
```

## Comparison with Portainer

| Feature | ReadyStackGo (v0.4) | Portainer |
|---------|---------------------|-----------|
| Anonymous wizard endpoints | Yes | Yes |
| State validation | Yes | Yes |
| Time window limit | No | Yes (5 minutes) |
| API lockdown after timeout | No | Yes |
| Setup token | No | No |

## Planned Improvements (v0.5+)

### Wizard Timeout (planned for v0.5)

- **5-minute time window** for admin creation after server start
- After expiration: API blocking until restart
- Status endpoint remains accessible for timeout display

### Other Planned Features

- **IP Whitelist**: Only certain IPs can access during setup
- **Secure Restart**: Ability to extend time window through authenticated restart
- **Audit Logging**: Logging of all setup attempts

## Technical Details

### Wizard Endpoints

```
POST /api/wizard/admin        - Create admin user
POST /api/wizard/organization - Set organization
POST /api/wizard/install      - Complete wizard
GET  /api/wizard/status       - Query wizard status
```

### State Checking

```csharp
// Example: Admin can only be created when State = NotStarted
if (systemConfig.WizardState != WizardState.NotStarted)
{
    return new CreateAdminResponse
    {
        Success = false,
        Message = "Admin can only be created when wizard is NotStarted."
    };
}
```

## Security Recommendations

1. **Immediate setup**: Complete wizard right after installation
2. **Network isolation**: Restrict access during setup
3. **Enable TLS**: Configure HTTPS after setup
4. **Regular updates**: Keep ReadyStackGo up to date for security patches

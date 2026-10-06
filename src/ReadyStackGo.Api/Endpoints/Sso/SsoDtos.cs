using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.API.Endpoints.Sso;

public class IdentityProviderTemplateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SetupDescription { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public bool HasFixedAuthority { get; set; }
    public string? AuthorityUrl { get; set; }
    public string? AuthorityHint { get; set; }
    public string? AuthorityExample { get; set; }
    public string RegistrationKind { get; set; } = string.Empty;

    /// <summary>"manualEntry" or "connect".</summary>
    public string Interaction { get; set; } = string.Empty;
    public bool RequireHttps { get; set; }
    public string? HelpUrl { get; set; }

    public static IdentityProviderTemplateDto From(IdentityProviderTemplate t, IEnumerable<IClientRegistrationMethod> methods) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        SetupDescription = t.SetupDescription,
        IconUrl = t.HasIcon ? $"/api/identity-provider-templates/{t.Id}/icon" : null,
        HasFixedAuthority = t.HasFixedAuthority,
        AuthorityUrl = t.AuthorityUrl,
        AuthorityHint = t.AuthorityInput?.Hint,
        AuthorityExample = t.AuthorityInput?.Example,
        RegistrationKind = t.RegistrationKind,
        Interaction = InteractionName(methods.FirstOrDefault(m => m.Kind == t.RegistrationKind)?.Interaction),
        RequireHttps = t.RequireHttps,
        HelpUrl = t.HelpUrl
    };

    public static string InteractionName(RegistrationInteraction? interaction) =>
        interaction == RegistrationInteraction.Connect ? "connect" : "manualEntry";
}

public class CheckItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Code { get; set; }
}

public class CheckReportDto
{
    public List<CheckItemDto> Items { get; set; } = new();
    public string? Issuer { get; set; }
    public int ExecutedCount { get; set; }
    public int FailedCount { get; set; }
    public bool Passed { get; set; }

    /// <summary>False when the connection changed after the checks ran.</summary>
    public bool Current { get; set; } = true;

    public static CheckReportDto? From(OidcCheckReport? report, bool current = true) => report == null
        ? null
        : new CheckReportDto
        {
            Items = report.Items.Select(i => new CheckItemDto { Id = i.Id, Status = i.Status, Title = i.Title, Detail = i.Detail, Code = i.Code }).ToList(),
            Issuer = report.Issuer,
            ExecutedCount = report.ExecutedCount,
            FailedCount = report.FailedCount,
            Passed = report.Passed,
            Current = current
        };
}

public class TestSignInDto
{
    public bool Passed { get; set; }
    public bool Current { get; set; }
    public DateTime At { get; set; }
    public string? SignedInAs { get; set; }
    public List<TestSignInClaim> Claims { get; set; } = new();
    public string? WarningTitle { get; set; }
    public string? WarningBody { get; set; }
    public string? Error { get; set; }
    public string? ErrorDetail { get; set; }
}

public class SetupSessionDto
{
    public string Id { get; set; } = string.Empty;
    public bool IsNew { get; set; }
    public string? ExistingProvider { get; set; }
    public IdentityProviderTemplateDto? Template { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string RegistrationKind { get; set; } = string.Empty;
    public string Interaction { get; set; } = string.Empty;
    public bool RequireHttps { get; set; }
    public string? Authority { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }

    /// <summary>True if the base URL of the installation was set before this session.</summary>
    public bool BaseUrlConfigured { get; set; }
    public string? RedirectUri { get; set; }
    public string? ClientId { get; set; }
    public bool HasClientSecret { get; set; }
    public DateTime? PairedAt { get; set; }
    public string? PairedBy { get; set; }
    public string? RegistrationError { get; set; }
    public string? RegistrationErrorDescription { get; set; }
    public bool RegistrationPending { get; set; }
    public CheckReportDto? Discovery { get; set; }
    public CheckReportDto? Checks { get; set; }
    public TestSignInDto? TestSignIn { get; set; }
    public bool TrustUnverifiedEmail { get; set; }

    /// <summary>Checks passed and test sign-in passed for the current connection.</summary>
    public bool CanEnable { get; set; }
}

public class RegistrationStartDto
{
    /// <summary>"formPost", "redirect" or "completed".</summary>
    public string Kind { get; set; } = string.Empty;
    public string? Url { get; set; }
    public Dictionary<string, string>? Fields { get; set; }

    public static RegistrationStartDto From(RegistrationStart start) => new()
    {
        Kind = start.Kind switch
        {
            RegistrationStartKind.FormPost => "formPost",
            RegistrationStartKind.Redirect => "redirect",
            _ => "completed"
        },
        Url = start.Url,
        Fields = start.Fields?.ToDictionary(kv => kv.Key, kv => kv.Value)
    };
}

public class SsoErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

internal static class SsoResponses
{
    public static Task SendErrorAsync(HttpContext http, SsoSetupException ex, CancellationToken ct) =>
        http.Response.SendAsync(new SsoErrorDto { Code = ex.Code, Message = ex.Message }, ex.StatusCode, cancellation: ct);

    public static Task SendErrorAsync(HttpContext http, string code, string message, int status, CancellationToken ct) =>
        http.Response.SendAsync(new SsoErrorDto { Code = code, Message = message }, status, cancellation: ct);
}

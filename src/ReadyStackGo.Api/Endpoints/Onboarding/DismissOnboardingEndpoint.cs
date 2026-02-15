using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.Onboarding;

/// <summary>
/// POST /api/onboarding/dismiss - Dismiss the onboarding checklist
/// </summary>
public class DismissOnboardingEndpoint : EndpointWithoutRequest
{
    private readonly IOnboardingStateService _onboardingStateService;

    public DismissOnboardingEndpoint(IOnboardingStateService onboardingStateService)
    {
        _onboardingStateService = onboardingStateService;
    }

    public override void Configure()
    {
        Post("/api/onboarding/dismiss");
        Description(b => b.WithTags("Onboarding"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _onboardingStateService.DismissAsync(ct);
        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}

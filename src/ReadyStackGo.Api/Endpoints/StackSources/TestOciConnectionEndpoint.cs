using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Infrastructure.Services.StackSources;

namespace ReadyStackGo.API.Endpoints.StackSources;

/// <summary>
/// POST /api/stack-sources/test-oci-connection - Test OCI registry connectivity.
/// Lists tags from the registry to verify credentials and access.
/// Accessible by: SystemAdmin only.
/// </summary>
[RequireSystemAdmin]
public class TestOciConnectionEndpoint : Endpoint<TestOciConnectionRequest, TestOciConnectionResponse>
{
    private readonly OciRegistryClient _registryClient;

    public TestOciConnectionEndpoint(OciRegistryClient registryClient)
    {
        _registryClient = registryClient;
    }

    public override void Configure()
    {
        Post("/api/stack-sources/test-oci-connection");
        PreProcessor<RbacPreProcessor<TestOciConnectionRequest>>();
    }

    public override async Task HandleAsync(TestOciConnectionRequest req, CancellationToken ct)
    {
        try
        {
            var tags = await _registryClient.ListTagsAsync(
                req.RegistryUrl, req.Repository, req.Username, req.Password, ct);

            Response = new TestOciConnectionResponse
            {
                Success = true,
                Message = $"Connection successful. Found {tags.Count} tag(s).",
                TagCount = tags.Count,
                SampleTags = tags.Take(10).ToList()
            };
        }
        catch (Exception ex)
        {
            Response = new TestOciConnectionResponse
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}"
            };
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}

public class TestOciConnectionRequest
{
    public string RegistryUrl { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class TestOciConnectionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int TagCount { get; set; }
    public List<string>? SampleTags { get; set; }
}

using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using Microsoft.Net.Http.Headers;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Themes;

/// <summary>
/// GET /api/themes/{id}/theme.css - Stylesheet of an offered theme package.
/// Anonymous access. Served with Cache-Control: no-cache and an ETag, so browsers revalidate
/// and get 304 Not Modified while the file is unchanged.
/// </summary>
public class GetThemeCssEndpoint : EndpointWithoutRequest
{
    private readonly IThemeCatalog _themeCatalog;

    public GetThemeCssEndpoint(IThemeCatalog themeCatalog)
    {
        _themeCatalog = themeCatalog;
    }

    public override void Configure()
    {
        Get("/api/themes/{id}/theme.css");
        AllowAnonymous();
        Description(b => b
            .WithTags("Themes")
            .WithSummary("Get theme stylesheet")
            .WithDescription("Returns theme.css of an offered theme package."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("id", isRequired: false);

        // The catalog validates the id against the theme id pattern before any file access.
        var css = ThemeId.IsValid(id) ? _themeCatalog.GetThemeCss(id) : null;
        if (css is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(css);
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(SHA256.HashData(bytes))[..32]}\"");

        var response = HttpContext.Response;
        response.Headers.CacheControl = "no-cache";
        response.Headers.ETag = etag.ToString();

        var ifNoneMatch = HttpContext.Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(etag, useStrongComparison: false)))
        {
            await Send.ResultAsync(Results.StatusCode(StatusCodes.Status304NotModified));
            return;
        }

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/css; charset=utf-8";
        response.ContentLength = bytes.Length;
        await response.Body.WriteAsync(bytes, ct);
    }
}

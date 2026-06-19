using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Infrastructure.Services.Health;

namespace ReadyStackGo.UnitTests.Infrastructure.Maintenance;

public class WebhookSetterTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _responses;
        public int Calls { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();

        public StubHandler(params HttpStatusCode[] responses) => _responses = new Queue<HttpStatusCode>(responses);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Requests.Add(request);
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            var status = _responses.Count > 0 ? _responses.Dequeue() : HttpStatusCode.InternalServerError;
            return new HttpResponseMessage(status);
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static WebhookSetter CreateSut(StubHandler handler, string? secret = null, int maxRetries = 2)
    {
        var settings = new WebhookSetterSettings("https://product.example.com/maintenance", secret, TimeSpan.FromSeconds(5), maxRetries);
        var config = MaintenanceSetterConfig.Create(SetterType.Webhook, TimeSpan.Zero, "maintenance", "normal", settings);
        return new WebhookSetter(config, new StubFactory(handler), new Mock<ILogger<WebhookSetter>>().Object);
    }

    [Fact]
    public async Task SetAsync_Success_ReturnsOk_OneCall()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var sut = CreateSut(handler);

        var result = await sut.SetAsync(MaintenanceState.Maintenance);

        result.Success.Should().BeTrue();
        handler.Calls.Should().Be(1);
        handler.Bodies[0].Should().Contain("\"state\":\"maintenance\"");
    }

    [Fact]
    public async Task SetAsync_RetriesThenSucceeds()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        var sut = CreateSut(handler, maxRetries: 2);

        var result = await sut.SetAsync(MaintenanceState.Normal);

        result.Success.Should().BeTrue();
        handler.Calls.Should().Be(2);
        handler.Bodies[0].Should().Contain("\"state\":\"normal\"");
    }

    [Fact]
    public async Task SetAsync_AllAttemptsFail_ReturnsFailure_NonFatal()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError);
        var sut = CreateSut(handler, maxRetries: 1);

        var result = await sut.SetAsync(MaintenanceState.Maintenance);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        handler.Calls.Should().Be(2); // maxRetries(1) + 1
    }

    [Fact]
    public async Task SetAsync_WithSecret_AddsSignatureHeader()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var sut = CreateSut(handler, secret: "s3cr3t");

        await sut.SetAsync(MaintenanceState.Maintenance);

        var header = handler.Requests[0].Headers.TryGetValues(WebhookSignature.HeaderName, out var values)
            ? values.First() : null;
        header.Should().NotBeNull();
        header!.Should().Be(WebhookSignature.Compute("s3cr3t", handler.Bodies[0]));
    }

    [Fact]
    public async Task SetAsync_WithoutSecret_NoSignatureHeader()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var sut = CreateSut(handler, secret: null);

        await sut.SetAsync(MaintenanceState.Maintenance);

        handler.Requests[0].Headers.Contains(WebhookSignature.HeaderName).Should().BeFalse();
    }
}

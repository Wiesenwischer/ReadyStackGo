using FastEndpoints;
using Microsoft.Data.SqlClient;

namespace ReadyStackGo.API.Endpoints.Connections;

public class TestSqlServerConnectionRequest
{
    public string ConnectionString { get; set; } = null!;
}

public class TestSqlServerConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public string? ServerVersion { get; set; }
}

/// <summary>
/// POST /api/connections/test/sqlserver - Test SQL Server connection.
/// Anonymous access to support connection testing in wizard and configuration.
/// </summary>
public class TestSqlServerConnectionEndpoint : Endpoint<TestSqlServerConnectionRequest, TestSqlServerConnectionResponse>
{
    public override void Configure()
    {
        Post("/api/connections/test/sqlserver");
        Description(b => b.WithTags("Connections"));
        AllowAnonymous();
    }

    public override async Task HandleAsync(TestSqlServerConnectionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionString))
        {
            Response = new TestSqlServerConnectionResponse
            {
                Success = false,
                Message = "Connection string is required"
            };
            return;
        }

        try
        {
            // Ensure connection timeout is reasonable for testing
            var builder = new SqlConnectionStringBuilder(req.ConnectionString);
            if (builder.ConnectTimeout > 30)
            {
                builder.ConnectTimeout = 30;
            }
            if (builder.ConnectTimeout < 5)
            {
                builder.ConnectTimeout = 5;
            }

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(ct);

            Response = new TestSqlServerConnectionResponse
            {
                Success = true,
                Message = "Connection successful",
                ServerVersion = connection.ServerVersion
            };
        }
        catch (SqlException ex)
        {
            Response = new TestSqlServerConnectionResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
        catch (Exception ex)
        {
            Response = new TestSqlServerConnectionResponse
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}"
            };
        }
    }
}

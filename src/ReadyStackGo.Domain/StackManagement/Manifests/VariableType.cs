namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Type of a stack variable for input validation and UI rendering.
/// </summary>
public enum VariableType
{
    /// <summary>
    /// Free-form text input (default).
    /// </summary>
    String = 0,

    /// <summary>
    /// Numeric input (integer or decimal).
    /// </summary>
    Number = 1,

    /// <summary>
    /// Boolean toggle (true/false).
    /// </summary>
    Boolean = 2,

    /// <summary>
    /// Selection from predefined options.
    /// </summary>
    Select = 3,

    /// <summary>
    /// Password input (masked in UI, never logged).
    /// </summary>
    Password = 4,

    /// <summary>
    /// Port number (validated range 1-65535).
    /// </summary>
    Port = 5,

    /// <summary>
    /// URL input (validated URL format).
    /// </summary>
    Url = 6,

    /// <summary>
    /// Email input (validated email format).
    /// </summary>
    Email = 7,

    /// <summary>
    /// File system path input.
    /// </summary>
    Path = 8,

    /// <summary>
    /// Multi-line text input (textarea).
    /// </summary>
    MultiLine = 9,

    /// <summary>
    /// Generic connection string (text input only, no builder).
    /// </summary>
    ConnectionString = 10,

    /// <summary>
    /// SQL Server connection string with builder dialog.
    /// </summary>
    SqlServerConnectionString = 11,

    /// <summary>
    /// PostgreSQL connection string with builder dialog.
    /// </summary>
    PostgresConnectionString = 12,

    /// <summary>
    /// MySQL connection string with builder dialog.
    /// </summary>
    MySqlConnectionString = 13,

    /// <summary>
    /// EventStoreDB connection string with builder dialog.
    /// </summary>
    EventStoreConnectionString = 14,

    /// <summary>
    /// MongoDB connection string with builder dialog.
    /// </summary>
    MongoConnectionString = 15,

    /// <summary>
    /// Redis connection string with builder dialog.
    /// </summary>
    RedisConnectionString = 16
}

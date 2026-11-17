namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Classifies variable types by whether their value is a secret.
///
/// A secret value must never be returned to a client: deployment detail views only learn *that* a
/// value is stored, never what it is. Connection strings count as secrets because they embed
/// credentials (`Password=…`), even though their type is not <see cref="VariableType.Password"/>.
/// </summary>
public static class VariableTypeSecrecy
{
    /// <summary>
    /// Whether values of this type must be withheld from API responses.
    /// </summary>
    public static bool IsSecret(this VariableType type) => type switch
    {
        VariableType.Password => true,
        VariableType.ConnectionString => true,
        VariableType.SqlServerConnectionString => true,
        VariableType.PostgresConnectionString => true,
        VariableType.MySqlConnectionString => true,
        VariableType.EventStoreConnectionString => true,
        VariableType.MongoConnectionString => true,
        VariableType.RedisConnectionString => true,
        _ => false
    };

    /// <summary>
    /// Fail-safe fallback for deployments created before the secret variable names were recorded
    /// (and for values whose definition can no longer be resolved, e.g. an offline source).
    /// Judges by name alone, deliberately erring towards withholding: showing a port in clear text
    /// is worth less than the risk of showing a password.
    /// </summary>
    public static bool NameSuggestsSecret(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return false;

        var name = variableName.Replace("_", string.Empty).Replace("-", string.Empty);

        return Markers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] Markers =
    [
        "PASSWORD",
        "PASSWD",
        "PWD",
        "SECRET",
        "TOKEN",
        "APIKEY",
        "ACCESSKEY",
        "PRIVATEKEY",
        "CREDENTIAL",
        "CONNECTIONSTRING",
        "CONNSTR"
    ];
}

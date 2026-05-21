using System.Text.Json.Serialization;

namespace ReadyStackGo.Application.Integrations.Prtg;

/// <summary>
/// PRTG's "HTTP Data Advanced" response envelope. A sensor of this type polls
/// a URL and expects exactly this JSON shape. PRTG renders one chart per
/// channel; rules in <see cref="PrtgChannel.LimitMaxError"/> + LimitMode put
/// the sensor into Error/Warning automatically without admin-side thresholds.
/// See: https://www.paessler.com/manuals/prtg/custom_sensors
/// </summary>
public sealed class PrtgJsonStatusResponse
{
    [JsonPropertyName("prtg")]
    public required PrtgResult Prtg { get; init; }

    [JsonPropertyName("error")]
    public int? Error { get; init; }

    [JsonPropertyName("error_text")]
    public string? ErrorText { get; init; }
}

public sealed class PrtgResult
{
    [JsonPropertyName("result")]
    public required IReadOnlyList<PrtgChannel> Result { get; init; }

    /// <summary>Optional free-form status text PRTG shows alongside the sensor.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

/// <summary>
/// One channel = one chart in PRTG. PRTG allows at most 50 channels per
/// sensor — keep the channel list lean.
/// </summary>
public sealed class PrtgChannel
{
    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    [JsonPropertyName("customunit")]
    public string? CustomUnit { get; init; }

    [JsonPropertyName("ValueLookup")]
    public string? ValueLookup { get; init; }

    [JsonPropertyName("limitmaxerror")]
    public double? LimitMaxError { get; init; }

    [JsonPropertyName("limitmaxwarning")]
    public double? LimitMaxWarning { get; init; }

    [JsonPropertyName("limitmode")]
    public int? LimitMode { get; init; }

    [JsonPropertyName("Float")]
    public int? Float { get; init; }
}

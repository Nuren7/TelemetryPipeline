namespace TelemetryPipeline.Models;

public sealed class ProcessedEvent : EventPayload
{
    public Guid LogId { get; set; }

    public DateTimeOffset ProcessedAtUtc { get; set; }

    public string SchemaVersion { get; set; } = "1.0";

    public bool? IsAnomaly { get; set; }

    public double? AnomalyZScore { get; set; }
}

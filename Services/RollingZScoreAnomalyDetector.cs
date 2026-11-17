using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using TelemetryPipeline.Models;

namespace TelemetryPipeline.Services;

public sealed class RollingZScoreAnomalyDetector
{
    private const int WindowSize = 30;
    private const int MinimumBaselineSamples = 5;
    private const double ZScoreThreshold = 3;

    private readonly ConcurrentDictionary<(string SourceSystem, string EventType), MetricWindow> windows = new();

    public AnomalyResult? Evaluate(EventPayload eventPayload)
    {
        if (eventPayload.PayloadData is not JObject payloadData
            || string.IsNullOrWhiteSpace(eventPayload.SourceSystem)
            || string.IsNullOrWhiteSpace(eventPayload.EventType))
        {
            return null;
        }

        JToken? metric = payloadData["value"];
        if (metric?.Type is not (JTokenType.Integer or JTokenType.Float))
        {
            return null;
        }

        double value = metric.Value<double>();
        if (!double.IsFinite(value))
        {
            return null;
        }

        MetricWindow window = windows.GetOrAdd(
            (eventPayload.SourceSystem!, eventPayload.EventType!),
            static _ => new MetricWindow());

        return window.Evaluate(value);
    }

    private sealed class MetricWindow
    {
        private readonly Queue<double> values = new();

        public AnomalyResult? Evaluate(double value)
        {
            lock (values)
            {
                AnomalyResult? result = null;
                if (values.Count >= MinimumBaselineSamples)
                {
                    double mean = values.Average();
                    double variance = values.Average(sample => Math.Pow(sample - mean, 2));
                    double standardDeviation = Math.Sqrt(variance);

                    if (standardDeviation == 0)
                    {
                        result = new AnomalyResult(value != mean, null);
                    }
                    else
                    {
                        double zScore = (value - mean) / standardDeviation;
                        result = new AnomalyResult(Math.Abs(zScore) >= ZScoreThreshold, zScore);
                    }
                }

                values.Enqueue(value);
                if (values.Count > WindowSize)
                {
                    values.Dequeue();
                }

                return result;
            }
        }
    }
}

public sealed record AnomalyResult(bool IsAnomaly, double? ZScore);

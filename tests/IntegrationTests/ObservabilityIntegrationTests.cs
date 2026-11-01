using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests;

public sealed class ObservabilityIntegrationTests
{
    [Fact]
    [Trait("Category", "Observability")]
    public async Task GrafanaStack_Should_Persist_Logs_Traces_And_Metrics()
    {
        if (Environment.GetEnvironmentVariable("RUN_OBSERVABILITY_TESTS") != "1")
        {
            return;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using HttpResponseMessage health = await client.GetAsync("http://localhost:3300/api/health", TestContext.Current.CancellationToken);
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var healthBody = JsonDocument.Parse(await health.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        healthBody.RootElement.GetProperty("database").GetString().ShouldBe("ok");

        string marker = $"observability-proof-{Guid.NewGuid():N}";
        string traceId = Guid.NewGuid().ToString("N");
        string timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000).ToString(CultureInfo.InvariantCulture);
        string service = "observability-smoke-test";

        var logs = new
        {
            resourceLogs = new[] { new
            {
                resource = new { attributes = new[] { new { key = "service.name", value = new { stringValue = service } } } },
                scopeLogs = new[] { new { logRecords = new[] { new { timeUnixNano = timestamp, severityText = "INFO", body = new { stringValue = marker } } } } }
            } }
        };
        using HttpResponseMessage logResponse = await client.PostAsJsonAsync("http://localhost:4318/v1/logs", logs, TestContext.Current.CancellationToken);
        logResponse.EnsureSuccessStatusCode();

        var traces = new
        {
            resourceSpans = new[] { new
            {
                resource = new { attributes = new[] { new { key = "service.name", value = new { stringValue = service } } } },
                scopeSpans = new[] { new { spans = new[] { new { traceId, spanId = "1234567890abcdef", name = marker, kind = 1, startTimeUnixNano = timestamp, endTimeUnixNano = timestamp } } } }
            } }
        };
        using HttpResponseMessage traceResponse = await client.PostAsJsonAsync("http://localhost:4318/v1/traces", traces, TestContext.Current.CancellationToken);
        traceResponse.EnsureSuccessStatusCode();

        var metrics = new
        {
            resourceMetrics = new[] { new
            {
                resource = new { attributes = new[] { new { key = "service.name", value = new { stringValue = service } } } },
                scopeMetrics = new[] { new { metrics = new[] { new { name = "observability_proof_gauge", gauge = new { dataPoints = new[] { new { asDouble = 42.0, timeUnixNano = timestamp } } } } } } }
            } }
        };
        using HttpResponseMessage metricResponse = await client.PostAsJsonAsync("http://localhost:4318/v1/metrics", metrics, TestContext.Current.CancellationToken);
        metricResponse.EnsureSuccessStatusCode();

        await WaitForAsync(client, $"/api/datasources/proxy/uid/loki/loki/api/v1/query_range?query={Uri.EscapeDataString($"{{service_name=\"{service}\"}} |= \"{marker}\"")}&limit=10", root =>
            root.GetProperty("data").GetProperty("result").EnumerateArray().Any(stream =>
                stream.GetProperty("values").EnumerateArray().Any(value => value[1].GetString() == marker)));

        await WaitForAsync(client, $"/api/datasources/proxy/uid/tempo/api/traces/{traceId}", root =>
            root.GetProperty("batches").EnumerateArray().Any(batch =>
                batch.GetProperty("scopeSpans").EnumerateArray().Any(scope =>
                    scope.GetProperty("spans").EnumerateArray().Any(span => span.GetProperty("name").GetString() == marker))));

        await WaitForAsync(client, "/api/datasources/proxy/uid/prometheus/api/v1/query?query=observability_proof_gauge", root =>
            root.GetProperty("data").GetProperty("result").EnumerateArray().Any(result =>
                result.GetProperty("value")[1].GetString() == "42"));
    }

    private static async Task WaitForAsync(HttpClient client, string path, Func<JsonElement, bool> condition)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync("http://localhost:3300" + path, TestContext.Current.CancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                    if (condition(body.RootElement))
                    {
                        return;
                    }
                }
            }
            catch (HttpRequestException) when (attempt < 14)
            {
                // Backend indexing may not be ready immediately.
            }

            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Telemetry was not queryable at {path} within 30 seconds.");
    }
}

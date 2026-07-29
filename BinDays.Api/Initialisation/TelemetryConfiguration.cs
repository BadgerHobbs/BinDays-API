namespace BinDays.Api.Initialisation;

using BinDays.Api.Telemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Reflection;

/// <summary>
/// Configures OpenTelemetry for the application, covering metric collection and
/// log export. Metrics are always collected, as they are held in process and
/// exposed for scraping. Log export is only configured when an OTLP endpoint is
/// supplied, leaving local runs and the integration tests unaffected.
/// </summary>
internal static class TelemetryConfiguration
{
	/// <summary>
	/// Histogram boundaries for the number of addresses returned by a lookup.
	/// Chosen to match the range the values actually occupy.
	/// </summary>
	private static readonly double[] AddressCountBoundaries = [0, 1, 5, 10, 25, 50, 100, 250];

	/// <summary>
	/// Histogram boundaries for the number of bin days returned by a lookup.
	/// Collections are fortnightly at most, so the useful range is small.
	/// </summary>
	private static readonly double[] BinDayCountBoundaries = [0, 1, 2, 4, 8, 16, 32];

	/// <summary>
	/// Adds metric collection, and log export when an OTLP endpoint is configured.
	/// </summary>
	/// <param name="builder">The application builder.</param>
	public static void AddTelemetry(this WebApplicationBuilder builder)
	{
		var environmentName = builder.Environment.EnvironmentName.ToLowerInvariant();

		builder.Services.AddOpenTelemetry()
			.ConfigureResource(resource => ConfigureResource(resource, environmentName))
			.WithMetrics(ConfigureMetrics);

		var otlpLogsEndpoint = builder.Configuration.GetValue<string>("Otlp:LogsEndpoint");

		if (string.IsNullOrEmpty(otlpLogsEndpoint))
		{
			return;
		}

		builder.Logging.AddOpenTelemetry(options => ConfigureLogging(options, otlpLogsEndpoint));
	}

	/// <summary>
	/// Describes this service on every exported metric and log.
	/// </summary>
	/// <param name="resource">The resource builder to configure.</param>
	/// <param name="environmentName">The name of the current hosting environment.</param>
	private static void ConfigureResource(ResourceBuilder resource, string environmentName)
	{
		var serviceVersion = Assembly.GetExecutingAssembly()
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

		resource.AddService(serviceName: BinDaysMetrics.ServiceName, serviceVersion: serviceVersion);
		resource.AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environmentName)]);
	}

	/// <summary>
	/// Configures the metrics to collect and the endpoint to expose them on.
	/// </summary>
	/// <param name="metrics">The meter provider builder to configure.</param>
	private static void ConfigureMetrics(MeterProviderBuilder metrics)
	{
		// Metric instrumentation has no request filter, unlike tracing. Scrape and
		// health traffic is therefore excluded at query time via http_route.
		metrics.AddAspNetCoreInstrumentation();
		metrics.AddHttpClientInstrumentation();
		metrics.AddRuntimeInstrumentation();
		metrics.AddMeter(BinDaysMetrics.MeterName);

		// The default boundaries span up to 10000, which is far outside the range
		// either value occupies, so most buckets would be empty while the range that
		// matters collapses into one. Narrower boundaries give usable resolution and
		// cut the series count, which is charged once per collector.
		metrics.AddView(
			instrumentName: BinDaysMetrics.AddressesReturnedInstrument,
			new ExplicitBucketHistogramConfiguration { Boundaries = AddressCountBoundaries }
		);
		metrics.AddView(
			instrumentName: BinDaysMetrics.BinDaysReturnedInstrument,
			new ExplicitBucketHistogramConfiguration { Boundaries = BinDayCountBoundaries }
		);

		metrics.AddPrometheusExporter();
	}

	/// <summary>
	/// Configures log export over OTLP.
	/// </summary>
	/// <param name="options">The logger options to configure.</param>
	/// <param name="otlpLogsEndpoint">The endpoint to export logs to.</param>
	private static void ConfigureLogging(OpenTelemetryLoggerOptions options, string otlpLogsEndpoint)
	{
		// Required, or the properties LoggerData attaches via BeginScope never reach
		// the exporter.
		options.IncludeScopes = true;

		// Defaults to false, which would export the raw message template rather than
		// the rendered message.
		options.IncludeFormattedMessage = true;

		options.AddOtlpExporter(exporter => ConfigureOtlpExporter(exporter, otlpLogsEndpoint));
	}

	/// <summary>
	/// Configures the OTLP exporter to send to a Loki OTLP receiver.
	/// </summary>
	/// <param name="exporter">The exporter options to configure.</param>
	/// <param name="otlpLogsEndpoint">The endpoint to export logs to.</param>
	private static void ConfigureOtlpExporter(OtlpExporterOptions exporter, string otlpLogsEndpoint)
	{
		// Loki's OTLP receiver is HTTP only, and the signal path is not appended to an
		// endpoint set in code, so the full URL is required.
		exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
		exporter.Endpoint = new(otlpLogsEndpoint);
	}
}

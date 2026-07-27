using BinDays.Api.Initialisation;
using BinDays.Api.Telemetry;
using Microsoft.OpenApi;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Scalar.AspNetCore;
using StackExchange.Redis;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Use Autofac as the service provider factory
builder.Host.UseServiceProviderFactory(new Autofac.Extensions.DependencyInjection.AutofacServiceProviderFactory());

// Register services directly with Autofac using the ConfigureContainer method
builder.Host.ConfigureContainer<Autofac.ContainerBuilder>(BinDays.Api.Initialisation.DependencyInjection.ConfigureContainer);

var redis = builder.Configuration.GetValue<string>("Redis");

// Endpoint for exporting logs via OTLP, e.g. to Loki (optional)
var otlpLogsEndpoint = builder.Configuration.GetValue<string>("Otlp:LogsEndpoint");

builder.Services.AddControllers(options =>
{
	if (string.IsNullOrEmpty(redis))
	{
		options.Conventions.Add(new ExcludeCacheControllerConvention());
	}
});

// Add caching for responses, either in-memory or Redis
if (!string.IsNullOrEmpty(redis))
{
	var multiplexer = ConnectionMultiplexer.Connect(redis);
	builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

	builder.Services.AddStackExchangeRedisCache(options =>
	{
		options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
	});
}
else
{
	builder.Services.AddDistributedMemoryCache();
}

// Health check for monitoring
builder.Services.AddHealthChecks();

// Configure Seq logging (optional)
builder.Services.AddLogging(loggingBuilder =>
{
	loggingBuilder.AddSeq(builder.Configuration.GetSection("Seq"));
});

// Describe this service on every exported metric and log
var serviceVersion = Assembly.GetExecutingAssembly()
	.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

builder.Services.AddOpenTelemetry()
	.ConfigureResource(resource => resource
		.AddService(serviceName: BinDaysMetrics.ServiceName, serviceVersion: serviceVersion)
		.AddAttributes(
		[
			new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName.ToLowerInvariant()),
		]))
	.WithMetrics(metrics => metrics
		// Metrics instrumentation has no request filter, unlike tracing. Scrape and health
		// traffic is therefore excluded at query time via http_route, not here.
		.AddAspNetCoreInstrumentation()
		.AddHttpClientInstrumentation()
		.AddRuntimeInstrumentation()
		.AddMeter(BinDaysMetrics.MeterName)
		// Explicit buckets, as the default 18 boundaries across every collector is needless cardinality.
		.AddView(
			instrumentName: "bindays.addresses.returned",
			new ExplicitBucketHistogramConfiguration { Boundaries = [0, 1, 5, 10, 25, 50, 100, 250] })
		.AddView(
			instrumentName: "bindays.bin_days.returned",
			new ExplicitBucketHistogramConfiguration { Boundaries = [0, 1, 2, 4, 8, 16, 32] })
		.AddPrometheusExporter());

// Configure OTLP log export, e.g. to Loki (optional)
if (!string.IsNullOrEmpty(otlpLogsEndpoint))
{
	builder.Logging.AddOpenTelemetry(options =>
	{
		// Required, or the properties LoggerData attaches via BeginScope never reach the exporter.
		options.IncludeScopes = true;

		// Defaults to false, which would export the raw message template rather than the rendered message.
		options.IncludeFormattedMessage = true;

		options.AddOtlpExporter(exporter =>
		{
			// Loki's OTLP receiver is HTTP only, and the signal path is not appended to an
			// endpoint set in code, so the full URL is required.
			exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
			exporter.Endpoint = new(otlpLogsEndpoint);
		});
	});
}

builder.Services.AddOpenApi(options =>
{
	options.AddDocumentTransformer((document, context, ct) =>
	{
		document.Components ??= new OpenApiComponents();
		document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
		document.Components.SecuritySchemes.Add("ApiKey", new OpenApiSecurityScheme
		{
			Type = SecuritySchemeType.ApiKey,
			In = ParameterLocation.Header,
			Name = "X-Api-Key",
		});
		return Task.CompletedTask;
	});
});

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference((options, context) =>
{
	options
		.WithOperationTitleSource(OperationTitleSource.Path)
		.AddApiKeyAuthentication("ApiKey", scheme => { })
		.AddServer(new ScalarServer($"{context.Request.Scheme}://{context.Request.Host}"));
});

app.UseCors(x => x
	.AllowAnyOrigin()
	.AllowAnyMethod()
	.AllowAnyHeader()
);

// Scrape and health traffic arrives over plaintext HTTP on the internal container network and
// must never be redirected. No HTTPS port is configured in the container, so the middleware is
// already inert there, but this makes the guarantee explicit rather than incidental.
app.UseWhen(
	context => !context.Request.Path.StartsWithSegments("/metrics")
		&& !context.Request.Path.StartsWithSegments("/status"),
	branch => branch.UseHttpsRedirection()
);

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/status");

// Metrics for monitoring, scraped over the internal container network only
app.MapPrometheusScrapingEndpoint();

app.Run();

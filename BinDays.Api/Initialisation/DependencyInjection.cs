namespace BinDays.Api.Initialisation;

using Autofac;
using BinDays.Api.Collectors.Collectors;
using BinDays.Api.Collectors.Services;
using BinDays.Api.Collectors.Telemetry;
using BinDays.Api.Telemetry;

/// <summary>
/// Configures dependency injection for the application using Autofac.
/// </summary>
internal static class DependencyInjection
{
	/// <summary>
	/// Configures the Autofac container with application-specific services.
	/// </summary>
	/// <param name="builder">The Autofac container builder.</param>
	public static void ConfigureContainer(ContainerBuilder builder)
	{
		// Register implementations of ICollector
		var collectorsAssembly = typeof(ICollector).Assembly;

		builder.RegisterAssemblyTypes(collectorsAssembly)
			.AssignableTo<ICollector>()
			.Where(t => t.IsInterface == false && t.IsAbstract == false)
			.As<ICollector>();

		// Register collector service.
		builder.RegisterType<CollectorService>();

		// Register metric instruments. Must be a single instance, as each one owns a Meter.
		// Exposed as ICollectorMetrics as well as itself, so CollectorService can record from
		// inside the pipeline without the collectors project depending on this one.
		builder.RegisterType<BinDaysMetrics>()
			.AsSelf()
			.As<ICollectorMetrics>()
			.SingleInstance();
	}
}

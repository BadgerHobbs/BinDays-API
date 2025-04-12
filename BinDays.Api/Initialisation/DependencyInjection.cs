namespace BinDays.Api.Initialisation
{
    using Autofac;
    using BinDays.Api.Collectors.Collectors;
    using BinDays.Api.Collectors.Services;

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
            builder.RegisterType<ICollector>().AsImplementedInterfaces();

            // Register collector service.
            builder.RegisterType<CollectorService>();
        }
    }
}
